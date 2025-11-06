using System.Runtime.CompilerServices;
using HakoJS.VM;
using HakoJS.SourceGeneration;

namespace HakoJS.Extensions;

/// <summary>
/// Provides extension methods for converting C# collections to JavaScript arrays.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Converts a C# array to a JavaScript array of primitive types.
    /// </summary>
    /// <typeparam name="T">The primitive element type (string, bool, int, long, double, float, etc.).</typeparam>
    /// <param name="array">The C# array to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    /// <exception cref="NotSupportedException">The element type is not a supported primitive type.</exception>
    /// <remarks>
    /// <para>
    /// This method handles primitive types only. For custom types implementing <see cref="IJSMarshalable{T}"/>,
    /// use <see cref="ToJSArrayOf{T}"/> instead.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// int[] numbers = { 1, 2, 3, 4, 5 };
    /// using var jsArray = numbers.ToJSArray(realm);
    /// 
    /// string[] strings = { "hello", "world" };
    /// using var jsStrings = strings.ToJSArray(realm);
    /// </code>
    /// </para>
    /// </remarks>
    public static JSValue ToJSArray<T>(this T[] array, Realm realm)
    {
        var jsArray = realm.NewArray();
        
        try
        {
            for (int i = 0; i < array.Length; i++)
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
    /// Converts a C# List to a JavaScript array of primitive types.
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
            for (int i = 0; i < list.Count; i++)
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
    /// Converts a C# IEnumerable to a JavaScript array of primitive types.
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
            int i = 0;
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
    /// Converts a C# ReadOnlySpan to a JavaScript array of primitive types.
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
            for (int i = 0; i < span.Length; i++)
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
    /// Converts a C# array to a JavaScript array of types implementing <see cref="IJSMarshalable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type that implements <see cref="IJSMarshalable{T}"/>.</typeparam>
    /// <param name="array">The C# array to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    /// <remarks>
    /// <para>
    /// This method is specifically for custom types decorated with [JSObject] or [JSClass] that implement
    /// the <see cref="IJSMarshalable{T}"/> interface through source generation.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// [JSObject]
    /// partial record Point(double X, double Y);
    /// 
    /// Point[] points = { new(1, 2), new(3, 4), new(5, 6) };
    /// using var jsArray = points.ToJSArrayOf(realm);
    /// </code>
    /// </para>
    /// </remarks>
    public static JSValue ToJSArrayOf<T>(this T[] array, Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();
        
        try
        {
            for (int i = 0; i < array.Length; i++)
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
    /// Converts a C# List to a JavaScript array of types implementing <see cref="IJSMarshalable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type that implements <see cref="IJSMarshalable{T}"/>.</typeparam>
    /// <param name="list">The C# List to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    public static JSValue ToJSArrayOf<T>(this List<T> list, Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();
        
        try
        {
            for (int i = 0; i < list.Count; i++)
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
    /// Converts a C# IEnumerable to a JavaScript array of types implementing <see cref="IJSMarshalable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type that implements <see cref="IJSMarshalable{T}"/>.</typeparam>
    /// <param name="enumerable">The C# IEnumerable to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    public static JSValue ToJSArrayOf<T>(this IEnumerable<T> enumerable, Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();
        
        try
        {
            int i = 0;
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
    /// Converts a C# ReadOnlySpan to a JavaScript array of types implementing <see cref="IJSMarshalable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type that implements <see cref="IJSMarshalable{T}"/>.</typeparam>
    /// <param name="span">The C# ReadOnlySpan to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript array.</param>
    /// <returns>A JavaScript array containing the converted elements.</returns>
    public static JSValue ToJSArrayOf<T>(this ReadOnlySpan<T> span, Realm realm) where T : IJSMarshalable<T>
    {
        var jsArray = realm.NewArray();
        
        try
        {
            for (int i = 0; i < span.Length; i++)
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
            return realm.NewString(stringValue ?? string.Empty);
        }
        else if (elementType == typeof(bool))
        {
            var boolValue = Unsafe.As<T, bool>(ref value);
            return boolValue ? realm.True() : realm.False();
        }
        else if (elementType == typeof(int))
        {
            var intValue = Unsafe.As<T, int>(ref value);
            return realm.NewNumber(intValue);
        }
        else if (elementType == typeof(long))
        {
            var longValue = Unsafe.As<T, long>(ref value);
            return realm.NewNumber(longValue);
        }
        else if (elementType == typeof(double))
        {
            var doubleValue = Unsafe.As<T, double>(ref value);
            return realm.NewNumber(double.IsNaN(doubleValue) || double.IsInfinity(doubleValue) ? 0.0 : doubleValue);
        }
        else if (elementType == typeof(float))
        {
            var floatValue = Unsafe.As<T, float>(ref value);
            return realm.NewNumber(float.IsNaN(floatValue) || float.IsInfinity(floatValue) ? 0.0 : floatValue);
        }
        else if (elementType == typeof(short))
        {
            var shortValue = Unsafe.As<T, short>(ref value);
            return realm.NewNumber(shortValue);
        }
        else if (elementType == typeof(byte))
        {
            var byteValue = Unsafe.As<T, byte>(ref value);
            return realm.NewNumber(byteValue);
        }
        else if (elementType == typeof(sbyte))
        {
            var sbyteValue = Unsafe.As<T, sbyte>(ref value);
            return realm.NewNumber(sbyteValue);
        }
        else if (elementType == typeof(uint))
        {
            var uintValue = Unsafe.As<T, uint>(ref value);
            return realm.NewNumber(uintValue);
        }
        else if (elementType == typeof(ulong))
        {
            var ulongValue = Unsafe.As<T, ulong>(ref value);
            return realm.NewNumber(ulongValue);
        }
        else if (elementType == typeof(ushort))
        {
            var ushortValue = Unsafe.As<T, ushort>(ref value);
            return realm.NewNumber(ushortValue);
        }
        else
        {
            throw new NotSupportedException($"Array element type {elementType.Name} is not supported. Only primitive types (string, bool, int, long, float, double, etc.) are supported. Use ToJSArrayOf<T>() for custom types implementing IJSMarshalable<T>.");
        }
    }
}