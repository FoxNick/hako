using System.Diagnostics;
using HakoJS.Exceptions;
using HakoJS.Lifetime;
using HakoJS.SourceGeneration;
using HakoJS.Utils;

namespace HakoJS.VM;

/// <summary>
/// Represents a JavaScript value with automatic lifetime management and type-safe conversion methods.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JSValue"/> is the fundamental type for all JavaScript values in HakoJS. It wraps a QuickJS
/// value handle and provides type checking, conversion, property access, and disposal semantics.
/// </para>
/// <para>
/// JavaScript values in HakoJS use reference counting for memory management. Each <see cref="JSValue"/>
/// must be disposed when no longer needed, either explicitly via <see cref="Dispose"/> or automatically
/// using <c>using</c> statements.
/// </para>
/// <para>
/// The class supports two lifecycle modes via <see cref="ValueLifecycle"/>:
/// <list type="bullet">
/// <item><b>Owned</b>: The value owns its reference and frees it on disposal (default)</item>
/// <item><b>Borrowed</b>: The value doesn't own its reference and won't free it on disposal</item>
/// </list>
/// </para>
/// <para>
/// Example usage:
/// <code>
/// // Create and use a value
/// using var str = realm.NewString("Hello");
/// Console.WriteLine(str.AsString()); // "Hello"
/// 
/// // Work with objects
/// using var obj = realm.NewObject();
/// obj.SetProperty("name", "Alice");
/// using var name = obj.GetProperty("name");
/// Console.WriteLine(name.AsString()); // "Alice"
/// 
/// // Type checking
/// if (str.IsString())
/// {
///     var length = str.AsString().Length;
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class JSValue(Realm realm, int handle, ValueLifecycle lifecycle = ValueLifecycle.Owned)
    : IDisposable, IAlive
{
    private bool _disposed;
    private int _handle = handle;

    /// <summary>
    /// Gets the realm in which this value exists.
    /// </summary>
    /// <value>
    /// The <see cref="VM.Realm"/> that owns this value.
    /// </value>
    /// <exception cref="ArgumentNullException">Thrown if realm is <c>null</c> during construction.</exception>
    public Realm Realm { get; } = realm ?? throw new ArgumentNullException(nameof(realm));

    /// <summary>
    /// Gets a value indicating whether this <see cref="JSValue"/> is still valid and has not been disposed.
    /// </summary>
    /// <value>
    /// <c>true</c> if the value is alive and can be used; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// A value is considered alive if it has a non-zero handle and has not been disposed.
    /// Always check this property before using a value that may have been disposed.
    /// </remarks>
    public bool Alive => _handle != 0 && !_disposed;

    /// <summary>
    /// Creates a <see cref="JSValue"/> from a raw handle with a specified lifecycle.
    /// </summary>
    /// <param name="realm">The realm in which the value exists.</param>
    /// <param name="handle">The QuickJS value handle.</param>
    /// <param name="lifecycle">The lifecycle mode for the value.</param>
    /// <returns>A new <see cref="JSValue"/> wrapping the handle.</returns>
    /// <remarks>
    /// This is an advanced method typically used internally. Most users should use realm methods
    /// like <see cref="Realm.NewString"/> or <see cref="Realm.NewObject"/> to create values.
    /// </remarks>
    public static JSValue FromHandle(Realm realm, int handle, ValueLifecycle lifecycle)
    {
        return new JSValue(realm, handle, lifecycle);
    }

    /// <summary>
    /// Gets the underlying QuickJS value handle.
    /// </summary>
    /// <returns>An integer representing the QuickJS value handle.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// This is an advanced method used for interop with low-level QuickJS operations.
    /// The handle is only valid while the <see cref="JSValue"/> is alive.
    /// </remarks>
    public int GetHandle()
    {
        AssertAlive();
        return _handle;
    }

    /// <summary>
    /// Gets the pointer to the realm's QuickJS context.
    /// </summary>
    /// <returns>An integer representing the context pointer.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// This is an advanced method used for low-level QuickJS operations.
    /// </remarks>
    public int GetRealmPointer()
    {
        AssertAlive();
        return Realm.Pointer;
    }

    
    #region Conversion to Native

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public TValue As<TValue>() where TValue : IJSMarshalable<TValue>
    {
        AssertAlive();
        return TValue.FromJSValue(realm, this);
    }
    
    private static bool TryGetMarshalableConverter<T>(out Func<Realm, JSValue, T>? converter) 
        where T : IJSMarshalable<T>
    {
        converter = T.FromJSValue;
        return true;
    } 

    /// <summary>
    /// Converts the JavaScript value to a .NET value of the specified type.
    /// </summary>
    /// <typeparam name="T">The target .NET type.</typeparam>
    /// <returns>
    /// A <see cref="NativeBox{T}"/> containing the converted value and managing disposal of
    /// intermediate JavaScript values.
    /// </returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The conversion is not supported or failed.</exception>
    /// <remarks>
    /// <para>
    /// This method performs deep conversion from JavaScript to .NET types:
    /// </para>
    /// <list type="bullet">
    /// <item><b>undefined</b> → <c>default(T)</c></item>
    /// <item><b>null</b> → <c>default(T)</c></item>
    /// <item><b>boolean</b> → <c>bool</c></item>
    /// <item><b>number</b> → <c>double</c>, <c>int</c>, <c>long</c>, <c>short</c>, <c>byte</c>, etc.</item>
    /// <item><b>string</b> → <c>string</c></item>
    /// <item><b>BigInt</b> → <c>long</c></item>
    /// <item><b>array</b> → <c>object[]</c></item>
    /// <item><b>object</b> → <c>Dictionary&lt;string, object?&gt;</c> or <c>Dictionary&lt;string, string&gt;</c></item>
    /// <item><b>function</b> → <c>Func&lt;object?[], object?&gt;</c></item>
    /// </list>
    /// <para>
    /// The returned <see cref="NativeBox{T}"/> must be disposed to clean up intermediate values.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var jsValue = realm.EvalCode("[1, 2, 3]").Unwrap();
    /// using var native = jsValue.ToNativeValue&lt;object[]&gt;();
    /// var array = native.Value; // object[] { 1.0, 2.0, 3.0 }
    /// 
    /// using var jsNum = realm.EvalCode("42").Unwrap();
    /// using var intNative = jsNum.ToNativeValue&lt;int&gt;();
    /// int value = intNative.Value; // 42
    /// </code>
    /// </para>
    /// </remarks>
    public NativeBox<T> ToNativeValue<T>()
    {
        AssertAlive();
        var type = Type;
        var disposables = new List<IDisposable> { this };
        
        NativeBox<T> CreateResult(object value)
        {
            var alive = true;
            return new NativeBox<T>(
                (T)value,
                _ =>
                {
                    if (!alive) return;
                    alive = false;
                    foreach (var d in disposables)
                        d.Dispose();
                });
        }

        try
        {
            if (typeof(T) == typeof(string))
            {
                return CreateResult(AsString());
            }
            return type switch
            {
                JSType.Undefined => CreateResult(default(T)!),
                JSType.Boolean => CreateResult(AsBoolean()),
                JSType.Number => CreateResult(ConvertNumber()),
                JSType.String => CreateResult(AsString()),
                JSType.BigInt => CreateResult(GetBigInt()),
                JSType.Object => ConvertObject(),
                JSType.Function => CreateResult(CreateStandaloneFunction()),
                _ => throw new InvalidOperationException("Unknown type")
            };

            object ConvertNumber()
            {
                var numValue = AsNumber();
                
                if (typeof(T) == typeof(int))
                    return (int)numValue;
                if (typeof(T) == typeof(long))
                    return (long)numValue;
                if (typeof(T) == typeof(short))
                    return (short)numValue;
                if (typeof(T) == typeof(byte))
                    return (byte)numValue;
                if (typeof(T) == typeof(uint))
                    return (uint)numValue;
                if (typeof(T) == typeof(ulong))
                    return (ulong)numValue;
                if (typeof(T) == typeof(ushort))
                    return (ushort)numValue;
                if (typeof(T) == typeof(sbyte))
                    return (sbyte)numValue;
                if (typeof(T) == typeof(float))
                    return (float)numValue;
                if (typeof(T) == typeof(decimal))
                    return (decimal)numValue;
                return numValue;
            }

            NativeBox<T> ConvertObject()
            {
                if (IsNull()) return CreateResult(default(T)!);
                if (IsArray()) return CreateResult(ConvertArray());

                return typeof(T) == typeof(Dictionary<string, string>)
                    ? CreateResult(ConvertToStringDictionary())
                    : CreateResult(ConvertToObjectDictionary());
            }

            object?[] ConvertArray()
            {
                var length = GetLength();
                var result = new object?[length];
                for (var i = 0; i < length; i++)
                {
                    var item = GetProperty(i).ToNativeValue<object>();
                    disposables.Add(item);
                    result[i] = item.Value;
                }

                return result;
            }
           
            Dictionary<string, string> ConvertToStringDictionary()
            {
                var dict = new Dictionary<string, string>();
                foreach (var prop in GetOwnPropertyNames())
                {
                    var propName = prop.AsString();
                    prop.Dispose();
                    using var valueVm = GetProperty(propName);
                    dict[propName] = valueVm.IsNullOrUndefined() ? "" : valueVm.AsString();
                }

                return dict;
            }

            Dictionary<string, object?> ConvertToObjectDictionary()
            {
                var obj = new Dictionary<string, object?>();
                var thisObject = this;

                foreach (var prop in GetOwnPropertyNames())
                {
                    var propName = prop.AsString();
                    prop.Dispose();
                    var propValue = GetProperty(propName);

                    if (propValue.IsFunction())
                    {
                        obj[propName] = CreateBoundMethod(propValue, thisObject);
                        disposables.Add(propValue);
                    }
                    else
                    {
                        var value = propValue.ToNativeValue<object>();
                        disposables.Add(value);
                        obj[propName] = value.Value;
                    }
                }

                return obj;
            }

            Func<object?[], object?> CreateBoundMethod(JSValue function, JSValue thisContext)
            {
                return args =>
                {
                    var jsArgs = new JSValue[args.Length];
                    try
                    {
                        for (var i = 0; i < args.Length; i++) jsArgs[i] = Realm.NewValue(args[i]);

                        using var result = Realm.CallFunction(function, thisContext, jsArgs).Unwrap();
                        using var resultJs = result.ToNativeValue<object>();
                        return resultJs.Value;
                    }
                    finally
                    {
                        foreach (var arg in jsArgs) arg?.Dispose();
                    }
                };
            }

            Func<object?[], object?> CreateStandaloneFunction()
            {
                return args =>
                {
                    var jsArgs = new JSValue[args.Length];
                    try
                    {
                        for (var i = 0; i < args.Length; i++) jsArgs[i] = Realm.NewValue(args[i]);

                        using var result = Realm.CallFunction(this, null, jsArgs).Unwrap();
                        using var resultJs = result.ToNativeValue<object>();
                        return resultJs.Value;
                    }
                    finally
                    {
                        foreach (var arg in jsArgs) arg?.Dispose();
                    }
                };
            }
        }
        catch
        {
            foreach (var d in disposables)
                d.Dispose();
            throw;
        }
    }

    #endregion

    #region Type Checking

    /// <summary>
    /// Gets the JavaScript type of this value.
    /// </summary>
    /// <value>
    /// A <see cref="JSType"/> enumeration value indicating the type.
    /// </value>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// Note that JavaScript <c>null</c> returns <see cref="JSType.Object"/>, which matches
    /// JavaScript's <c>typeof null === "object"</c> behavior. Use <see cref="IsNull"/> to
    /// specifically check for <c>null</c>.
    /// </remarks>
    public JSType Type
    {
        get
        {
            AssertAlive();
            if (IsNull()) return JSType.Object;

            var typeId = Realm.Runtime.Registry.TypeOf(Realm.Pointer, _handle);
            return typeId switch
            {
                0 => JSType.Undefined,
                1 => JSType.Object,
                2 => JSType.String,
                3 => JSType.Symbol,
                4 => JSType.Boolean,
                5 => JSType.Number,
                6 => JSType.BigInt,
                7 => JSType.Function,
                _ => JSType.Undefined
            };
        }
    }

    /// <summary>
    /// Checks if the value is <c>undefined</c>.
    /// </summary>
    /// <returns><c>true</c> if the value is <c>undefined</c>; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public bool IsUndefined()
    {
        AssertAlive();
        return Realm.Runtime.Registry.IsUndefined(_handle) != 0;
    }

    /// <summary>
    /// Checks if the value is <c>null</c>.
    /// </summary>
    /// <returns><c>true</c> if the value is <c>null</c>; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public bool IsNull()
    {
        AssertAlive();
        return Realm.Runtime.Registry.IsNull(_handle) != 0;
    }

    /// <summary>
    /// Checks if the value is a boolean.
    /// </summary>
    /// <returns><c>true</c> if the value is a boolean; otherwise, <c>false</c>.</returns>
    public bool IsBoolean()
    {
        return Type == JSType.Boolean;
    }

    /// <summary>
    /// Checks if the value is a number.
    /// </summary>
    /// <returns><c>true</c> if the value is a number; otherwise, <c>false</c>.</returns>
    public bool IsNumber()
    {
        return Type == JSType.Number;
    }

    /// <summary>
    /// Checks if the value is a string.
    /// </summary>
    /// <returns><c>true</c> if the value is a string; otherwise, <c>false</c>.</returns>
    public bool IsString()
    {
        return Type == JSType.String;
    }

    /// <summary>
    /// Checks if the value is a symbol.
    /// </summary>
    /// <returns><c>true</c> if the value is a symbol; otherwise, <c>false</c>.</returns>
    public bool IsSymbol()
    {
        return Type == JSType.Symbol;
    }

    /// <summary>
    /// Checks if the value is an object (including arrays, but not <c>null</c>).
    /// </summary>
    /// <returns><c>true</c> if the value is an object; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This returns <c>false</c> for <c>null</c> even though <c>typeof null === "object"</c> in JavaScript.
    /// </remarks>
    public bool IsObject()
    {
        return Type == JSType.Object;
    }

    /// <summary>
    /// Checks if the value is a function.
    /// </summary>
    /// <returns><c>true</c> if the value is a function; otherwise, <c>false</c>.</returns>
    public bool IsFunction()
    {
        return Type == JSType.Function;
    }

    /// <summary>
    /// Checks if the value is an array.
    /// </summary>
    /// <returns><c>true</c> if the value is an array; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public bool IsArray()
    {
        AssertAlive();
        return Realm.Runtime.Registry.IsArray(Realm.Pointer, _handle) != 0;
    }

    /// <summary>
    /// Checks if the value is an Error object.
    /// </summary>
    /// <returns><c>true</c> if the value is an Error; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public bool IsError()
    {
        AssertAlive();
        return Realm.Runtime.Registry.IsError(Realm.Pointer, _handle) != 0;
    }

    /// <summary>
    /// Checks if the value is an exception (special internal error representation).
    /// </summary>
    /// <returns><c>true</c> if the value is an exception; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// This checks for QuickJS's internal exception representation, which is different from
    /// checking if a value is an Error object. Used primarily for error handling.
    /// </remarks>
    public bool IsException()
    {
        AssertAlive();
        return Realm.Runtime.Registry.IsException(_handle) != 0;
    }

    /// <summary>
    /// Checks if the value is a Promise.
    /// </summary>
    /// <returns><c>true</c> if the value is a Promise; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public bool IsPromise()
    {
        AssertAlive();
        return Realm.Runtime.Registry.IsPromise(Realm.Pointer, _handle) != 0;
    }

    /// <summary>
    /// Checks if the value is a TypedArray (Uint8Array, Int32Array, Float64Array, etc.).
    /// </summary>
    /// <returns><c>true</c> if the value is a TypedArray; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public bool IsTypedArray()
    {
        AssertAlive();
        return Realm.Runtime.Registry.IsTypedArray(Realm.Pointer, _handle) != 0;
    }

    /// <summary>
    /// Checks if the value is an ArrayBuffer.
    /// </summary>
    /// <returns><c>true</c> if the value is an ArrayBuffer; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public bool IsArrayBuffer()
    {
        AssertAlive();
        return Realm.Runtime.Registry.IsArrayBuffer(_handle) != 0;
    }

    /// <summary>
    /// Checks if the value is a global symbol (created with Symbol.for).
    /// </summary>
    /// <returns><c>true</c> if the value is a global symbol; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public bool IsGlobalSymbol()
    {
        AssertAlive();
        return Realm.Runtime.Registry.IsGlobalSymbol(Realm.Pointer, _handle) == 1;
    }

    #endregion

    #region Type Conversion

    /// <summary>
    /// Converts the value to a number (double).
    /// </summary>
    /// <returns>The numeric value as a <c>double</c>.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This follows JavaScript's ToNumber conversion rules:
    /// <list type="bullet">
    /// <item><c>undefined</c> → <c>NaN</c></item>
    /// <item><c>null</c> → <c>0</c></item>
    /// <item><c>true</c> → <c>1</c></item>
    /// <item><c>false</c> → <c>0</c></item>
    /// <item>String → parsed number or <c>NaN</c></item>
    /// <item>Object → depends on valueOf/toString</item>
    /// </list>
    /// </para>
    /// </remarks>
    public double AsNumber()
    {
        AssertAlive();
        return Realm.Runtime.Registry.GetFloat64(Realm.Pointer, _handle);
    }

    /// <summary>
    /// Converts the value to a boolean using JavaScript's ToBoolean semantics.
    /// </summary>
    /// <returns>The boolean value.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// JavaScript falsy values that return <c>false</c>:
    /// <list type="bullet">
    /// <item><c>undefined</c></item>
    /// <item><c>null</c></item>
    /// <item><c>false</c></item>
    /// <item><c>0</c>, <c>-0</c>, <c>NaN</c></item>
    /// <item>Empty string <c>""</c></item>
    /// </list>
    /// All other values return <c>true</c>.
    /// </para>
    /// </remarks>
    public bool AsBoolean()
    {
        AssertAlive();
        if (IsBoolean())
            return Realm.IsEqual(
                _handle,
                Realm.Runtime.Registry.GetTrue());

        if (IsNullOrUndefined()) return false;
        if (IsNumber())
        {
            var num = AsNumber();
            return num != 0 && !double.IsNaN(num);
        }

        if (IsString()) return AsString() != "";

        return true;
    }

    /// <summary>
    /// Converts the value to a string using JavaScript's ToString semantics.
    /// </summary>
    /// <returns>The string representation of the value.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Examples of conversion:
    /// <list type="bullet">
    /// <item><c>undefined</c> → <c>"undefined"</c></item>
    /// <item><c>null</c> → <c>"null"</c></item>
    /// <item><c>true</c> → <c>"true"</c></item>
    /// <item><c>42</c> → <c>"42"</c></item>
    /// <item>Object → <c>"[object Object]"</c> or result of toString()</item>
    /// <item>Array → comma-separated elements</item>
    /// </list>
    /// </para>
    /// </remarks>
    public string AsString()
    {
        AssertAlive();
        var strPtr = Realm.Runtime.Registry.ToCString(Realm.Pointer, _handle);
        var str = Realm.ReadString(strPtr);
        Realm.FreeCString(strPtr);
        return str;
    }

    /// <summary>
    /// Gets the BigInt value as a 64-bit integer.
    /// </summary>
    /// <returns>The BigInt value as a <c>long</c>.</returns>
    /// <exception cref="HakoException">
    /// BigInt support is not enabled in this build of QuickJS.
    /// </exception>
    /// <exception cref="InvalidOperationException">The value is not a BigInt.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// BigInt support requires QuickJS to be compiled with BigNum support.
    /// Check <c>runtime.Utils.GetBuildInfo().HasBignum</c> to verify availability.
    /// </para>
    /// <para>
    /// This method only supports BigInt values that fit in a 64-bit signed integer.
    /// Larger values will cause parsing to fail.
    /// </para>
    /// </remarks>
    public long GetBigInt()
    {
        AssertAlive();
        if (Type != JSType.BigInt) throw new InvalidOperationException("Value is not a BigInt");
        return long.Parse(AsString());
    }

    #endregion

    #region Property Access

    /// <summary>
    /// Gets a property value by name.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <returns>A <see cref="JSValue"/> representing the property value that must be disposed.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <exception cref="HakoException">An error occurred getting the property.</exception>
    /// <remarks>
    /// <para>
    /// If the property doesn't exist, returns JavaScript <c>undefined</c>.
    /// The caller is responsible for disposing the returned value.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var obj = realm.NewObject();
    /// obj.SetProperty("name", "Alice");
    /// using var name = obj.GetProperty("name");
    /// Console.WriteLine(name.AsString()); // "Alice"
    /// </code>
    /// </para>
    /// </remarks>
    public JSValue GetProperty(string key)
    {
        AssertAlive();
        using var keyStrPtr = Realm.AllocateString(key, out _);
        var keyPtr = Realm.Runtime.Registry.NewString(Realm.Pointer, keyStrPtr);
        try
        {
            var propPtr = Realm.Runtime.Registry.GetProp(Realm.Pointer, _handle, keyPtr);
            if (propPtr == 0)
            {
                var error = Realm.GetLastError();
                if (error is not null) throw new HakoException("Error getting property", error);
            }

            return new JSValue(Realm, propPtr);
        }
        finally
        {
            Realm.FreeValuePointer(keyPtr);
        }
    }

    /// <summary>
    /// Gets a property value by numeric index.
    /// </summary>
    /// <param name="index">The numeric index.</param>
    /// <returns>A <see cref="JSValue"/> representing the property value that must be disposed.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <exception cref="HakoException">An error occurred getting the property.</exception>
    /// <remarks>
    /// This is typically used for array access. If the index doesn't exist, returns <c>undefined</c>.
    /// </remarks>
    public JSValue GetProperty(int index)
    {
        AssertAlive();
        var propPtr = Realm.Runtime.Registry.GetPropNumber(Realm.Pointer, _handle, index);
        if (propPtr == 0)
        {
            var error = Realm.GetLastError();
            if (error is not null) throw new HakoException("Error getting property", error);
        }

        return new JSValue(Realm, propPtr);
    }

    /// <summary>
    /// Gets a property value using another <see cref="JSValue"/> as the key.
    /// </summary>
    /// <param name="key">The property key as a <see cref="JSValue"/> (can be string, symbol, or number).</param>
    /// <returns>A <see cref="JSValue"/> representing the property value that must be disposed.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <exception cref="HakoException">An error occurred getting the property.</exception>
    /// <remarks>
    /// This overload allows using symbols or computed property names as keys.
    /// </remarks>
    public JSValue GetProperty(JSValue key)
    {
        AssertAlive();
        var propPtr = Realm.Runtime.Registry.GetProp(Realm.Pointer, _handle, key.GetHandle());
        if (propPtr == 0)
        {
            var error = Realm.GetLastError();
            if (error is not null) throw new HakoException("Error getting property", error);
        }

        return new JSValue(Realm, propPtr);
    }

    /// <summary>
    /// Sets a property value by name.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="key">The property name.</param>
    /// <param name="value">The value to set. Can be a .NET value or a <see cref="JSValue"/>.</param>
    /// <returns><c>true</c> if the property was set successfully; <c>false</c> otherwise.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <exception cref="HakoException">An error occurred setting the property.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="value"/> is a <see cref="JSValue"/>, it's used directly.
    /// Otherwise, it's automatically converted to a JavaScript value.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var obj = realm.NewObject();
    /// obj.SetProperty("name", "Alice");
    /// obj.SetProperty("age", 30);
    /// obj.SetProperty("active", true);
    /// </code>
    /// </para>
    /// </remarks>
    public bool SetProperty<T>(string key, T value) where T : notnull
    {
        AssertAlive();

        JSValue? keyValue = null;
        JSValue? valueVm = null;
        var valueWasCreated = false;

        try
        {
            keyValue = Realm.NewValue(key);

            int valuePtr;
            if (value is JSValue vmValue)
            {
                valuePtr = vmValue.GetHandle();
            }
            else
            {
                valueVm = Realm.NewValue(value);
                valuePtr = valueVm.GetHandle();
                valueWasCreated = true;
            }

            var result = Realm.Runtime.Registry.SetProp(
                Realm.Pointer, _handle, keyValue.GetHandle(), valuePtr);

            if (result == -1)
            {
                var error = Realm.GetLastError();
                if (error is not null) throw new HakoException("Error setting property", error);
            }

            return result == 1;
        }
        finally
        {
            keyValue?.Dispose();

            if (valueWasCreated) valueVm?.Dispose();
        }
    }

    /// <summary>
    /// Sets a property value by numeric index.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="index">The numeric index.</param>
    /// <param name="value">The value to set. Can be a .NET value or a <see cref="JSValue"/>.</param>
    /// <returns><c>true</c> if the property was set successfully; <c>false</c> otherwise.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <exception cref="HakoException">An error occurred setting the property.</exception>
    /// <remarks>
    /// This is typically used for setting array elements.
    /// </remarks>
    public bool SetProperty<T>(int index, T value) where T : notnull
    {
        AssertAlive();

        JSValue? keyValue = null;
        JSValue? valueVm = null;

        try
        {
            keyValue = Realm.NewValue(index);

            int valuePtr;
            if (value is JSValue vmValue)
            {
                valuePtr = vmValue.GetHandle();
            }
            else
            {
                valueVm = Realm.NewValue(value);
                valuePtr = valueVm.GetHandle();
            }

            var result = Realm.Runtime.Registry.SetProp(
                Realm.Pointer, _handle, keyValue.GetHandle(), valuePtr);

            if (result == -1)
            {
                var error = Realm.GetLastError();
                if (error is not null) throw new HakoException("Error setting property", error);
            }

            return result == 1;
        }
        finally
        {
            keyValue?.Dispose();
            valueVm?.Dispose();
        }
    }

    #endregion

    #region Array/Object Operations

    /// <summary>
    /// Gets the length of an array.
    /// </summary>
    /// <returns>The array length as an integer.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public int GetLength()
    {
        AssertAlive();
        if (!IsArray()) throw new InvalidOperationException("Value is not an array");
        return Realm.GetLength(_handle);
    }

    /// <summary>
    /// Gets the names of an object's own properties.
    /// </summary>
    /// <param name="flags">Flags controlling which properties to enumerate. Default is enumerable string properties.</param>
    /// <returns>An enumerable of <see cref="JSValue"/> representing property names. Each must be disposed.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <exception cref="HakoException">An error occurred getting property names.</exception>
    /// <remarks>
    /// <para>
    /// This method returns property names as JavaScript values (usually strings or symbols).
    /// Each returned value must be disposed by the caller.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var obj = realm.NewObject();
    /// obj.SetProperty("name", "Alice");
    /// obj.SetProperty("age", 30);
    /// 
    /// foreach (var prop in obj.GetOwnPropertyNames())
    /// {
    ///     using (prop)
    ///     {
    ///         Console.WriteLine(prop.AsString()); // "name", "age"
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public IEnumerable<JSValue> GetOwnPropertyNames(
        PropertyEnumFlags flags = PropertyEnumFlags.String | PropertyEnumFlags.Enumerable)
    {
        AssertAlive();

        using var outPtrPtr = Realm.AllocatePointerArray(1);
        using var outLenPtr = Realm.AllocateMemory(4);

        Realm.WriteUint32(outLenPtr, 0);

        var errorPtr = Realm.Runtime.Registry.GetOwnPropertyNames(
            Realm.Pointer,
            outPtrPtr,
            outLenPtr,
            _handle,
            (int)flags);

        var error = Realm.GetLastError(errorPtr);
        if (error != null)
        {
            Realm.FreeValuePointer(errorPtr);
            throw new HakoException("Error getting property names", error);
        }

        var outLen = (int)Realm.ReadUint32(outLenPtr);
        if (outLen == 0) return Array.Empty<JSValue>();

        var outPtrsBase = Realm.ReadPointer(outPtrPtr);
        if (outPtrsBase == 0) return Array.Empty<JSValue>();

        var results = new List<JSValue>(outLen);
        try
        {
            for (var i = 0; i < outLen; i++)
            {
                var valuePtr = Realm.ReadPointerFromArray(outPtrsBase, i);
                results.Add(new JSValue(Realm, valuePtr));
            }
        }
        finally
        {
            Realm.FreeMemory(outPtrsBase);
        }

        return results;
    }

    #endregion

    #region Promise Operations

    /// <summary>
    /// Gets the current state of a Promise.
    /// </summary>
    /// <returns>The promise state (Pending, Fulfilled, or Rejected).</returns>
    /// <exception cref="InvalidOperationException">The value is not a promise.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public PromiseState GetPromiseState()
    {
        AssertAlive();
        if (!IsPromise()) throw new InvalidOperationException("Value is not a promise");

        var state = Realm.Runtime.Registry.PromiseState(Realm.Pointer, _handle);
        return state switch
        {
            0 => PromiseState.Pending,
            1 => PromiseState.Fulfilled,
            2 => PromiseState.Rejected,
            _ => PromiseState.Pending
        };
    }

    /// <summary>
    /// Gets the result of a settled Promise (fulfilled value or rejection reason).
    /// </summary>
    /// <returns>
    /// A <see cref="JSValue"/> representing the promise result that must be disposed,
    /// or <c>null</c> if the promise is still pending.
    /// </returns>
    /// <exception cref="InvalidOperationException">The value is not a promise.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// Only returns a value if the promise is fulfilled or rejected. For pending promises, returns <c>null</c>.
    /// </remarks>
    public JSValue? GetPromiseResult()
    {
        AssertAlive();
        if (!IsPromise()) throw new InvalidOperationException("Value is not a promise");

        var state = GetPromiseState();
        if (state != PromiseState.Fulfilled && state != PromiseState.Rejected) return null;

        var resultPtr = Realm.Runtime.Registry.PromiseResult(Realm.Pointer, _handle);
        return new JSValue(Realm, resultPtr);
    }

    #endregion

    #region TypedArray Operations

    /// <summary>
    /// Gets the type of a TypedArray (Uint8Array, Int32Array, Float64Array, etc.).
    /// </summary>
    /// <returns>The TypedArray type.</returns>
    /// <exception cref="HakoException">The value is not a typed array.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public TypedArrayType GetTypedArrayType()
    {
        AssertAlive();
        var typeId = Realm.Runtime.Registry.GetTypedArrayType(Realm.Pointer, _handle);
        if (typeId is -1) throw new HakoException("Value is not a typed array");
        return (TypedArrayType)typeId;
    }

    /// <summary>
    /// Copies the contents of a TypedArray to a byte array.
    /// </summary>
    /// <returns>A byte array containing a copy of the TypedArray's data.</returns>
    /// <exception cref="HakoException">The value is not a typed array or copying failed.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// This creates a managed copy of the TypedArray's underlying buffer.
    /// Changes to the returned array do not affect the original TypedArray.
    /// </remarks>
    public byte[] CopyTypedArray()
    {
        AssertAlive();
        using var pointer = Realm.AllocatePointerArray(1);

        var bufPtr = Realm.Runtime.Registry.CopyTypedArrayBuffer(Realm.Pointer, _handle, pointer);
        if (bufPtr == 0)
        {
            var error = Realm.GetLastError();
            if (error != null) throw error;
        }

        try
        {
            var length = Realm.ReadPointerFromArray(pointer, 0);
            return Realm.CopyMemory(bufPtr, length);
        }
        finally
        {
            Realm.FreeMemory(bufPtr);
        }
    }

    /// <summary>
    /// Copies the contents of an ArrayBuffer to a byte array.
    /// </summary>
    /// <returns>A byte array containing a copy of the ArrayBuffer's data.</returns>
    /// <exception cref="InvalidOperationException">The value is not an ArrayBuffer.</exception>
    /// <exception cref="HakoException">Copying failed.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    public byte[] CopyArrayBuffer()
    {
        AssertAlive();
        if (!IsArrayBuffer()) throw new InvalidOperationException("Value is not an ArrayBuffer");

        using var pointer = Realm.AllocatePointerArray(1);

        var bufPtr = Realm.Runtime.Registry.CopyArrayBuffer(Realm.Pointer, _handle, pointer);
        if (bufPtr == 0)
        {
            var error = Realm.GetLastError();
            if (error != null) throw error;
        }

        try
        {
            var length = Realm.ReadPointer(pointer);
            if (length == 0) return [];

            return Realm.CopyMemory(bufPtr, length);
        }
        finally
        {
            Realm.FreeMemory(bufPtr);
        }
    }

    #endregion

    #region Equality Operations

    /// <summary>
    /// Compares this value with another using JavaScript's strict equality (===).
    /// </summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><c>true</c> if the values are strictly equal; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// <para>
    /// Strict equality does not perform type coercion:
    /// <list type="bullet">
    /// <item><c>5 === 5</c> is <c>true</c></item>
    /// <item><c>5 === "5"</c> is <c>false</c></item>
    /// <item><c>NaN === NaN</c> is <c>false</c></item>
    /// <item><c>+0 === -0</c> is <c>true</c></item>
    /// </list>
    /// </para>
    /// </remarks>
    public bool Eq(JSValue other)
    {
        return Realm.IsEqual(_handle, other.GetHandle());
    }

    /// <summary>
    /// Compares this value with another using JavaScript's SameValue algorithm.
    /// </summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><c>true</c> if the values are the same; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// <para>
    /// SameValue is like strict equality but with two differences:
    /// <list type="bullet">
    /// <item><c>NaN</c> equals <c>NaN</c></item>
    /// <item><c>+0</c> does not equal <c>-0</c></item>
    /// </list>
    /// This is the algorithm used by <c>Object.is()</c> in JavaScript.
    /// </para>
    /// </remarks>
    public bool SameValue(JSValue other)
    {
        return Realm.IsEqual(_handle, other.GetHandle(), EqualityOp.SameValue);
    }

    /// <summary>
    /// Compares this value with another using JavaScript's SameValueZero algorithm.
    /// </summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><c>true</c> if the values are the same; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// <para>
    /// SameValueZero is like SameValue but <c>+0</c> equals <c>-0</c>.
    /// This is the algorithm used by Set, Map, and array methods like <c>includes()</c>.
    /// </para>
    /// </remarks>
    public bool SameValueZero(JSValue other)
    {
        return Realm.IsEqual(_handle, other.GetHandle(), EqualityOp.SameValueZero);
    }

    #endregion

    #region Class Operations

    /// <summary>
    /// Gets the class ID for this value if it's a class instance.
    /// </summary>
    /// <returns>The class ID as an integer, or 0 if not a class instance.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// Class IDs are unique identifiers assigned to JavaScript classes created with <see cref="JSClass"/>.
    /// They're used internally to identify and validate class instances.
    /// </remarks>
    public int ClassId()
    {
        AssertAlive();
        return Realm.Runtime.Registry.GetClassID(_handle);
    }

    /// <summary>
    /// Gets the opaque value stored with this class instance.
    /// </summary>
    /// <returns>The opaque integer value.</returns>
    /// <exception cref="HakoException">Failed to retrieve the opaque value.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Opaque values are typically used to store identifiers that link JavaScript instances
    /// to native .NET objects. Common uses include:
    /// <list type="bullet">
    /// <item>Hash codes for dictionary lookups</item>
    /// <item>Pointers to native resources</item>
    /// <item>Instance IDs for tracking objects</item>
    /// </list>
    /// </para>
    /// </remarks>
    public int GetOpaque()
    {
        AssertAlive();
        var result = Realm.Runtime.Registry.GetOpaque(Realm.Pointer, _handle, ClassId());
        var error = Realm.GetLastError(result);
        if (error != null) throw new HakoException("Unable to get opaque", error);
        return result;
    }

    /// <summary>
    /// Sets the opaque value for this class instance.
    /// </summary>
    /// <param name="opaque">The opaque integer value to store.</param>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// This should only be called on instances created with <see cref="JSClass"/>.
    /// The opaque value is typically set during instance construction.
    /// </remarks>
    public void SetOpaque(int opaque)
    {
        AssertAlive();
        Realm.Runtime.Registry.SetOpaque(_handle, opaque);
    }

    /// <summary>
    /// Checks if this value is an instance of the specified constructor.
    /// </summary>
    /// <param name="constructor">The constructor function to check against.</param>
    /// <returns><c>true</c> if this value is an instance of the constructor; otherwise, <c>false</c>.</returns>
    /// <exception cref="HakoException">The instanceof check failed.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This implements JavaScript's <c>instanceof</c> operator, checking if the constructor's
    /// prototype appears anywhere in the value's prototype chain.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var obj = realm.EvalCode("new Date()").Unwrap();
    /// using var dateConstructor = realm.EvalCode("Date").Unwrap();
    /// bool isDate = obj.InstanceOf(dateConstructor); // true
    /// </code>
    /// </para>
    /// </remarks>
    public bool InstanceOf(JSValue constructor)
    {
        AssertAlive();
        var result = Realm.Runtime.Registry.IsInstanceOf(Realm.Pointer, _handle, constructor.GetHandle());
        if (result == -1)
        {
            var error = Realm.GetLastError();
            if (error != null) throw error;
        }

        return result == 1;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Converts the value to a JSON string.
    /// </summary>
    /// <param name="indent">The number of spaces to use for indentation, or 0 for compact output.</param>
    /// <returns>A JSON string representation of the value.</returns>
    /// <exception cref="HakoException">JSON serialization failed.</exception>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This is equivalent to calling <c>JSON.stringify(value, null, indent)</c> in JavaScript.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var obj = realm.NewObject();
    /// obj.SetProperty("name", "Alice");
    /// obj.SetProperty("age", 30);
    /// 
    /// var json = obj.Stringify(2);
    /// // {
    /// //   "name": "Alice",
    /// //   "age": 30
    /// // }
    /// </code>
    /// </para>
    /// </remarks>
    public string Stringify(int indent = 0)
    {
        AssertAlive();
        var jsonPtr = Realm.Runtime.Registry.ToJson(Realm.Pointer, _handle, indent);
        var error = Realm.GetLastError(jsonPtr);
        if (error != null)
        {
            Realm.FreeValuePointer(jsonPtr);
            throw error;
        }

        using var json = new JSValue(Realm, jsonPtr);
        return json.AsString();
    }

    /// <summary>
    /// Creates a duplicate of this value with independent lifecycle.
    /// </summary>
    /// <returns>
    /// A new <see cref="JSValue"/> that must be disposed independently.
    /// If this value is borrowed, returns the same value without duplication.
    /// </returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Use this when you need a value that outlives the current scope or when you need
    /// to store a value beyond the lifetime of its original reference.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// JSValue StoreName(Realm realm)
    /// {
    ///     using var obj = realm.NewObject();
    ///     obj.SetProperty("name", "Alice");
    ///     using var name = obj.GetProperty("name");
    ///     return name.Dup(); // Duplicate so it outlives the using blocks
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public JSValue Dup()
    {
        if (lifecycle is ValueLifecycle.Borrowed) return this;
        AssertAlive();
        return Realm.DupValue(_handle);
    }

    /// <summary>
    /// Creates a borrowed reference to this value that doesn't own the underlying handle.
    /// </summary>
    /// <returns>A borrowed <see cref="JSValue"/> that won't free the handle on disposal.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Borrowed references are useful when you need to pass a value temporarily without
    /// transferring ownership. The borrowed value becomes invalid when the original value is disposed.
    /// </para>
    /// <para>
    /// Warning: Be careful not to use a borrowed value after its original has been disposed.
    /// </para>
    /// </remarks>
    public JSValue Borrow()
    {
        AssertAlive();
        return new JSValue(Realm, _handle, ValueLifecycle.Borrowed);
    }

    /// <summary>
    /// Consumes this value by passing it to a function and then disposing it.
    /// </summary>
    /// <typeparam name="T">The return type of the consumer function.</typeparam>
    /// <param name="consumer">A function that processes the value and returns a result.</param>
    /// <returns>The result returned by the consumer function.</returns>
    /// <exception cref="HakoUseAfterFreeException">The value has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This is a convenience method for processing a value and ensuring it's disposed afterward,
    /// similar to a using statement but returning a value.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var length = realm.EvalCode("[1, 2, 3]").Unwrap()
    ///     .Consume(arr => arr.GetLength()); // arr is disposed after this
    /// </code>
    /// </para>
    /// </remarks>
    public T Consume<T>(Func<JSValue, T> consumer)
    {
        AssertAlive();
        try
        {
            return consumer(this);
        }
        finally
        {
            Dispose();
        }
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Detaches the underlying handle from this <see cref="JSValue"/>, returning it without disposal.
    /// </summary>
    /// <returns>The raw handle value.</returns>
    /// <exception cref="ObjectDisposedException">The value has already been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This is an advanced method that transfers ownership of the handle to the caller.
    /// The caller becomes responsible for freeing the handle using <see cref="Realm.FreeValuePointer"/>.
    /// </para>
    /// <para>
    /// After calling this method, the <see cref="JSValue"/> is marked as disposed and cannot be used.
    /// </para>
    /// </remarks>
    public int DetachHandle()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(JSValue));

        var handle = _handle;
        _disposed = true;
        return handle;
    }

    /// <summary>
    /// Checks if this value is <c>null</c> or <c>undefined</c>.
    /// </summary>
    /// <returns><c>true</c> if the value is <c>null</c> or <c>undefined</c>; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This is a convenience method equivalent to <c>IsNull() || IsUndefined()</c>.
    /// </remarks>
    public bool IsNullOrUndefined()
    {
        return Realm.Runtime.Registry.IsNullOrUndefined(_handle) != 0;
    }

    /// <summary>
    /// Disposes the value, releasing its underlying QuickJS reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After disposal, the value cannot be used. Attempting to use a disposed value
    /// will throw <see cref="HakoUseAfterFreeException"/>.
    /// </para>
    /// <para>
    /// Values with <see cref="ValueLifecycle.Borrowed"/> lifecycle don't free their
    /// handle on disposal since they don't own it.
    /// </para>
    /// <para>
    /// It's safe to call <see cref="Dispose"/> multiple times; subsequent calls have no effect.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        if (_handle != 0)
        {
            if (lifecycle != ValueLifecycle.Borrowed) Realm.FreeValuePointer(_handle);
            _handle = 0;
        }

        _disposed = true;
    }

    private void AssertAlive()
    {
        if (!Alive) throw new HakoUseAfterFreeException("JSValue", _handle);
    }

    #endregion
}