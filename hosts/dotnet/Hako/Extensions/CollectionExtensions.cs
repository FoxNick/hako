using System.Runtime.CompilerServices;
using HakoJS.SourceGeneration;
using HakoJS.VM;

namespace HakoJS.Extensions;

/// <summary>
///     Provides extension methods for converting C# collections to JavaScript arrays.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    ///     Converts a C# array to a JavaScript array of primitive types.
    /// </summary>
    /// <typeparam name="T">The primitive element type (string, bool, int, long, double, float, etc.).</typeparam>
    /// <param name="array">The C# array to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    /// <exception cref="NotSupportedException">The element type is not a supported primitive type.</exception>
    /// <remarks>
    ///     <para>
    ///         This method handles primitive types only. For custom types implementing <see cref="IJSMarshalable{T}" />,
    ///         use <see cref="ToJSArrayOf{T}" /> instead.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// int[] numbers = { 1, 2, 3, 4, 5 };
    /// using var jsArray = numbers.ToJSArray(realm);
    /// 
    /// string[] strings = { "hello", "world" };
    /// using var jsStrings = strings.ToJSArray(realm);
    /// </code>
    ///     </para>
    /// </remarks>
    public static JSValue ToJSArray<T>(this T[] array, Realm realm)
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < array.Length; i++)
            {
                using var jsElement = MarshalPrimitive(array[i], realm);
                jsArray.SetProperty(i, jsElement);
            }

            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# List to a JavaScript array of primitive types.
    /// </summary>
    /// <typeparam name="T">The primitive element type (string, bool, int, long, double, float, etc.).</typeparam>
    /// <param name="list">The C# List to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    /// <exception cref="NotSupportedException">The element type is not a supported primitive type.</exception>
    public static JSValue ToJSArray<T>(this List<T> list, Realm realm)
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < list.Count; i++)
            {
                using var jsElement = MarshalPrimitive(list[i], realm);
                jsArray.SetProperty(i, jsElement);
            }

            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# IEnumerable to a JavaScript array of primitive types.
    /// </summary>
    /// <typeparam name="T">The primitive element type (string, bool, int, long, double, float, etc.).</typeparam>
    /// <param name="enumerable">The C# IEnumerable to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    /// <exception cref="NotSupportedException">The element type is not a supported primitive type.</exception>
    public static JSValue ToJSArray<T>(this IEnumerable<T> enumerable, Realm realm)
    {
        var jsArray = realm.NewArray();

        try
        {
            var i = 0;
            foreach (var item in enumerable)
            {
                using var jsElement = MarshalPrimitive(item, realm);
                jsArray.SetProperty(i++, jsElement);
            }

            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# ReadOnlySpan to a JavaScript array of primitive types.
    /// </summary>
    /// <typeparam name="T">The primitive element type (string, bool, int, long, double, float, etc.).</typeparam>
    /// <param name="span">The C# ReadOnlySpan to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    /// <exception cref="NotSupportedException">The element type is not a supported primitive type.</exception>
    public static JSValue ToJSArray<T>(this ReadOnlySpan<T> span, Realm realm)
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < span.Length; i++)
            {
                using var jsElement = MarshalPrimitive(span[i], realm);
                jsArray.SetProperty(i, jsElement);
            }

            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# array to a JavaScript array of types implementing <see cref="IJSMarshalable{T}" />.
    /// </summary>
    /// <typeparam name="T">The element type that implements <see cref="IJSMarshalable{T}" />.</typeparam>
    /// <param name="array">The C# array to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    /// <remarks>
    ///     <para>
    ///         This method is specifically for custom types decorated with [JSObject] or [JSClass] that implement
    ///         the <see cref="IJSMarshalable{T}" /> interface through source generation.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// [JSObject]
    /// partial record Point(double X, double Y);
    /// 
    /// Point[] points = { new(1, 2), new(3, 4), new(5, 6) };
    /// using var jsArray = points.ToJSArrayOf(realm);
    /// </code>
    ///     </para>
    /// </remarks>
    public static JSValue ToJSArrayOf<T>(this T[] array, Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < array.Length; i++)
            {
                using var jsElement = array[i].ToJSValue(realm);
                jsArray.SetProperty(i, jsElement);
            }

            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# List to a JavaScript array of types implementing <see cref="IJSMarshalable{T}" />.
    /// </summary>
    /// <typeparam name="T">The element type that implements <see cref="IJSMarshalable{T}" />.</typeparam>
    /// <param name="list">The C# List to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    public static JSValue ToJSArrayOf<T>(this List<T> list, Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < list.Count; i++)
            {
                using var jsElement = list[i].ToJSValue(realm);
                jsArray.SetProperty(i, jsElement);
            }

            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# IEnumerable to a JavaScript array of types implementing <see cref="IJSMarshalable{T}" />.
    /// </summary>
    /// <typeparam name="T">The element type that implements <see cref="IJSMarshalable{T}" />.</typeparam>
    /// <param name="enumerable">The C# IEnumerable to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    public static JSValue ToJSArrayOf<T>(this IEnumerable<T> enumerable, Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();

        try
        {
            var i = 0;
            foreach (var item in enumerable)
            {
                using var jsElement = item.ToJSValue(realm);
                jsArray.SetProperty(i++, jsElement);
            }

            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# ReadOnlySpan to a JavaScript array of types implementing <see cref="IJSMarshalable{T}" />.
    /// </summary>
    /// <typeparam name="T">The element type that implements <see cref="IJSMarshalable{T}" />.</typeparam>
    /// <param name="span">The C# ReadOnlySpan to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    public static JSValue ToJSArrayOf<T>(this ReadOnlySpan<T> span, Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < span.Length; i++)
            {
                using var jsElement = span[i].ToJSValue(realm);
                jsArray.SetProperty(i, jsElement);
            }

            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    private static JSValue MarshalPrimitive<T>(T value, Realm realm)
    {
        var elementType = typeof(T);

        if (elementType == typeof(string))
        {
            var stringValue = Unsafe.As<T, string>(ref value);
            return realm.NewString(stringValue);
        }

        if (elementType == typeof(bool))
        {
            var boolValue = Unsafe.As<T, bool>(ref value);
            return boolValue ? realm.True() : realm.False();
        }

        if (elementType == typeof(int))
        {
            var intValue = Unsafe.As<T, int>(ref value);
            return realm.NewNumber(intValue);
        }

        if (elementType == typeof(long))
        {
            var longValue = Unsafe.As<T, long>(ref value);
            return realm.NewNumber(longValue);
        }

        if (elementType == typeof(double))
        {
            var doubleValue = Unsafe.As<T, double>(ref value);
            return realm.NewNumber(double.IsNaN(doubleValue) || double.IsInfinity(doubleValue) ? 0.0 : doubleValue);
        }

        if (elementType == typeof(float))
        {
            var floatValue = Unsafe.As<T, float>(ref value);
            return realm.NewNumber(float.IsNaN(floatValue) || float.IsInfinity(floatValue) ? 0.0 : floatValue);
        }

        if (elementType == typeof(short))
        {
            var shortValue = Unsafe.As<T, short>(ref value);
            return realm.NewNumber(shortValue);
        }

        if (elementType == typeof(byte))
        {
            var byteValue = Unsafe.As<T, byte>(ref value);
            return realm.NewNumber(byteValue);
        }

        if (elementType == typeof(sbyte))
        {
            var sbyteValue = Unsafe.As<T, sbyte>(ref value);
            return realm.NewNumber(sbyteValue);
        }

        if (elementType == typeof(uint))
        {
            var uintValue = Unsafe.As<T, uint>(ref value);
            return realm.NewNumber(uintValue);
        }

        if (elementType == typeof(ulong))
        {
            var ulongValue = Unsafe.As<T, ulong>(ref value);
            return realm.NewNumber(ulongValue);
        }

        if (elementType == typeof(ushort))
        {
            var ushortValue = Unsafe.As<T, ushort>(ref value);
            return realm.NewNumber(ushortValue);
        }

        if (elementType == typeof(DateTime))
        {
            var dateTimeValue = Unsafe.As<T, DateTime>(ref value);
            return realm.NewDate(dateTimeValue);
        }
        throw new NotSupportedException(
            $"Array element type {elementType.Name} is not supported. Only primitive types (string, bool, int, long, float, double, etc.) are supported. Use ToJSArrayOf<T>() for custom types implementing IJSMarshalable<T>.");
    }

    /// <summary>
    ///     Converts a C# IReadOnlyList to a frozen JavaScript array of primitive types.
    /// </summary>
    public static JSValue ToJSArray<T>(this IReadOnlyList<T> list, Realm realm)
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < list.Count; i++)
            {
                using var jsElement = MarshalPrimitive(list[i], realm);
                jsArray.SetProperty(i, jsElement);
            }

            jsArray.Freeze(realm);
            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# IReadOnlyCollection to a frozen JavaScript array of primitive types.
    /// </summary>
    public static JSValue ToJSArray<T>(this IReadOnlyCollection<T> collection, Realm realm)
    {
        var jsArray = realm.NewArray();

        try
        {
            var i = 0;
            foreach (var item in collection)
            {
                using var jsElement = MarshalPrimitive(item, realm);
                jsArray.SetProperty(i++, jsElement);
            }

            jsArray.Freeze(realm);
            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# ReadOnlyCollection to a frozen JavaScript array of primitive types.
    /// </summary>
    public static JSValue ToJSArray<T>(this System.Collections.ObjectModel.ReadOnlyCollection<T> collection,
        Realm realm)
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < collection.Count; i++)
            {
                using var jsElement = MarshalPrimitive(collection[i], realm);
                jsArray.SetProperty(i, jsElement);
            }

            jsArray.Freeze(realm);
            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# IReadOnlyList to a frozen JavaScript array of IJSMarshalable types.
    /// </summary>
    public static JSValue ToJSArrayOf<T>(this IReadOnlyList<T> list, Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < list.Count; i++)
            {
                using var jsElement = list[i].ToJSValue(realm);
                jsArray.SetProperty(i, jsElement);
            }

            jsArray.Freeze(realm);
            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# IReadOnlyCollection to a frozen JavaScript array of IJSMarshalable types.
    /// </summary>
    public static JSValue ToJSArrayOf<T>(this IReadOnlyCollection<T> collection, Realm realm)
        where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();

        try
        {
            var i = 0;
            foreach (var item in collection)
            {
                using var jsElement = item.ToJSValue(realm);
                jsArray.SetProperty(i++, jsElement);
            }

            jsArray.Freeze(realm);
            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# ReadOnlyCollection to a frozen JavaScript array of IJSMarshalable types.
    /// </summary>
    public static JSValue ToJSArrayOf<T>(this System.Collections.ObjectModel.ReadOnlyCollection<T> collection,
        Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();

        try
        {
            for (var i = 0; i < collection.Count; i++)
            {
                using var jsElement = collection[i].ToJSValue(realm);
                jsArray.SetProperty(i, jsElement);
            }

            jsArray.Freeze(realm);
            return jsArray;
        }
        catch
        {
            jsArray.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# IReadOnlyDictionary to a frozen JavaScript object (primitives only).
    /// </summary>
    public static JSValue ToJSDictionary<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, Realm realm)
        where TValue : notnull where TKey : notnull
    {
        var jsObject = realm.NewObject();

        try
        {
            foreach (var kvp in dictionary)
            {
                switch (kvp.Key)
                {
                    case string strKey:
                        jsObject.SetProperty(strKey, kvp.Value);
                        break;
                    case int intKey:
                        jsObject.SetProperty(intKey, kvp.Value);
                        break;
                    case long longKey:
                        jsObject.SetProperty(longKey, kvp.Value);
                        break;
                    case short shortKey:
                        jsObject.SetProperty(shortKey, kvp.Value);
                        break;
                    case byte byteKey:
                        jsObject.SetProperty(byteKey, kvp.Value);
                        break;
                    case uint uintKey:
                        jsObject.SetProperty(uintKey, kvp.Value);
                        break;
                    case ulong ulongKey:
                        jsObject.SetProperty(ulongKey, kvp.Value);
                        break;
                    case ushort ushortKey:
                        jsObject.SetProperty(ushortKey, kvp.Value);
                        break;
                    case sbyte sbyteKey:
                        jsObject.SetProperty(sbyteKey, kvp.Value);
                        break;
                    case double doubleKey:
                        jsObject.SetProperty(doubleKey, kvp.Value);
                        break;
                    case float floatKey:
                        jsObject.SetProperty(floatKey, kvp.Value);
                        break;
                    case decimal decimalKey:
                        jsObject.SetProperty(decimalKey, kvp.Value);
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Dictionary key type {typeof(TKey).Name} is not supported. Only string and numeric keys are supported.");
                }
            }

            jsObject.Freeze(realm);
            return jsObject;
        }
        catch
        {
            jsObject.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# IReadOnlyDictionary to a frozen JavaScript object (IJSMarshalable types).
    /// </summary>
    public static JSValue ToJSDictionaryOf<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, Realm realm)
        where TValue : IJSMarshalable<TValue> where TKey : notnull
    {
        var jsObject = realm.NewObject();

        try
        {
            foreach (var kvp in dictionary)
            {
                using var jsValue = kvp.Value.ToJSValue(realm);

                switch (kvp.Key)
                {
                    case string strKey:
                        jsObject.SetProperty(strKey, jsValue);
                        break;
                    case int intKey:
                        jsObject.SetProperty(intKey, jsValue);
                        break;
                    case long longKey:
                        jsObject.SetProperty(longKey, jsValue);
                        break;
                    case short shortKey:
                        jsObject.SetProperty(shortKey, jsValue);
                        break;
                    case byte byteKey:
                        jsObject.SetProperty(byteKey, jsValue);
                        break;
                    case uint uintKey:
                        jsObject.SetProperty(uintKey, jsValue);
                        break;
                    case ulong ulongKey:
                        jsObject.SetProperty(ulongKey, jsValue);
                        break;
                    case ushort ushortKey:
                        jsObject.SetProperty(ushortKey, jsValue);
                        break;
                    case sbyte sbyteKey:
                        jsObject.SetProperty(sbyteKey, jsValue);
                        break;
                    case double doubleKey:
                        jsObject.SetProperty(doubleKey, jsValue);
                        break;
                    case float floatKey:
                        jsObject.SetProperty(floatKey, jsValue);
                        break;
                    case decimal decimalKey:
                        jsObject.SetProperty(decimalKey, jsValue);
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Dictionary key type {typeof(TKey).Name} is not supported. Only string and numeric keys are supported.");
                }
            }

            jsObject.Freeze(realm);
            return jsObject;
        }
        catch
        {
            jsObject.Dispose();
            throw;
        }
    }

    #region Dictionary Conversions

    /// <summary>
    ///     Converts a C# Dictionary to a JavaScript object (primitives only).
    /// </summary>
    public static JSValue ToJSDictionary<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, Realm realm)
        where TValue : notnull where TKey : notnull
    {
        var jsObject = realm.NewObject();

        try
        {
            foreach (var kvp in dictionary)
                switch (kvp.Key)
                {
                    case string strKey:
                        jsObject.SetProperty(strKey, kvp.Value);
                        break;
                    case int intKey:
                        jsObject.SetProperty(intKey, kvp.Value);
                        break;
                    case long longKey:
                        jsObject.SetProperty(longKey, kvp.Value);
                        break;
                    case short shortKey:
                        jsObject.SetProperty(shortKey, kvp.Value);
                        break;
                    case byte byteKey:
                        jsObject.SetProperty(byteKey, kvp.Value);
                        break;
                    case uint uintKey:
                        jsObject.SetProperty(uintKey, kvp.Value);
                        break;
                    case ulong ulongKey:
                        jsObject.SetProperty(ulongKey, kvp.Value);
                        break;
                    case ushort ushortKey:
                        jsObject.SetProperty(ushortKey, kvp.Value);
                        break;
                    case sbyte sbyteKey:
                        jsObject.SetProperty(sbyteKey, kvp.Value);
                        break;
                    case double doubleKey:
                        jsObject.SetProperty(doubleKey, kvp.Value);
                        break;
                    case float floatKey:
                        jsObject.SetProperty(floatKey, kvp.Value);
                        break;
                    case decimal decimalKey:
                        jsObject.SetProperty(decimalKey, kvp.Value);
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Dictionary key type {typeof(TKey).Name} is not supported. Only string and numeric keys are supported.");
                }

            return jsObject;
        }
        catch
        {
            jsObject.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# Dictionary to a JavaScript object (IJSMarshalable types).
    /// </summary>
    public static JSValue ToJSDictionaryOf<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, Realm realm)
        where TValue : IJSMarshalable<TValue> where TKey : notnull
    {
        var jsObject = realm.NewObject();

        try
        {
            foreach (var kvp in dictionary)
            {
                using var jsValue = kvp.Value.ToJSValue(realm);

                switch (kvp.Key)
                {
                    case string strKey:
                        jsObject.SetProperty(strKey, jsValue);
                        break;
                    case int intKey:
                        jsObject.SetProperty(intKey, jsValue);
                        break;
                    case long longKey:
                        jsObject.SetProperty(longKey, jsValue);
                        break;
                    case short shortKey:
                        jsObject.SetProperty(shortKey, jsValue);
                        break;
                    case byte byteKey:
                        jsObject.SetProperty(byteKey, jsValue);
                        break;
                    case uint uintKey:
                        jsObject.SetProperty(uintKey, jsValue);
                        break;
                    case ulong ulongKey:
                        jsObject.SetProperty(ulongKey, jsValue);
                        break;
                    case ushort ushortKey:
                        jsObject.SetProperty(ushortKey, jsValue);
                        break;
                    case sbyte sbyteKey:
                        jsObject.SetProperty(sbyteKey, jsValue);
                        break;
                    case double doubleKey:
                        jsObject.SetProperty(doubleKey, jsValue);
                        break;
                    case float floatKey:
                        jsObject.SetProperty(floatKey, jsValue);
                        break;
                    case decimal decimalKey:
                        jsObject.SetProperty(decimalKey, jsValue);
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Dictionary key type {typeof(TKey).Name} is not supported. Only string and numeric keys are supported.");
                }
            }

            return jsObject;
        }
        catch
        {
            jsObject.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# IDictionary to a JavaScript object (primitives only).
    /// </summary>
    public static JSValue ToJSDictionary<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Realm realm)
        where TValue : notnull
    {
        var jsObject = realm.NewObject();

        try
        {
            foreach (var kvp in dictionary)
                switch (kvp.Key)
                {
                    case string strKey:
                        jsObject.SetProperty(strKey, kvp.Value);
                        break;
                    case int intKey:
                        jsObject.SetProperty(intKey, kvp.Value);
                        break;
                    case long longKey:
                        jsObject.SetProperty(longKey, kvp.Value);
                        break;
                    case short shortKey:
                        jsObject.SetProperty(shortKey, kvp.Value);
                        break;
                    case byte byteKey:
                        jsObject.SetProperty(byteKey, kvp.Value);
                        break;
                    case uint uintKey:
                        jsObject.SetProperty(uintKey, kvp.Value);
                        break;
                    case ulong ulongKey:
                        jsObject.SetProperty(ulongKey, kvp.Value);
                        break;
                    case ushort ushortKey:
                        jsObject.SetProperty(ushortKey, kvp.Value);
                        break;
                    case sbyte sbyteKey:
                        jsObject.SetProperty(sbyteKey, kvp.Value);
                        break;
                    case double doubleKey:
                        jsObject.SetProperty(doubleKey, kvp.Value);
                        break;
                    case float floatKey:
                        jsObject.SetProperty(floatKey, kvp.Value);
                        break;
                    case decimal decimalKey:
                        jsObject.SetProperty(decimalKey, kvp.Value);
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Dictionary key type {typeof(TKey).Name} is not supported. Only string and numeric keys are supported.");
                }

            return jsObject;
        }
        catch
        {
            jsObject.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Converts a C# IDictionary to a JavaScript object (IJSMarshalable types).
    /// </summary>
    public static JSValue ToJSDictionaryOf<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, Realm realm)
        where TValue : IJSMarshalable<TValue>
    {
        var jsObject = realm.NewObject();

        try
        {
            foreach (var kvp in dictionary)
            {
                using var jsValue = kvp.Value.ToJSValue(realm);

                if (kvp.Key is string strKey)
                    jsObject.SetProperty(strKey, jsValue);
                else if (kvp.Key is int intKey)
                    jsObject.SetProperty(intKey, jsValue);
                else if (kvp.Key is long longKey)
                    jsObject.SetProperty(longKey, jsValue);
                else if (kvp.Key is short shortKey)
                    jsObject.SetProperty(shortKey, jsValue);
                else if (kvp.Key is byte byteKey)
                    jsObject.SetProperty(byteKey, jsValue);
                else if (kvp.Key is uint uintKey)
                    jsObject.SetProperty(uintKey, jsValue);
                else if (kvp.Key is ulong ulongKey)
                    jsObject.SetProperty(ulongKey, jsValue);
                else if (kvp.Key is ushort ushortKey)
                    jsObject.SetProperty(ushortKey, jsValue);
                else if (kvp.Key is sbyte sbyteKey)
                    jsObject.SetProperty(sbyteKey, jsValue);
                else if (kvp.Key is double doubleKey)
                    jsObject.SetProperty(doubleKey, jsValue);
                else if (kvp.Key is float floatKey)
                    jsObject.SetProperty(floatKey, jsValue);
                else if (kvp.Key is decimal decimalKey)
                    jsObject.SetProperty(decimalKey, jsValue);
                else
                    throw new NotSupportedException(
                        $"Dictionary key type {typeof(TKey).Name} is not supported. Only string and numeric keys are supported.");
            }

            return jsObject;
        }
        catch
        {
            jsObject.Dispose();
            throw;
        }
    }

    #endregion
}