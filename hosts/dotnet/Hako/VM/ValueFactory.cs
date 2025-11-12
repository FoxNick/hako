using System.Collections;
using System.Runtime.CompilerServices;
using HakoJS.Exceptions;
using HakoJS.Host;
using HakoJS.SourceGeneration;

namespace HakoJS.VM;

internal sealed class ValueFactory(Realm context) : IDisposable
{
    private Realm Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

    public void Dispose()
    {
    }


    public JSValue FromNativeValue(object? value, Dictionary<string, object>? options = null)
    {
        options ??= new Dictionary<string, object>();

        if (value == null) return CreateNull();

        return value switch
        {
            IJSMarshalable marshalable => marshalable.ToJSValue(Context),
            bool b => CreateBoolean(b),
            long => CreateBigInt(Convert.ToInt64(value)),
            ulong  => CreateBigUInt(Convert.ToUInt64(value)),
            byte or sbyte or short or ushort or int or uint or float or double or decimal
                => CreateNumber(Convert.ToDouble(value)),
            string str => CreateString(str),
            DBNull => CreateNull(),
            JSFunction func => CreateFunction(func, options),
            byte[] bytes => CreateArrayBuffer(bytes),
            ArraySegment<byte> segment => CreateArrayBuffer(segment.Array ?? []),
            Array arr => CreateArray(arr),
            IList list => CreateArray(list),
            DateTime dt => CreateDate(dt),
            DateTimeOffset dto => CreateDate(dto.DateTime),
            Exception ex => CreateError(ex),
            IDictionary dict => CreateObjectFromDictionary(dict, options),
            _ => CreateObjectFromAnonymous(value, options)
        };
    }

    #region Global Object

    public JSValue GetGlobalObject()
    {
        return new JSValue(Context, Context.Runtime.Registry.GetGlobalObject(Context.Pointer));
    }

    #endregion

    #region Circular Reference Detection

    private static void DetectCircularReferences(
        object obj,
        HashSet<object>? seen = null,
        string path = "root")
    {
        if (obj is string || obj.GetType().IsPrimitive)
            return;

        seen ??= new HashSet<object>(ReferenceEqualityComparer.Instance);

        if (!seen.Add(obj))
            throw new InvalidOperationException($"Circular reference detected at {path}");

        try
        {
            switch (obj)
            {
                case IDictionary dict:
                    foreach (DictionaryEntry entry in dict)
                        if (entry.Value != null)
                            DetectCircularReferences(entry.Value, seen, $"{path}.{entry.Key}");
                    break;

                case IEnumerable enumerable:
                    var index = 0;
                    foreach (var item in enumerable)
                    {
                        if (item != null)
                            DetectCircularReferences(item, seen, $"{path}[{index}]");
                        index++;
                    }

                    break;
            }
        }
        finally
        {
            seen.Remove(obj);
        }
    }

    #endregion

    #region Primitive Creation

    public JSValue CreateUndefined()
    {
        return new JSValue(Context, Context.Runtime.Registry.GetUndefined(), ValueLifecycle.Borrowed);
    }

    private JSValue CreateNull()
    {
        return new JSValue(Context, Context.Runtime.Registry.GetNull(), ValueLifecycle.Borrowed);
    }

    private JSValue CreateBoolean(bool value)
    {
        return new JSValue(Context,
            value ? Context.Runtime.Registry.GetTrue() : Context.Runtime.Registry.GetFalse(),
            ValueLifecycle.Borrowed);
    }

    private JSValue CreateBigInt(long value)
    {
        var big = Context.Runtime.Registry.NewBigInt(Context.Pointer,value);
        var error = Context.GetLastError(big);
        if (error != null)
        {
            Context.FreeValuePointer(big);
            throw new HakoException("Error creating BigInt", error);
        }
        return new JSValue(Context, big);
    }
    
    private JSValue CreateBigUInt(ulong value)
    {

        var big = Context.Runtime.Registry.NewBigUInt(Context.Pointer, value);
        var error = Context.GetLastError(big);
        if (error != null)
        {
            Context.FreeValuePointer(big);
            throw new HakoException("Error creating BigUInt", error);
        }
        return new JSValue(Context, big);
    }

    private JSValue CreateNumber(double value)
    {
        var numPtr = Context.Runtime.Registry.NewFloat64(Context.Pointer, value);
        return new JSValue(Context, numPtr);
    }

    private JSValue CreateString(string value)
    {
        int strPtr = Context.AllocateString(value, out _);
        try
        {
            var jsStrPtr = Context.Runtime.Registry.NewString(Context.Pointer, strPtr);
            return new JSValue(Context, jsStrPtr);
        }
        finally
        {
            Context.FreeMemory(strPtr);
        }
    }

    #endregion

    #region Complex Type Creation

    private JSValue CreateFunction(JSFunction callback,
        Dictionary<string, object> options)
    {
        if (!options.TryGetValue("name", out var nameObj) || nameObj is not string name)
            throw new ArgumentException("Function name is required in options");

        var functionId = Context.Runtime.Callbacks.NewFunction(Context.Pointer, callback, name);
        return new JSValue(Context, functionId);
    }

    private JSValue CreateArray(IEnumerable enumerable)
    {
        var arrayPtr = Context.Runtime.Registry.NewArray(Context.Pointer);
        var jsArray = new JSValue(Context, arrayPtr);

        var index = 0;
        foreach (var item in enumerable)
        {
            using var vmItem = FromNativeValue(item);
            jsArray.SetProperty(index++, vmItem);
        }

        return jsArray;
    }

    private JSValue CreateDate(DateTime value)
    {
        var timestamp = (value.ToUniversalTime() -
                         new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        var datePtr = Context.Runtime.Registry.NewDate(Context.Pointer, timestamp);

        var error = Context.GetLastError(datePtr);
        if (error != null)
        {
            Context.FreeValuePointer(datePtr);
            throw error;
        }

        return new JSValue(Context, datePtr);
    }

    private JSValue CreateError(Exception error)
    {
        var errorPtr = Context.Runtime.Registry.NewError(Context.Pointer);
        if (errorPtr == 0) throw new InvalidOperationException("Failed to create error object");

        try
        {
            using var message = CreateString(error.Message);
            using var name = CreateString("NativeException");
            using var stack = CreateString(error.StackTrace ?? "");

            SetErrorProperty(errorPtr, "message", message);
            SetErrorProperty(errorPtr, "name", name);

            if (error.InnerException != null)
            {
                using var cause = FromNativeValue(error.InnerException);
                SetErrorProperty(errorPtr, "cause", cause);
            }

            SetErrorProperty(errorPtr, "stack", stack);

            return new JSValue(Context, errorPtr);
        }
        catch
        {
            // If anything goes wrong, free the error pointer before re-throwing
            Context.FreeValuePointer(errorPtr);
            throw;
        }
    }

    private void SetErrorProperty(int errorPtr, string key, JSValue value)
    {
        using var keyValue = CreateString(key);
        var result = Context.Runtime.Registry.SetProp(
            Context.Pointer,
            errorPtr,
            keyValue.GetHandle(),
            value.GetHandle());

        if (result == -1)
        {
            var error = Context.GetLastError();
            if (error != null) throw new HakoException("Error setting error property: ", error);
        }
    }

    private JSValue CreateArrayBuffer(byte[] data)
    {
        var valuePtr = Context.NewArrayBufferPtr(data);

        var lastError = Context.GetLastError(valuePtr);
        if (lastError != null)
        {
            Context.FreeValuePointer(valuePtr);
            throw lastError;
        }

        return new JSValue(Context, valuePtr);
    }

    private JSValue CreateObjectFromDictionary(IDictionary dict, Dictionary<string, object> options)
    {
        DetectCircularReferences(dict);

        var objPtr = options.TryGetValue("proto", out var protoObj) && protoObj is JSValue proto
            ? Context.Runtime.Registry.NewObjectProto(Context.Pointer, proto.GetHandle())
            : Context.Runtime.Registry.NewObject(Context.Pointer);

        var lastError = Context.GetLastError(objPtr);
        if (lastError != null)
        {
            Context.FreeValuePointer(objPtr);
            throw lastError;
        }

        using var jsObj = new JSValue(Context, objPtr);

        foreach (DictionaryEntry entry in dict)
        {
            var key = entry.Key?.ToString() ?? "";
            using var propValue = FromNativeValue(entry.Value, options);
            jsObj.SetProperty(key, propValue);
        }

        return jsObj.Dup();
    }

    private JSValue CreateObjectFromAnonymous(object obj, Dictionary<string, object> options)
    {
        switch (obj)
        {
            case IDictionary dict:
                return CreateObjectFromDictionary(dict, options);
            case IEnumerable<KeyValuePair<string, object>> kvps:
            {
                var tempDict = new Dictionary<string, object>();
                foreach (var kvp in kvps)
                    tempDict[kvp.Key] = kvp.Value;
                return CreateObjectFromDictionary(tempDict, options);
            }
        }

        var objPtr = Context.Runtime.Registry.NewObject(Context.Pointer);

        var lastError = Context.GetLastError(objPtr);
        if (lastError != null)
        {
            Context.FreeValuePointer(objPtr);
            throw lastError;
        }

        return new JSValue(Context, objPtr);
    }

    #endregion
}

internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new();

    private ReferenceEqualityComparer()
    {
    }

    public new bool Equals(object? x, object? y)
    {
        return ReferenceEquals(x, y);
    }

    public int GetHashCode(object obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}