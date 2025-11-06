using HakoJS.Builders;
using HakoJS.Exceptions;
using HakoJS.Lifetime;
using HakoJS.SourceGeneration;
using HakoJS.VM;

namespace HakoJS.Extensions;

/// <summary>
/// Provides extension methods for working with JavaScript realms.
/// </summary>
public static class RealmExtensions
{
    /// <summary>
    /// Creates a new object builder for constructing JavaScript objects in the specified realm.
    /// </summary>
    /// <param name="context">The realm in which to create the object.</param>
    /// <returns>A <see cref="JSObjectBuilder"/> for fluently building a JavaScript object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Use this method to create JavaScript objects with a fluent API that allows setting properties,
    /// methods, and other characteristics before finalizing the object.
    /// </remarks>
    public static JSObjectBuilder BuildObject(this Realm context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        return JSObjectBuilder.Create(context);
    }

    /// <summary>
    /// Creates a new object builder for constructing JavaScript objects with a specific prototype.
    /// </summary>
    /// <param name="context">The realm in which to create the object.</param>
    /// <param name="prototype">The prototype object to use for the new object.</param>
    /// <returns>A <see cref="JSObjectBuilder"/> configured with the specified prototype.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
    /// <remarks>
    /// This method is useful for creating objects that inherit from a custom prototype,
    /// enabling prototype-based inheritance patterns.
    /// </remarks>
    public static JSObjectBuilder BuildObject(this Realm context, JSValue prototype)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        return JSObjectBuilder.Create(context).WithPrototype(prototype);
    }

  
/// <summary>
/// Asynchronously evaluates JavaScript code in the specified realm.
/// </summary>
/// <param name="context">The realm in which to evaluate the code.</param>
/// <param name="code">The JavaScript code to evaluate.</param>
/// <param name="options">Optional evaluation options controlling how the code is executed.</param>
/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
/// <returns>
/// A task that represents the asynchronous operation and contains the resulting <see cref="JSValue"/>.
/// If the code returns a Promise, the task completes when the Promise resolves.
/// </returns>
/// <exception cref="ArgumentNullException"><paramref name="context"/> or Hako.Dispatcher is <c>null</c>.</exception>
/// <exception cref="HakoException">
/// The evaluation or Promise resolution failed. The <see cref="Exception.InnerException"/> contains
/// specific failure details:
/// <list type="bullet">
/// <item><description><see cref="JavaScriptException"/> if the code failed to evaluate (syntax error, runtime error, etc.)</description></item>
/// <item><description><see cref="PromiseRejectedException"/> if the returned Promise was rejected. If the rejection
/// reason was a JavaScript Error object, it will be wrapped as a <see cref="JavaScriptException"/> in the
/// PromiseRejectedException's InnerException. Otherwise, the rejection value is available via the
/// <see cref="PromiseRejectedException.Reason"/> property.</description></item>
/// </list>
/// </exception>
/// <exception cref="OperationCanceledException">The operation was canceled.</exception>
/// <remarks>
/// <para>
/// This method marshals the evaluation to the event loop thread. If the evaluated code returns
/// a Promise, this method automatically awaits the Promise resolution.
/// </para>
/// <para>
/// The returned <see cref="JSValue"/> must be disposed by the caller when no longer needed.
/// </para>
/// <para>
/// When using <see cref="RealmEvalOptions.Async"/> set to <c>true</c> with <see cref="EvalType.Global"/>,
/// QuickJS wraps the result in an object with a 'value' property, which is automatically unwrapped.
/// </para>
/// </remarks>
public static Task<JSValue> EvalAsync(
    this Realm context, 
    string code, 
    RealmEvalOptions? options = null,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(Hako.Dispatcher, nameof(Hako.Dispatcher));
    var evalOptions = options ?? new RealmEvalOptions();

    return Hako.Dispatcher.InvokeAsync(async () =>
    {
        using var result = context.EvalCode(code, evalOptions);
        if (result.TryGetFailure(out var error))
        {
            var exception = context.GetLastError(error.GetHandle());
            if (exception is not null)
            {
                throw new HakoException("Evaluation failed", exception);
            }
            using var reasonBox = error.ToNativeValue<object>();
            throw new HakoException("Evaluation failed", new JavaScriptException(reasonBox.Value?.ToString() ?? "(unknown error)"));
        }

        using var value = result.Unwrap();
    
        JSValue evaluated;
        if (value.IsPromise())
        {
            using var resolved = await context.ResolvePromise(value, cancellationToken);
            if (resolved.TryGetFailure(out var failure))
            {
                var jsException = context.GetLastError(failure.GetHandle());
                if (jsException is not null)
                {
                    throw new HakoException("Evaluation failed", new PromiseRejectedException(jsException));
                }
                
                using var reasonBox = failure.ToNativeValue<object>();
                throw new HakoException("Evaluation failed", new PromiseRejectedException(reasonBox.Value));
            }
            evaluated = resolved.Unwrap();
        }
        else
        {
            evaluated = value.Dup();
        }

        // When using Async flag with Global type, QuickJS wraps the result in { value: result }
        if (evalOptions is { Async: true, Type: EvalType.Global })
        {
            using (evaluated)
            {
                return evaluated.GetProperty("value");
            }
        }

        return evaluated;
    }, cancellationToken);
}

    /// <summary>
    /// Asynchronously evaluates JavaScript code and converts the result to a native .NET value.
    /// </summary>
    /// <typeparam name="TValue">The .NET type to convert the result to.</typeparam>
    /// <param name="context">The realm in which to evaluate the code.</param>
    /// <param name="code">The JavaScript code to evaluate.</param>
    /// <param name="options">Optional evaluation options controlling how the code is executed.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation and contains the evaluation result
    /// converted to type <typeparamref name="TValue"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or Hako.Dispatcher is <c>null</c>.</exception>
    /// <exception cref="HakoException">The evaluation failed, the Promise was rejected, or conversion to <typeparamref name="TValue"/> failed.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <remarks>
    /// <para>
    /// This is a convenience method that combines <see cref="EvalAsync(Realm, string, RealmEvalOptions?, CancellationToken)"/>
    /// with automatic conversion to a native .NET type.
    /// </para>
    /// <para>
    /// The JavaScript value is automatically disposed after conversion.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// int result = await realm.EvalAsync&lt;int&gt;("2 + 2");
    /// string name = await realm.EvalAsync&lt;string&gt;("'Hello World'");
    /// </code>
    /// </para>
    /// </remarks>
    public static async ValueTask<TValue> EvalAsync<TValue>(
        this Realm context, 
        string code, 
        RealmEvalOptions? options = null,
        CancellationToken cancellationToken = default) 
    {
        return await Hako.Dispatcher.InvokeAsync(async () =>
        {
            using var result = await EvalAsync(context, code, options, cancellationToken);
            using var nativeBox = result.ToNativeValue<TValue>();
            return nativeBox.Value;
        }, cancellationToken);
    }

    /// <summary>
    /// Configures global variables and functions in the realm using a fluent builder API.
    /// </summary>
    /// <param name="context">The realm to configure.</param>
    /// <param name="configure">An action that receives a <see cref="GlobalsBuilder"/> to define globals.</param>
    /// <returns>The same realm instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// This method provides a fluent way to add global variables, functions, and objects to the JavaScript realm.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// realm.WithGlobals(globals =>
    /// {
    ///     globals.DefineFunction("print", (string message) => Console.WriteLine(message));
    ///     globals.DefineValue("appVersion", "1.0.0");
    ///     globals.DefineObject("config", obj =>
    ///     {
    ///         obj.SetProperty("debug", true);
    ///         obj.SetProperty("port", 8080);
    ///     });
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public static Realm WithGlobals(this Realm context, Action<GlobalsBuilder> configure)
    {
        var builder = GlobalsBuilder.For(context);
        configure(builder);
        builder.Apply();
        return context;
    }

    /// <summary>
    /// Gets the unique type key for a source-generated JavaScript bindable type.
    /// </summary>
    /// <typeparam name="T">The JavaScript bindable type that implements <see cref="IJSBindable{T}"/>.</typeparam>
    /// <param name="context">The realm context (not used but maintains extension method pattern).</param>
    /// <returns>A unique string key identifying the type for JavaScript interop.</returns>
    /// <remarks>
    /// This type key is used internally for marshaling between .NET and JavaScript objects.
    /// It is automatically generated by the source generator for types decorated with [JSClass].
    /// </remarks>
    public static string TypeKey<T>(this Realm context) where T : class, IJSBindable<T>
    {
        return T.TypeKey;
    }

    /// <summary>
    /// Creates and registers a JavaScript class for the specified .NET type, making it available in the JavaScript global scope.
    /// </summary>
    /// <typeparam name="T">The .NET type decorated with [JSClass] that implements <see cref="IJSBindable{T}"/>.</typeparam>
    /// <param name="realm">The realm in which to create and register the class.</param>
    /// <param name="customName">
    /// An optional custom name for the JavaScript constructor. If <c>null</c>, uses the class name.
    /// </param>
    /// <returns>The created <see cref="JSClass"/> representing the JavaScript class.</returns>
    /// <remarks>
    /// <para>
    /// This method creates the JavaScript class and exposes its constructor in the global scope,
    /// allowing JavaScript code to instantiate it with <c>new ClassName()</c>.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// realm.RegisterClass&lt;TextEncoder&gt;();
    /// await realm.EvalAsync("const encoder = new TextEncoder()");
    /// </code>
    /// </para>
    /// <para>
    /// The class includes all methods and properties defined by [JSProperty] and [JSMethod] attributes.
    /// This should typically be called once per type at application startup.
    /// </para>
    /// </remarks>
    public static JSClass RegisterClass<T>(this Realm realm, string? customName = null)
        where T : class, IJSBindable<T>
    {
        var jsClass = T.CreatePrototype(realm);
        jsClass.RegisterGlobal(customName);
        return jsClass;
    }

    /// <summary>
    /// Creates a JavaScript class prototype for the specified .NET type and registers it for marshaling,
    /// without exposing the constructor in the global scope.
    /// </summary>
    /// <typeparam name="T">The .NET type decorated with [JSClass] that implements <see cref="IJSBindable{T}"/>.</typeparam>
    /// <param name="realm">The realm in which to create the prototype.</param>
    /// <returns>The created <see cref="JSClass"/> representing the JavaScript class.</returns>
    /// <remarks>
    /// <para>
    /// This method creates the class prototype and registers it for marshaling, allowing instances
    /// to be passed between .NET and JavaScript. However, the constructor is NOT exposed globally,
    /// so JavaScript cannot directly instantiate it with <c>new ClassName()</c> unless you manually
    /// expose the constructor.
    /// </para>
    /// <para>
    /// Use this when you want to control where the constructor is available, such as within a module
    /// or namespace rather than globally.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var jsClass = realm.CreatePrototype&lt;MyClass&gt;();
    /// // Manually expose on a namespace
    /// namespaceObj.SetProperty("MyClass", jsClass.Constructor);
    /// </code>
    /// </para>
    /// </remarks>
    public static JSClass CreatePrototype<T>(this Realm realm)
        where T : class, IJSBindable<T>
    {
        return T.CreatePrototype(realm);
    }
    
    /// <summary>
/// Creates a disposal scope for managing JavaScript values created during realm operations.
/// All deferred disposables are automatically disposed when the scope exits (in LIFO order).
/// </summary>
/// <typeparam name="T">The return type of the action.</typeparam>
/// <param name="realm">The realm in which to execute the action.</param>
/// <param name="action">An action that receives the realm and a scope for deferring disposables.</param>
/// <returns>The result of the action.</returns>
/// <exception cref="ArgumentNullException"><paramref name="realm"/> or <paramref name="action"/> is <c>null</c>.</exception>
/// <remarks>
/// <para>
/// This method simplifies managing multiple JavaScript values that need cleanup.
/// Values are disposed in reverse order (LIFO) when the scope exits.
/// </para>
/// <para>
/// Example:
/// <code>
/// var sum = realm.UseScope((r, scope) =>
/// {
///     var obj = scope.Defer(r.EvalCode("({ x: 10, y: 20 })").Unwrap());
///     var x = scope.Defer(obj.GetProperty("x"));
///     var y = scope.Defer(obj.GetProperty("y"));
///     
///     return x.AsNumber() + y.AsNumber();
/// });
/// // All deferred values are automatically disposed here
/// </code>
/// </para>
/// </remarks>
public static T UseScope<T>(this Realm realm, Func<Realm, DisposableScope, T> action)
{
    ArgumentNullException.ThrowIfNull(realm);
    ArgumentNullException.ThrowIfNull(action);
    
    using var scope = new DisposableScope();
    return action(realm, scope);
}

/// <summary>
/// Creates an async disposal scope for managing JavaScript values created during async realm operations.
/// All deferred disposables are automatically disposed when the scope exits (in LIFO order).
/// </summary>
/// <typeparam name="T">The return type of the action.</typeparam>
/// <param name="realm">The realm in which to execute the action.</param>
/// <param name="action">An async action that receives the realm and a scope for deferring disposables.</param>
/// <returns>A task containing the result of the action.</returns>
/// <exception cref="ArgumentNullException"><paramref name="realm"/> or <paramref name="action"/> is <c>null</c>.</exception>
/// <remarks>
/// <para>
/// This method simplifies managing multiple JavaScript values during async operations.
/// Values are disposed in reverse order (LIFO) when the scope exits.
/// </para>
/// <para>
/// Example:
/// <code>
/// var result = await realm.UseScopeAsync(async (r, scope) =>
/// {
///     var promise = scope.Defer(await r.EvalAsync("fetch('https://api.example.com/data')"));
///     var response = scope.Defer(await promise.Await());
///     var json = scope.Defer(await response.InvokeAsync("json"));
///     
///     return json.GetPropertyOrDefault&lt;string&gt;("message");
/// });
/// // All deferred values are automatically disposed here
/// </code>
/// </para>
/// </remarks>
public static async Task<T> UseScopeAsync<T>(this Realm realm, Func<Realm, DisposableScope, Task<T>> action)
{
    ArgumentNullException.ThrowIfNull(realm);
    ArgumentNullException.ThrowIfNull(action);
    
    using var scope = new DisposableScope();
    return await action(realm, scope);
}

/// <summary>
/// Creates a disposal scope for managing JavaScript values created during realm operations.
/// All deferred disposables are automatically disposed when the scope exits (in LIFO order).
/// </summary>
/// <param name="realm">The realm in which to execute the action.</param>
/// <param name="action">An action that receives the realm and a scope for deferring disposables.</param>
/// <exception cref="ArgumentNullException"><paramref name="realm"/> or <paramref name="action"/> is <c>null</c>.</exception>
/// <remarks>
/// <para>
/// This method simplifies managing multiple JavaScript values that need cleanup.
/// Values are disposed in reverse order (LIFO) when the scope exits.
/// </para>
/// <para>
/// Example:
/// <code>
/// realm.UseScope((r, scope) =>
/// {
///     var result = scope.Defer(r.EvalCode("2 + 2"));
///     Console.WriteLine($"Result: {result.Unwrap().AsNumber()}");
/// });
/// // All deferred values are automatically disposed here
/// </code>
/// </para>
/// </remarks>
public static void UseScope(this Realm realm, Action<Realm, DisposableScope> action)
{
    ArgumentNullException.ThrowIfNull(realm);
    ArgumentNullException.ThrowIfNull(action);
    
    using var scope = new DisposableScope();
    action(realm, scope);
}

/// <summary>
/// Creates an async disposal scope for managing JavaScript values created during async realm operations.
/// All deferred disposables are automatically disposed when the scope exits (in LIFO order).
/// </summary>
/// <param name="realm">The realm in which to execute the action.</param>
/// <param name="action">An async action that receives the realm and a scope for deferring disposables.</param>
/// <returns>A task that completes when the action finishes.</returns>
/// <exception cref="ArgumentNullException"><paramref name="realm"/> or <paramref name="action"/> is <c>null</c>.</exception>
/// <remarks>
/// <para>
/// This method simplifies managing multiple JavaScript values during async operations.
/// Values are disposed in reverse order (LIFO) when the scope exits.
/// </para>
/// <para>
/// Example:
/// <code>
/// await realm.UseScopeAsync(async (r, scope) =>
/// {
///     var obj = scope.Defer(await r.EvalAsync("({ x: 10, y: 20 })"));
///     var x = scope.Defer(obj.GetProperty("x"));
///     Console.WriteLine($"x = {x.AsNumber()}");
/// });
/// // All deferred values are automatically disposed here
/// </code>
/// </para>
/// </remarks>
public static async Task UseScopeAsync(this Realm realm, Func<Realm, DisposableScope, Task> action)
{
    ArgumentNullException.ThrowIfNull(realm);
    ArgumentNullException.ThrowIfNull(action);
    
    using var scope = new DisposableScope();
    await action(realm, scope);
}

}