using System.Runtime.CompilerServices;
using HakoJS.Exceptions;
using HakoJS.Lifetime;
using HakoJS.SourceGeneration;
using HakoJS.VM;

namespace HakoJS.Extensions;

/// <summary>
///     Provides extension methods for <see cref="JSValue" /> to simplify common JavaScript operations.
/// </summary>
public static class JSValueExtensions
{
    /// <summary>
    ///     Gets the name of a JavaScript type as a string.
    /// </summary>
    /// <param name="type">The JavaScript type.</param>
    /// <returns>The string representation of the type (e.g., "undefined", "object", "string").</returns>
    /// <exception cref="ArgumentOutOfRangeException">The type is not a valid <see cref="JSType" />.</exception>
    public static string Name(this JSType type)
    {
        return type switch
        {
            JSType.Undefined => "undefined",
            JSType.Object => "object",
            JSType.String => "string",
            JSType.Symbol => "symbol",
            JSType.Boolean => "boolean",
            JSType.Number => "number",
            JSType.BigInt => "bigint",
            JSType.Function => "function",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    /// <summary>
    ///     Gets a property value and converts it to a .NET type, or returns a default value if undefined.
    /// </summary>
    /// <typeparam name="T">The .NET type to convert the property to.</typeparam>
    /// <param name="value">The JavaScript object.</param>
    /// <param name="key">The property key.</param>
    /// <param name="defaultValue">The default value to return if the property is undefined.</param>
    /// <returns>The property value converted to type <typeparamref name="T" />, or the default value.</returns>
    public static T GetPropertyOrDefault<T>(this JSValue value, string key, T defaultValue = default!)
    {
        using var prop = value.GetProperty(key);
        if (prop.IsUndefined()) return defaultValue;
        using var native = prop.ToNativeValue<T>();
        return native.Value;
    }

    /// <summary>
    ///     Gets an indexed property value and converts it to a .NET type, or returns a default value if undefined.
    /// </summary>
    /// <typeparam name="T">The .NET type to convert the property to.</typeparam>
    /// <param name="value">The JavaScript array or object.</param>
    /// <param name="index">The numeric index.</param>
    /// <param name="defaultValue">The default value to return if the property is undefined.</param>
    /// <returns>The property value converted to type <typeparamref name="T" />, or the default value.</returns>
    public static T GetPropertyOrDefault<T>(this JSValue value, int index, T defaultValue = default!)
    {
        using var prop = value.GetProperty(index);
        if (prop.IsUndefined()) return defaultValue;
        using var native = prop.ToNativeValue<T>();
        return native.Value;
    }

    /// <summary>
    ///     Attempts to get a property value and convert it to a .NET type.
    /// </summary>
    /// <typeparam name="T">The .NET type to convert the property to.</typeparam>
    /// <param name="value">The JavaScript object.</param>
    /// <param name="key">The property key.</param>
    /// <returns>
    ///     A <see cref="NativeBox{T}" /> containing the converted value if the property exists,
    ///     or <c>null</c> if the property is undefined.
    /// </returns>
    public static NativeBox<T>? TryGetProperty<T>(this JSValue value, string key)
    {
        using var prop = value.GetProperty(key);
        if (prop.IsUndefined()) return null;
        return prop.ToNativeValue<T>();
    }

    /// <summary>
    ///     Converts a JavaScript value to a C# instance for types implementing <see cref="IJSBindable{T}" />.
    /// </summary>
    /// <typeparam name="T">The C# type decorated with [JSClass].</typeparam>
    /// <param name="jsValue">The JavaScript value to convert.</param>
    /// <returns>The C# instance wrapped by the JavaScript value, or <c>null</c> if invalid.</returns>
    /// <remarks>
    ///     This extension method works with source-generated types that have the [JSClass] attribute.
    /// </remarks>
    public static T? ToInstance<T>(this JSValue jsValue) where T : class, IJSBindable<T>
    {
        return T.GetInstanceFromJS(jsValue);
    }

    /// <summary>
    ///     Converts a C# instance to a JavaScript value for types implementing <see cref="IJSMarshalable{T}" />.
    /// </summary>
    /// <typeparam name="T">The C# type that implements <see cref="IJSMarshalable{T}" />.</typeparam>
    /// <param name="instance">The C# instance to convert.</param>
    /// <param name="realm">The realm in which to create the JavaScript value.</param>
    /// <returns>A JavaScript value wrapping the C# instance.</returns>
    /// <remarks>
    ///     This extension method works with source-generated types that have the [JSClass] attribute.
    ///     The class must already be registered with the realm via <c>RegisterClass</c> or <c>CreatePrototype</c>.
    /// </remarks>
    public static JSValue ToJSValue<T>(this T instance, Realm realm)
        where T : IJSMarshalable<T>
    {
        return instance.ToJSValue(realm);
    }

    /// <summary>
    ///     Removes a C# instance from the JavaScript binding tracking system.
    /// </summary>
    /// <typeparam name="T">The C# type decorated with [JSClass].</typeparam>
    /// <param name="jsValue">The JavaScript value wrapping the instance.</param>
    /// <returns><c>true</c> if the instance was removed; otherwise, <c>false</c>.</returns>
    /// <remarks>
    ///     This is an advanced method typically used for manual lifetime management.
    ///     Most users should rely on automatic cleanup via finalizers.
    /// </remarks>
    public static bool RemoveInstance<T>(this JSValue jsValue) where T : class, IJSBindable<T>
    {
        return T.RemoveInstance(jsValue);
    }

    /// <summary>
    ///     Binds a 'this' context to a JavaScript function, returning a bound function wrapper.
    /// </summary>
    /// <param name="jsValue">The JavaScript function to bind.</param>
    /// <param name="thisArg">The value to use as 'this' when calling the function.</param>
    /// <returns>A <see cref="BoundJSFunction" /> that can be invoked with the bound context.</returns>
    /// <exception cref="InvalidOperationException">The JSValue is not a function.</exception>
    /// <remarks>
    ///     This is useful for calling methods with a specific 'this' context from C#.
    /// </remarks>
    public static BoundJSFunction Bind(this JSValue jsValue, JSValue thisArg)
    {
        if (!jsValue.IsFunction())
            throw new InvalidOperationException("JSValue is not a function");

        return new BoundJSFunction(jsValue, thisArg);
    }

    /// <summary>
    ///     Invokes a JavaScript function synchronously with unbound 'this' and returns the raw result.
    /// </summary>
    /// <param name="jsValue">The JavaScript function to invoke.</param>
    /// <param name="args">.NET arguments to pass to the function (converted automatically).</param>
    /// <returns>The raw <see cref="JSValue" /> result of the function call.</returns>
    /// <exception cref="InvalidOperationException">The JSValue is not a function.</exception>
    /// <exception cref="HakoException">
    ///     The function invocation failed. The InnerException contains a <see cref="JavaScriptException" />
    ///     with details about the JavaScript error.
    /// </exception>
    public static JSValue Invoke(this JSValue jsValue, params object?[] args)
    {
        return InvokeInternal(jsValue, null, args);
    }

    /// <summary>
    ///     Invokes a JavaScript function synchronously and returns the result as a typed .NET value.
    /// </summary>
    /// <typeparam name="TResult">The .NET type to convert the result to.</typeparam>
    /// <param name="jsValue">The JavaScript function to invoke.</param>
    /// <param name="args">.NET arguments to pass to the function (converted automatically).</param>
    /// <returns>The function result converted to <typeparamref name="TResult" />.</returns>
    /// <exception cref="InvalidOperationException">The JSValue is not a function.</exception>
    /// <exception cref="HakoException">
    ///     The function invocation failed. The InnerException contains a <see cref="JavaScriptException" />
    ///     with details about the JavaScript error.
    /// </exception>
    public static TResult Invoke<TResult>(this JSValue jsValue, params object?[] args)
    {
        return InvokeInternal<TResult>(jsValue, null, args);
    }

    /// <summary>
    ///     Invokes a JavaScript function asynchronously and returns the raw result.
    ///     Automatically handles promises.
    /// </summary>
    /// <param name="jsValue">The JavaScript function to invoke.</param>
    /// <param name="args">.NET arguments to pass to the function (converted automatically).</param>
    /// <returns>A task containing the raw <see cref="JSValue" /> result.</returns>
    /// <exception cref="InvalidOperationException">The JSValue is not a function.</exception>
    /// <exception cref="HakoException">
    ///     The function invocation or promise resolution failed. The InnerException contains either:
    ///     <list type="bullet">
    ///         <item>
    ///             <description><see cref="JavaScriptException" /> if the function threw an error</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <see cref="PromiseRejectedException" /> if the function returned a rejected Promise.
    ///                 If the rejection reason was a JavaScript Error object, it will be wrapped as a
    ///                 <see cref="JavaScriptException" />
    ///                 in the PromiseRejectedException's InnerException.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </exception>
    /// <remarks>
    ///     If the function returns a Promise, this method automatically awaits the promise resolution.
    /// </remarks>
    public static Task<JSValue> InvokeAsync(this JSValue jsValue, params object?[] args)
    {
        return InvokeAsyncInternal(jsValue, null, args);
    }

    /// <summary>
    ///     Invokes a JavaScript function asynchronously and returns the result as a typed .NET value.
    ///     Automatically handles promises.
    /// </summary>
    /// <typeparam name="TResult">The .NET type to convert the result to.</typeparam>
    /// <param name="jsValue">The JavaScript function to invoke.</param>
    /// <param name="args">.NET arguments to pass to the function (converted automatically).</param>
    /// <returns>A task containing the function result converted to <typeparamref name="TResult" />.</returns>
    /// <exception cref="InvalidOperationException">The JSValue is not a function.</exception>
    /// <exception cref="HakoException">
    ///     The function invocation or promise resolution failed. The InnerException contains either:
    ///     <list type="bullet">
    ///         <item>
    ///             <description><see cref="JavaScriptException" /> if the function threw an error</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <see cref="PromiseRejectedException" /> if the function returned a rejected Promise.
    ///                 If the rejection reason was a JavaScript Error object, it will be wrapped as a
    ///                 <see cref="JavaScriptException" />
    ///                 in the PromiseRejectedException's InnerException.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </exception>
    public static Task<TResult> InvokeAsync<TResult>(this JSValue jsValue, params object?[] args)
    {
        return InvokeAsyncInternal<TResult>(jsValue, null, args);
    }

    internal static JSValue InvokeInternal(JSValue jsValue, JSValue? thisArg, object?[] args)
    {
        var realm = jsValue.Realm;

        if (!jsValue.IsFunction())
            throw new InvalidOperationException("JSValue is not a function");

        ArgumentNullException.ThrowIfNull(Hako.Dispatcher, nameof(Hako.Dispatcher));

        return Hako.Dispatcher.Invoke(() =>
        {
            var jsArgs = new JSValue[args.Length];
            try
            {
                for (var i = 0; i < args.Length; i++)
                    jsArgs[i] = realm.NewValue(args[i]);

                using var callResult = realm.CallFunction(jsValue, thisArg, jsArgs);

                if (callResult.TryGetFailure(out var error))
                {
                    var exception = realm.GetLastError(error.GetHandle());
                    if (exception is not null) throw new HakoException("Function invocation failed", exception);

                    using var reasonBox = error.ToNativeValue<object>();
                    throw new HakoException("Function invocation failed",
                        new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
                }

                using var result = callResult.Unwrap();
                return result.Dup();
            }
            finally
            {
                foreach (var arg in jsArgs)
                    arg.Dispose();
            }
        });
    }

    internal static TResult InvokeInternal<TResult>(JSValue jsValue, JSValue? thisArg, object?[] args)
    {
        using var result = InvokeInternal(jsValue, thisArg, args);
        using var nativeBox = result.ToNativeValue<TResult>();
        return nativeBox.Value;
    }

    public static async Task<JSValue> Await(this JSValue jsValue, CancellationToken cancellationToken = default)
    {
        return await Hako.Dispatcher.InvokeAsync(async () =>
        {
            if (!jsValue.IsPromise()) return jsValue;

            using var resolved = await jsValue.Realm.ResolvePromise(jsValue, cancellationToken).ConfigureAwait(false);
            if (resolved.TryGetFailure(out var failure))
            {
                var jsException = jsValue.Realm.GetLastError(failure.GetHandle());
                if (jsException is not null)
                    throw new HakoException("Promise resolution failed", new PromiseRejectedException(jsException));

                using var reasonBox = failure.ToNativeValue<object>();
                throw new HakoException("Promise resolution failed", new PromiseRejectedException(reasonBox.Value));
            }

            return resolved.Unwrap();
        }, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<TValue> Await<TValue>(this JSValue jsValue, CancellationToken cancellationToken = default)
    {
        return await Hako.Dispatcher.InvokeAsync(async () =>
        {
            using var result = await Await(jsValue, cancellationToken).ConfigureAwait(false);
            using var nativeBox = result.ToNativeValue<TValue>();
            return nativeBox.Value;
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<JSValue> InvokeAsyncInternal(JSValue jsValue, JSValue? thisArg, object?[] args)
    {
        var realm = jsValue.Realm;

        if (!jsValue.IsFunction())
            throw new InvalidOperationException("JSValue is not a function");

        ArgumentNullException.ThrowIfNull(Hako.Dispatcher, nameof(Hako.Dispatcher));

        return await Hako.Dispatcher.InvokeAsync(async () =>
        {
            var jsArgs = new JSValue[args.Length];
            try
            {
                for (var i = 0; i < args.Length; i++)
                    jsArgs[i] = realm.NewValue(args[i]);

                using var callResult = realm.CallFunction(jsValue, thisArg, jsArgs);

                if (callResult.TryGetFailure(out var error))
                {
                    var exception = realm.GetLastError(error.GetHandle());
                    if (exception is not null) throw new HakoException("Function invocation failed", exception);

                    using var reasonBox = error.ToNativeValue<object>();
                    throw new HakoException("Function invocation failed",
                        new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
                }

                using var result = callResult.Unwrap();

                if (result.IsPromise())
                {
                    using var resolved = await realm.ResolvePromise(result).ConfigureAwait(false);
                    if (resolved.TryGetFailure(out var failure))
                    {
                        var jsException = realm.GetLastError(failure.GetHandle());
                        if (jsException is not null)
                            throw new HakoException("Promise resolution failed",
                                new PromiseRejectedException(jsException));

                        using var reasonBox = failure.ToNativeValue<object>();
                        throw new HakoException("Promise resolution failed",
                            new PromiseRejectedException(reasonBox.Value));
                    }

                    return resolved.Unwrap();
                }

                return result.Dup();
            }
            finally
            {
                foreach (var arg in jsArgs)
                    arg.Dispose();
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Creates a disposal scope where the JSValue and any deferred disposables
    ///     are automatically disposed when the scope exits (in reverse order).
    /// </summary>
    /// <typeparam name="T">The return type of the action.</typeparam>
    /// <param name="value">The JavaScript value to scope.</param>
    /// <param name="action">An action that receives the value and a scope for deferring additional disposables.</param>
    /// <returns>The result of the action.</returns>
    /// <remarks>
    ///     <para>
    ///         This is useful for managing multiple related JavaScript values that should be disposed together.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// var result = jsArray.UseScope((arr, scope) =>
    /// {
    ///     var first = arr.GetProperty(0);
    ///     scope.Defer(first);
    ///     
    ///     var second = arr.GetProperty(1);
    ///     scope.Defer(second);
    ///     
    ///     return first.AsNumber() + second.AsNumber();
    /// });
    /// // first, second, and jsArray are all disposed here
    /// </code>
    ///     </para>
    /// </remarks>
    public static T UseScope<T>(this JSValue value, Func<JSValue, DisposableScope, T> action)
    {
        using var scope = new DisposableScope();
        scope.Defer(value);
        return action(value, scope);
    }

    /// <summary>
    ///     Creates an async disposal scope where the JSValue and any deferred disposables
    ///     are automatically disposed when the scope exits (in reverse order).
    /// </summary>
    /// <typeparam name="T">The return type of the action.</typeparam>
    /// <param name="value">The JavaScript value to scope.</param>
    /// <param name="action">An async action that receives the value and a scope for deferring additional disposables.</param>
    /// <returns>A task containing the result of the action.</returns>
    public static async Task<T> UseScopeAsync<T>(this JSValue value, Func<JSValue, DisposableScope, Task<T>> action)
    {
        using var scope = new DisposableScope();
        scope.Defer(value);
        return await action(value, scope).ConfigureAwait(false);
    }


    public static JavaScriptException? GetException(this JSValue jsValue)
    {
        return jsValue.Realm.GetLastError(jsValue.GetHandle());
    }

    internal static async Task<TResult> InvokeAsyncInternal<TResult>(JSValue jsValue, JSValue? thisArg, object?[] args)
    {
        using var result = await InvokeAsyncInternal(jsValue, thisArg, args).ConfigureAwait(false);
        using var nativeBox = result.ToNativeValue<TResult>();
        return nativeBox.Value;
    }

    /// <summary>
    ///     Creates a disposal scope where the JSValue and any deferred disposables
    ///     are automatically disposed when the scope exits (in reverse order).
    /// </summary>
    /// <param name="value">The JavaScript value to scope.</param>
    /// <param name="action">An action that receives the value and a scope for deferring additional disposables.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     <para>
    ///         This is useful for managing multiple related JavaScript values that should be disposed together.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// jsArray.UseScope((arr, scope) =>
    /// {
    ///     var first = scope.Defer(arr.GetProperty(0));
    ///     var second = scope.Defer(arr.GetProperty(1));
    ///     
    ///     Console.WriteLine($"Sum: {first.AsNumber() + second.AsNumber()}");
    /// });
    /// // first, second, and jsArray are all disposed here
    /// </code>
    ///     </para>
    /// </remarks>
    public static void UseScope(this JSValue value, Action<JSValue, DisposableScope> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = new DisposableScope();
        scope.Defer(value);
        action(value, scope);
    }

    /// <summary>
    ///     Creates an async disposal scope where the JSValue and any deferred disposables
    ///     are automatically disposed when the scope exits (in reverse order).
    /// </summary>
    /// <param name="value">The JavaScript value to scope.</param>
    /// <param name="action">An async action that receives the value and a scope for deferring additional disposables.</param>
    /// <returns>A task that completes when the action finishes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     <para>
    ///         This is useful for managing multiple related JavaScript values during async operations.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// await jsPromise.UseScopeAsync(async (promise, scope) =>
    /// {
    ///     var result = scope.Defer(await promise.Await());
    ///     var data = scope.Defer(result.GetProperty("data"));
    ///     
    ///     Console.WriteLine($"Data: {data.GetPropertyOrDefault&lt;string&gt;("message")}");
    /// });
    /// // All deferred values are automatically disposed here
    /// </code>
    ///     </para>
    /// </remarks>
    public static async Task UseScopeAsync(this JSValue value, Func<JSValue, DisposableScope, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var scope = new DisposableScope();
        scope.Defer(value);
        await action(value, scope).ConfigureAwait(false);
    }

    /// <summary>
    ///     Converts a JavaScript array to a .NET array of primitive types.
    /// </summary>
    /// <typeparam name="T">The primitive element type (string, bool, int, long, double, float, etc.).</typeparam>
    /// <param name="jsValue">The JavaScript array value.</param>
    /// <returns>A .NET array containing the converted elements.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    /// <exception cref="NotSupportedException">The element type is not a supported primitive type.</exception>
    /// <remarks>
    ///     <para>
    ///         This method handles primitive types only. For custom types implementing <see cref="IJSMarshalable{T}" />,
    ///         use <see cref="ToArrayOf{T}" /> instead.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// using var jsArray = realm.EvalCode("[1, 2, 3]").Unwrap();
    /// int[] numbers = jsArray.ToArray&lt;int&gt;();
    /// 
    /// using var jsStrings = realm.EvalCode("['a', 'b', 'c']").Unwrap();
    /// string[] strings = jsStrings.ToArray&lt;string&gt;();
    /// </code>
    ///     </para>
    /// </remarks>
    public static T[] ToArray<T>(this JSValue jsValue)
    {
        if (!jsValue.IsArray())
            throw new InvalidOperationException("Value is not an array");

        using var lengthProp = jsValue.GetProperty("length");
        var length = (int)lengthProp.AsNumber();

        var array = new T[length];
        var elementType = typeof(T);

        for (var i = 0; i < length; i++)
        {
            using var jsElement = jsValue.GetProperty(i);

            if (elementType == typeof(string))
            {
                var stringValue = jsElement.IsString() ? jsElement.AsString() :
                    jsElement.IsNullOrUndefined() ? string.Empty : jsElement.AsString();
                array[i] = Unsafe.As<string, T>(ref stringValue);
            }
            else if (elementType == typeof(bool))
            {
                var boolValue = jsElement.AsBoolean();
                array[i] = Unsafe.As<bool, T>(ref boolValue);
            }
            else if (elementType == typeof(int))
            {
                var intValue = jsElement.IsNumber() ? (int)jsElement.AsNumber() : 0;
                array[i] = Unsafe.As<int, T>(ref intValue);
            }
            else if (elementType == typeof(long))
            {
                var longValue = jsElement.IsNumber() ? (long)jsElement.AsNumber() : 0L;
                array[i] = Unsafe.As<long, T>(ref longValue);
            }
            else if (elementType == typeof(double))
            {
                var doubleValue = jsElement.IsNumber() ? jsElement.AsNumber() : 0.0;
                array[i] = Unsafe.As<double, T>(ref doubleValue);
            }
            else if (elementType == typeof(float))
            {
                var floatValue = jsElement.IsNumber() ? (float)jsElement.AsNumber() : 0.0f;
                array[i] = Unsafe.As<float, T>(ref floatValue);
            }
            else if (elementType == typeof(short))
            {
                var shortValue = jsElement.IsNumber() ? (short)jsElement.AsNumber() : (short)0;
                array[i] = Unsafe.As<short, T>(ref shortValue);
            }
            else if (elementType == typeof(byte))
            {
                var byteValue = jsElement.IsNumber() ? (byte)jsElement.AsNumber() : (byte)0;
                array[i] = Unsafe.As<byte, T>(ref byteValue);
            }
            else if (elementType == typeof(sbyte))
            {
                var sbyteValue = jsElement.IsNumber() ? (sbyte)jsElement.AsNumber() : (sbyte)0;
                array[i] = Unsafe.As<sbyte, T>(ref sbyteValue);
            }
            else if (elementType == typeof(uint))
            {
                var uintValue = jsElement.IsNumber() ? (uint)jsElement.AsNumber() : 0u;
                array[i] = Unsafe.As<uint, T>(ref uintValue);
            }
            else if (elementType == typeof(ulong))
            {
                var ulongValue = jsElement.IsNumber() ? (ulong)jsElement.AsNumber() : 0ul;
                array[i] = Unsafe.As<ulong, T>(ref ulongValue);
            }
            else if (elementType == typeof(ushort))
            {
                var ushortValue = jsElement.IsNumber() ? (ushort)jsElement.AsNumber() : (ushort)0;
                array[i] = Unsafe.As<ushort, T>(ref ushortValue);
            }
            else if (elementType == typeof(DateTime))
            {
                var dateTimeValue = jsElement.IsDate() ? jsElement.AsDateTime() : default;
                array[i] = Unsafe.As<DateTime, T>(ref dateTimeValue);
            }
            else
            {
                throw new NotSupportedException(
                    $"Array element type {elementType.Name} is not supported. Only primitive types (string, bool, int, long, float, double, etc.) are supported. Use ToArrayOf<T>() for custom types implementing IJSMarshalable<T>.");
            }
        }

        return array;
    }
    

    /// <summary>
    ///     Converts a JavaScript array to a .NET array of types implementing <see cref="IJSMarshalable{T}" />.
    /// </summary>
    /// <typeparam name="T">The element type that implements <see cref="IJSMarshalable{T}" />.</typeparam>
    /// <param name="jsValue">The JavaScript array value.</param>
    /// <returns>A .NET array containing the converted elements.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array or conversion fails.</exception>
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
    /// using var jsArray = realm.EvalCode("[{x: 1, y: 2}, {x: 3, y: 4}]").Unwrap();
    /// Point[] points = jsArray.ToArrayOf&lt;Point&gt;();
    /// </code>
    ///     </para>
    /// </remarks>
    public static T[] ToArrayOf<T>(this JSValue jsValue) where T : IJSMarshalable<T>
    {
        if (!jsValue.IsArray())
            throw new InvalidOperationException("Value is not an array");

        var realm = jsValue.Realm;
        using var lengthProp = jsValue.GetProperty("length");
        var length = (int)lengthProp.AsNumber();

        var array = new T[length];

        for (var i = 0; i < length; i++)
        {
            using var jsElement = jsValue.GetProperty(i);
            array[i] = T.FromJSValue(realm, jsElement);
        }

        return array;
    }

    /// <summary>
    ///     Converts a JavaScript value to a native .NET value, disposing the original.
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <typeparam name="T">The type of the value to convert</typeparam>
    /// <returns>The converted value</returns>
    public static T GetNativeValue<T>(this JSValue value)
    {
        using var v = value.ToNativeValue<T>();
        return v.Value;
    }

    /// <summary>
    ///     Converts a JavaScript object to a .NET Dictionary with string keys and primitive values.
    /// </summary>
    /// <typeparam name="TValue">The primitive value type (string, bool, int, long, double, float, etc.).</typeparam>
    /// <param name="jsValue">The JavaScript object value.</param>
    /// <returns>A .NET Dictionary containing the converted key-value pairs.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    /// <exception cref="NotSupportedException">The value type is not a supported primitive type.</exception>
    /// <remarks>
    ///     <para>
    ///         This method handles primitive value types only. For custom types implementing <see cref="IJSMarshalable{T}" />,
    ///         use <see cref="ToDictionaryOf{TValue}" /> instead.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// using var jsObject = realm.EvalCode("({a: 1, b: 2, c: 3})").Unwrap();
    /// Dictionary&lt;string, int&gt; dict = jsObject.ToDictionary&lt;int&gt;();
    /// 
    /// using var jsData = realm.EvalCode("({name: 'Alice', city: 'NYC'})").Unwrap();
    /// Dictionary&lt;string, string&gt; data = jsData.ToDictionary&lt;string&gt;();
    /// </code>
    ///     </para>
    /// </remarks>
    public static Dictionary<string, TValue> ToDictionary<TValue>(this JSValue jsValue)
    {
        if (!jsValue.IsObject() || jsValue.IsArray())
            throw new InvalidOperationException("Value must be a JavaScript object (not an array)");

        var dictionary = new Dictionary<string, TValue>();

        foreach (var keyValue in jsValue.GetOwnPropertyNames())
        {
            var value = jsValue.GetProperty(keyValue).GetNativeValue<TValue>();
            var key = keyValue.GetNativeValue<string>();
            dictionary[key] = value;
        }

        return dictionary;
    }

    /// <summary>
    ///     Converts a JavaScript object to a .NET Dictionary with typed keys and primitive values.
    /// </summary>
    /// <typeparam name="TKey">The key type (string or numeric types).</typeparam>
    /// <typeparam name="TValue">The primitive value type (string, bool, int, long, double, float, etc.).</typeparam>
    /// <param name="jsValue">The JavaScript object value.</param>
    /// <returns>A .NET Dictionary containing the converted key-value pairs.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    /// <exception cref="NotSupportedException">The key or value type is not supported.</exception>
    /// <remarks>
    ///     <para>
    ///         This method handles primitive value types only. For custom types implementing <see cref="IJSMarshalable{T}" />,
    ///         use <see cref="ToDictionaryOf{TKey, TValue}" /> instead.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// using var jsObject = realm.EvalCode("({0: 'a', 1: 'b', 2: 'c'})").Unwrap();
    /// Dictionary&lt;int, string&gt; dict = jsObject.ToDictionary&lt;int, string&gt;();
    /// </code>
    ///     </para>
    /// </remarks>
    public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this JSValue jsValue) where TKey : notnull
    {
        if (!jsValue.IsObject() || jsValue.IsArray())
            throw new InvalidOperationException("Value must be a JavaScript object (not an array)");

        var dictionary = new Dictionary<TKey, TValue>();

        foreach (var keyValue in jsValue.GetOwnPropertyNames())
        {
            var value = jsValue.GetProperty(keyValue).GetNativeValue<TValue>();
            var key = keyValue.GetNativeValue<TKey>();
            dictionary[key] = value;
        }

        return dictionary;
    }

    /// <summary>
    ///     Converts a JavaScript object to a .NET Dictionary with string keys and IJSMarshalable values.
    /// </summary>
    /// <typeparam name="TValue">The value type that implements <see cref="IJSMarshalable{T}" />.</typeparam>
    /// <param name="jsValue">The JavaScript object value.</param>
    /// <returns>A .NET Dictionary containing the converted key-value pairs.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object or conversion fails.</exception>
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
    /// using var jsObject = realm.EvalCode("({a: {x: 1, y: 2}, b: {x: 3, y: 4}})").Unwrap();
    /// Dictionary&lt;string, Point&gt; points = jsObject.ToDictionaryOf&lt;Point&gt;();
    /// </code>
    ///     </para>
    /// </remarks>
    public static Dictionary<string, TValue> ToDictionaryOf<TValue>(this JSValue jsValue)
        where TValue : IJSMarshalable<TValue>
    {
        if (!jsValue.IsObject() || jsValue.IsArray())
            throw new InvalidOperationException("Value must be a JavaScript object (not an array)");

        var dictionary = new Dictionary<string, TValue>();
        var realm = jsValue.Realm;

        foreach (var keyValue in jsValue.GetOwnPropertyNames())
        {
            using var propertyValue = jsValue.GetProperty(keyValue);
            var value = TValue.FromJSValue(realm, propertyValue);
            var key = keyValue.GetNativeValue<string>();
            dictionary[key] = value;
        }

        return dictionary;
    }

    /// <summary>
    ///     Converts a JavaScript object to a .NET Dictionary with typed keys and IJSMarshalable values.
    /// </summary>
    /// <typeparam name="TKey">The key type (string or numeric types).</typeparam>
    /// <typeparam name="TValue">The value type that implements <see cref="IJSMarshalable{T}" />.</typeparam>
    /// <param name="jsValue">The JavaScript object value.</param>
    /// <returns>A .NET Dictionary containing the converted key-value pairs.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object or conversion fails.</exception>
    /// <exception cref="NotSupportedException">The key type is not supported.</exception>
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
    /// using var jsObject = realm.EvalCode("({0: {x: 1, y: 2}, 1: {x: 3, y: 4}})").Unwrap();
    /// Dictionary&lt;int, Point&gt; points = jsObject.ToDictionaryOf&lt;int, Point&gt;();
    /// </code>
    ///     </para>
    /// </remarks>
    public static Dictionary<TKey, TValue> ToDictionaryOf<TKey, TValue>(this JSValue jsValue)
        where TKey : notnull
        where TValue : IJSMarshalable<TValue>
    {
        if (!jsValue.IsObject() || jsValue.IsArray())
            throw new InvalidOperationException("Value must be a JavaScript object (not an array)");

        var dictionary = new Dictionary<TKey, TValue>();
        var realm = jsValue.Realm;

        foreach (var keyValue in jsValue.GetOwnPropertyNames())
        {
            using var propertyValue = jsValue.GetProperty(keyValue);
            var value = TValue.FromJSValue(realm, propertyValue);
            var key = keyValue.GetNativeValue<TKey>();
            dictionary[key] = value;
        }

        return dictionary;
    }

    /// <summary>
    ///     Sets a readonly property on the object (writable=false, enumerable=true, configurable=false).
    /// </summary>
    /// <param name="obj">The target object.</param>
    /// <param name="key">The property name.</param>
    /// <param name="value">The property value.</param>
    public static void SetReadOnlyProperty(this JSValue obj, string key, JSValue value)
    {
        DefineProperty(obj, key, value, false, true, false);
    }

    /// <summary>
    ///     Sets a property with custom descriptor attributes.
    /// </summary>
    /// <param name="obj">The target object.</param>
    /// <param name="key">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <param name="writable">Whether the property value can be changed.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    public static void SetPropertyWithDescriptor(
        this JSValue obj,
        string key,
        JSValue value,
        bool writable = true,
        bool enumerable = true,
        bool configurable = true)
    {
        DefineProperty(obj, key, value, writable, enumerable, configurable);
    }

    /// <summary>
    ///     Sets a hidden property that doesn't appear in enumerations (writable=true, enumerable=false, configurable=true).
    /// </summary>
    /// <param name="obj">The target object.</param>
    /// <param name="key">The property name.</param>
    /// <param name="value">The property value.</param>
    public static void SetHiddenProperty(this JSValue obj, string key, JSValue value)
    {
        DefineProperty(obj, key, value, true, false, true);
    }

    /// <summary>
    ///     Sets a locked property that cannot be deleted or reconfigured (writable=true, enumerable=true, configurable=false).
    /// </summary>
    /// <param name="obj">The target object.</param>
    /// <param name="key">The property name.</param>
    /// <param name="value">The property value.</param>
    public static void SetLockedProperty(this JSValue obj, string key, JSValue value)
    {
        DefineProperty(obj, key, value, true, true, false);
    }

    /// <summary>
    ///     Freezes the object, making it completely immutable.
    /// </summary>
    /// <param name="obj">The object to freeze.</param>
    /// <param name="realm">The realm context.</param>
    public static void Freeze(this JSValue obj, Realm realm)
    {
        using var globalObj = realm.GetGlobalObject();
        using var objectConstructor = globalObj.GetProperty("Object");
        using var freezeFunc = objectConstructor.GetProperty("freeze");
        using var result = realm.CallFunction(freezeFunc, realm.Undefined(), obj);

        if (result.TryGetFailure(out var error))
        {
            var exception = realm.GetLastError(error.GetHandle());
            if (exception is not null)
                throw new HakoException("Failed to freeze object", exception);

            using var reasonBox = error.ToNativeValue<object>();
            throw new HakoException("Failed to freeze object",
                new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
        }
    }

    /// <summary>
    ///     Seals the object, preventing new properties from being added.
    /// </summary>
    /// <param name="obj">The object to seal.</param>
    /// <param name="realm">The realm context.</param>
    public static void Seal(this JSValue obj, Realm realm)
    {
        using var globalObj = realm.GetGlobalObject();
        using var objectConstructor = globalObj.GetProperty("Object");
        using var sealFunc = objectConstructor.GetProperty("seal");
        using var result = realm.CallFunction(sealFunc, realm.Undefined(), obj);

        if (result.TryGetFailure(out var error))
        {
            var exception = realm.GetLastError(error.GetHandle());
            if (exception is not null)
                throw new HakoException("Failed to seal object", exception);

            using var reasonBox = error.ToNativeValue<object>();
            throw new HakoException("Failed to seal object",
                new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
        }
    }


    private static void DefineProperty(
        JSValue obj,
        string key,
        JSValue value,
        bool writable,
        bool enumerable,
        bool configurable)
    {
        ArgumentNullException.ThrowIfNull(obj);
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));
        ArgumentNullException.ThrowIfNull(value);

        var realm = obj.Realm;
        using var keyValue = realm.NewString(key);

        var result = realm.Runtime.Registry.DefineProp(
            realm.Pointer,
            obj.GetHandle(),
            keyValue.GetHandle(),
            value.GetHandle(),
            realm.Runtime.Registry.GetUndefined(), // get
            realm.Runtime.Registry.GetUndefined(), // set
            configurable ? 1 : 0,
            enumerable ? 1 : 0,
            1, // hasValue
            1, // hasWritable
            writable ? 1 : 0
        );

        if (result == -1)
        {
            var exception = realm.GetLastError();
            if (exception is not null)
                throw new HakoException($"Failed to define property '{key}'", exception);
        
            throw new HakoException($"Failed to define property '{key}'",
                new JavaScriptException("(unknown error)"));
        }

        if (result == 0)
        {
            throw new HakoException($"Failed to define property '{key}'",
                new JavaScriptException("Operation returned FALSE"));
        }
    }

    #region Synchronous Iteration

    /// <summary>
    ///     Iterates over an iterable JavaScript value synchronously.
    /// </summary>
    /// <param name="value">The iterable JavaScript value (e.g., Array, Set, Map).</param>
    /// <param name="context">An optional realm context. If <c>null</c>, uses the value's realm.</param>
    /// <returns>An enumerable sequence of iteration results.</returns>
    /// <exception cref="HakoException">
    ///     An error occurred while obtaining or using the iterator. The InnerException contains
    ///     a <see cref="JavaScriptException" /> with details about the JavaScript error.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         This method calls the object's Symbol.iterator method to obtain an iterator.
    ///         Each yielded value must be disposed by the caller.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// using var array = realm.EvalCode("[1, 2, 3]").Unwrap();
    /// foreach (var itemResult in array.Iterate())
    /// {
    ///     if (itemResult.TryGetSuccess(out var item))
    ///     {
    ///         using (item)
    ///         {
    ///             Console.WriteLine(item.AsNumber());
    ///         }
    ///     }
    /// }
    /// </code>
    ///     </para>
    /// </remarks>
    public static IEnumerable<DisposableResult<JSValue, JSValue>> Iterate(
        this JSValue value, Realm? context = null)
    {
        var realm = context ?? value.Realm;
        using var iteratorResult = realm.GetIterator(value);
        if (iteratorResult.TryGetFailure(out var error))
        {
            var exception = realm.GetLastError(error.GetHandle());
            if (exception is not null)
                throw new HakoException("An error occurred while iterating the value", exception);

            using var reasonBox = error.ToNativeValue<object>();
            throw new HakoException("An error occurred while iterating the value",
                new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
        }

        using var iterator = iteratorResult.Unwrap();
        foreach (var entry in iterator)
            yield return entry;
    }

    /// <summary>
    ///     Iterates over an iterable JavaScript value synchronously, converting each item to the specified .NET type.
    /// </summary>
    /// <typeparam name="T">The .NET type to convert each item to.</typeparam>
    /// <param name="value">The iterable JavaScript value (e.g., Array, Set).</param>
    /// <param name="context">An optional realm context. If <c>null</c>, uses the value's realm.</param>
    /// <returns>An enumerable sequence of converted values.</returns>
    /// <exception cref="HakoException">
    ///     An error occurred while iterating the value. The InnerException contains
    ///     a <see cref="JavaScriptException" /> with details about the JavaScript error.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         This is a convenience method that automatically unwraps and converts each iterated value.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// using var array = realm.EvalCode("[1, 2, 3]").Unwrap();
    /// foreach (var number in array.Iterate&lt;double&gt;())
    /// {
    ///     Console.WriteLine(number);
    /// }
    /// </code>
    ///     </para>
    /// </remarks>
    public static IEnumerable<T> Iterate<T>(this JSValue value, Realm? context = null)
    {
        var realm = context ?? value.Realm;
        foreach (var itemResult in value.Iterate(realm))
        {
            if (itemResult.TryGetFailure(out var error))
            {
                var exception = realm.GetLastError(error.GetHandle());
                if (exception is not null)
                    throw new HakoException("An error occurred while iterating the value", exception);

                using var reasonBox = error.ToNativeValue<object>();
                throw new HakoException("An error occurred while iterating the value",
                    new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
            }

            if (itemResult.TryGetSuccess(out var item))
                using (item)
                {
                    using var native = item.ToNativeValue<T>();
                    yield return native.Value;
                }
        }
    }

    /// <summary>
    ///     Iterates over a JavaScript Map synchronously, yielding key-value pairs.
    /// </summary>
    /// <typeparam name="TKey">The .NET type for the map keys.</typeparam>
    /// <typeparam name="TValue">The .NET type for the map values.</typeparam>
    /// <param name="map">The JavaScript Map object.</param>
    /// <param name="context">An optional realm context. If <c>null</c>, uses the map's realm.</param>
    /// <returns>An enumerable sequence of key-value pairs.</returns>
    /// <exception cref="HakoException">
    ///     An error occurred while iterating the map. The InnerException contains
    ///     a <see cref="JavaScriptException" /> with details about the JavaScript error.
    /// </exception>
    public static IEnumerable<KeyValuePair<TKey, TValue>> IterateMap<TKey, TValue>(
        this JSValue map, Realm? context = null)
    {
        var realm = context ?? map.Realm;
        foreach (var entryResult in map.Iterate(realm))
        {
            if (entryResult.TryGetFailure(out var error))
            {
                var exception = realm.GetLastError(error.GetHandle());
                if (exception is not null)
                    throw new HakoException("An error occurred while iterating the map", exception);

                using var reasonBox = error.ToNativeValue<object>();
                throw new HakoException("An error occurred while iterating the map",
                    new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
            }

            if (entryResult.TryGetSuccess(out var entry))
                using (entry)
                {
                    var key = entry.GetPropertyOrDefault<TKey>(0);
                    var val = entry.GetPropertyOrDefault<TValue>(1);
                    yield return new KeyValuePair<TKey, TValue>(key, val);
                }
        }
    }

    /// <summary>
    ///     Iterates over a JavaScript Set synchronously, yielding the set's values.
    /// </summary>
    /// <typeparam name="T">The .NET type for the set values.</typeparam>
    /// <param name="set">The JavaScript Set object.</param>
    /// <param name="context">An optional realm context. If <c>null</c>, uses the set's realm.</param>
    /// <returns>An enumerable sequence of set values.</returns>
    /// <exception cref="HakoException">
    ///     An error occurred while iterating the set. The InnerException contains
    ///     a <see cref="JavaScriptException" /> with details about the JavaScript error.
    /// </exception>
    public static IEnumerable<T> IterateSet<T>(this JSValue set, Realm? context = null)
    {
        var realm = context ?? set.Realm;
        foreach (var entryResult in set.Iterate(realm))
        {
            if (entryResult.TryGetFailure(out var error))
            {
                var exception = realm.GetLastError(error.GetHandle());
                if (exception is not null)
                    throw new HakoException("An error occurred while iterating the set", exception);

                using var reasonBox = error.ToNativeValue<object>();
                throw new HakoException("An error occurred while iterating the set",
                    new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
            }

            if (entryResult.TryGetSuccess(out var entry))
                using (entry)
                {
                    using var native = entry.ToNativeValue<T>();
                    yield return native.Value;
                }
        }
    }

    #endregion

    #region Asynchronous Iteration

    /// <summary>
    ///     Iterates over an async iterable JavaScript value asynchronously.
    /// </summary>
    /// <param name="value">The async iterable JavaScript value.</param>
    /// <param name="context">An optional realm context. If <c>null</c>, uses the value's realm.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>An async enumerable sequence of iteration results.</returns>
    /// <exception cref="HakoException">
    ///     An error occurred while obtaining or using the async iterator. The InnerException contains
    ///     a <see cref="JavaScriptException" /> with details about the JavaScript error.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         This method calls the object's Symbol.asyncIterator method to obtain an async iterator.
    ///         The iteration properly yields control to the event loop between iterations.
    ///     </para>
    /// </remarks>
    public static async IAsyncEnumerable<DisposableResult<JSValue, JSValue>> IterateAsync(
        this JSValue value,
        Realm? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var realm = context ?? value.Realm;

        // Get the iterator
        var iteratorResult = await Hako.Dispatcher.InvokeAsync(() => realm.GetAsyncIterator(value), cancellationToken).ConfigureAwait(false);

        if (iteratorResult.TryGetFailure(out var error))
        {
            var exception = realm.GetLastError(error.GetHandle());
            if (exception is not null)
                throw new HakoException("An error occurred while iterating the value", exception);

            using var reasonBox = error.ToNativeValue<object>();
            throw new HakoException("An error occurred while iterating the value",
                new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
        }

        await using var iterator = iteratorResult.Unwrap();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Run the entire iteration step on the event loop thread
            var (hasNext, current) = await Hako.Dispatcher.InvokeAsync(async () =>
            {
                // We're on the event loop thread with SynchronizationContext installed
                var moveNextTask = iterator.MoveNextAsync();

                // Yield back to event loop while waiting - this allows jobs and timers to run
                while (!moveNextTask.IsCompleted) await Hako.Dispatcher.Yield();

                var hasNext = await moveNextTask.ConfigureAwait(false);
                var current = hasNext ? iterator.Current : null;

                return (hasNext, current);
            }, cancellationToken).ConfigureAwait(false);

            if (!hasNext)
                break;

            if (current != null) yield return current;
        }
    }

    /// <summary>
    ///     Iterates over an async iterable JavaScript value asynchronously, converting each item to the specified .NET type.
    /// </summary>
    /// <typeparam name="T">The .NET type to convert each item to.</typeparam>
    /// <param name="value">The async iterable JavaScript value.</param>
    /// <param name="context">An optional realm context. If <c>null</c>, uses the value's realm.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>An async enumerable sequence of converted values.</returns>
    /// <exception cref="HakoException">
    ///     An error occurred while iterating the value. The InnerException contains
    ///     a <see cref="JavaScriptException" /> with details about the JavaScript error.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         This is a convenience method that automatically unwraps and converts each iterated value.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    /// using var asyncIterable = realm.EvalCode("(async function*() { yield 1; yield 2; yield 3; })()").Unwrap();
    /// await foreach (var number in asyncIterable.IterateAsync&lt;double&gt;())
    /// {
    ///     Console.WriteLine(number);
    /// }
    /// </code>
    ///     </para>
    /// </remarks>
    public static async IAsyncEnumerable<T> IterateAsync<T>(
        this JSValue value,
        Realm? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var realm = context ?? value.Realm;
        await foreach (var itemResult in value.IterateAsync(realm, cancellationToken).ConfigureAwait(false))
        {
            if (itemResult.TryGetFailure(out var error))
            {
                var exception = realm.GetLastError(error.GetHandle());
                if (exception is not null)
                    throw new HakoException("An error occurred while iterating the value", exception);

                using var reasonBox = error.ToNativeValue<object>();
                throw new HakoException("An error occurred while iterating the value",
                    new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
            }

            if (itemResult.TryGetSuccess(out var item))
                using (item)
                {
                    using var native = item.ToNativeValue<T>();
                    yield return native.Value;
                }
        }
    }

    /// <summary>
    ///     Iterates over an async JavaScript Map, yielding key-value pairs.
    /// </summary>
    /// <typeparam name="TKey">The .NET type for the map keys.</typeparam>
    /// <typeparam name="TValue">The .NET type for the map values.</typeparam>
    /// <param name="map">The JavaScript async Map object.</param>
    /// <param name="context">An optional realm context. If <c>null</c>, uses the map's realm.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>An async enumerable sequence of key-value pairs.</returns>
    /// <exception cref="HakoException">
    ///     An error occurred while iterating the map. The InnerException contains
    ///     a <see cref="JavaScriptException" /> with details about the JavaScript error.
    /// </exception>
    public static async IAsyncEnumerable<KeyValuePair<TKey, TValue>> IterateMapAsync<TKey, TValue>(
        this JSValue map,
        Realm? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var realm = context ?? map.Realm;
        await foreach (var entryResult in map.IterateAsync(realm, cancellationToken).ConfigureAwait(false))
        {
            if (entryResult.TryGetFailure(out var error))
            {
                var exception = realm.GetLastError(error.GetHandle());
                if (exception is not null)
                    throw new HakoException("An error occurred while iterating the async map", exception);

                using var reasonBox = error.ToNativeValue<object>();
                throw new HakoException("An error occurred while iterating the async map",
                    new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
            }

            if (entryResult.TryGetSuccess(out var entry))
                using (entry)
                {
                    var key = entry.GetPropertyOrDefault<TKey>(0);
                    var val = entry.GetPropertyOrDefault<TValue>(1);
                    yield return new KeyValuePair<TKey, TValue>(key, val);
                }
        }
    }

    /// <summary>
    ///     Iterates over an async JavaScript Set, yielding the set's values.
    /// </summary>
    /// <typeparam name="T">The .NET type for the set values.</typeparam>
    /// <param name="set">The JavaScript async Set object.</param>
    /// <param name="context">An optional realm context. If <c>null</c>, uses the set's realm.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>An async enumerable sequence of set values.</returns>
    /// <exception cref="HakoException">
    ///     An error occurred while iterating the set. The InnerException contains
    ///     a <see cref="JavaScriptException" /> with details about the JavaScript error.
    /// </exception>
    public static async IAsyncEnumerable<T> IterateSetAsync<T>(
        this JSValue set,
        Realm? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var realm = context ?? set.Realm;
        await foreach (var entryResult in set.IterateAsync(realm, cancellationToken).ConfigureAwait(false))
        {
            if (entryResult.TryGetFailure(out var error))
            {
                var exception = realm.GetLastError(error.GetHandle());
                if (exception is not null)
                    throw new HakoException("An error occurred while iterating the async set", exception);

                using var reasonBox = error.ToNativeValue<object>();
                throw new HakoException("An error occurred while iterating the async set",
                    new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
            }

            if (entryResult.TryGetSuccess(out var entry))
                using (entry)
                {
                    using var native = entry.ToNativeValue<T>();
                    yield return native.Value;
                }
        }
    }

    #endregion
}