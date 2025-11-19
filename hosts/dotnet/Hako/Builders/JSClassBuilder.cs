using HakoJS.Host;
using HakoJS.VM;

namespace HakoJS.Builders;

/// <summary>
/// Provides a fluent API for building JavaScript classes with constructors, methods, properties, and lifecycle hooks.
/// </summary>
/// <remarks>
/// <para>
/// This builder is primarily used by the source generator for [JSClass] types, but can also be used
/// manually to create JavaScript classes at runtime. It supports instance and static members, async methods,
/// property getters/setters, and custom finalizers.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var builder = new JSClassBuilder(realm, "MyClass");
/// builder.SetConstructor((ctx, instance, args, newTarget) =>
/// {
///     // Initialize instance
///     instance.SetOpaque(myData);
///     return null;
/// })
/// .AddMethod("greet", (ctx, thisArg, args) =>
/// {
///     return ctx.NewString("Hello!");
/// })
/// .AddReadOnlyProperty("name", (ctx, thisArg, args) =>
/// {
///     return ctx.NewString("MyClass");
/// });
/// 
/// var jsClass = builder.Build();
/// </code>
/// </para>
/// </remarks>
public sealed class JSClassBuilder
{
    private readonly Realm _context;
    private readonly Dictionary<string, JSFunction> _methods = new();
    private readonly CModuleInitializer? _moduleInitializer;
    private readonly string _name;
    private readonly Dictionary<string, ClassOptions.PropertyDefinition> _properties = new();
    private readonly Dictionary<string, JSFunction> _staticMethods = new();
    private readonly Dictionary<string, ClassOptions.PropertyDefinition> _staticProperties = new();
    private JSConstructor? _constructor;
    private ClassFinalizerHandler? _finalizer;
    private ClassGcMarkHandler? _gcMark;

    /// <summary>
    /// Initializes a new instance of the <see cref="JSClassBuilder"/> class.
    /// </summary>
    /// <param name="context">The realm in which to create the class.</param>
    /// <param name="name">The name of the JavaScript class.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="name"/> is <c>null</c>.</exception>
    public JSClassBuilder(Realm context, string name)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _moduleInitializer = null;
    }

    internal JSClassBuilder(Realm context, string name, CModuleInitializer moduleInitializer)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _moduleInitializer = moduleInitializer ?? throw new ArgumentNullException(nameof(moduleInitializer));
    }

    /// <summary>
    /// Sets the constructor function for the class.
    /// </summary>
    /// <param name="constructor">
    /// A function that receives the realm, instance JSValue, constructor arguments, and new.target,
    /// and returns an optional error JSValue.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="constructor"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// The constructor is called when JavaScript code uses <c>new ClassName(...)</c>.
    /// Return <c>null</c> for success, or return an error JSValue to throw an exception.
    /// </para>
    /// <para>
    /// The instance JSValue typically has an opaque value set to store native data.
    /// </para>
    /// </remarks>
    public JSClassBuilder SetConstructor(JSConstructor constructor)
    {
        _constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
        return this;
    }

    /// <summary>
    /// Sets the constructor function for the class using an action that doesn't return a value.
    /// </summary>
    /// <param name="constructor">
    /// An action that receives the realm, instance JSValue, constructor arguments, and new.target.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="constructor"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Use this overload when the constructor always succeeds and doesn't need to return errors.
    /// </remarks>
    public JSClassBuilder SetConstructor(JSAction constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        _constructor = (ctx, instance, args, newTarget) =>
        {
            constructor(ctx, instance, args);
            return null;
        };
        return this;
    }

    /// <summary>
    /// Builds the JavaScript class with all configured members and options.
    /// </summary>
    /// <returns>A <see cref="JSClass"/> instance representing the built class.</returns>
    /// <exception cref="InvalidOperationException">No constructor has been set.</exception>
    /// <remarks>
    /// <para>
    /// If this builder was created from a module initializer, the class is automatically exported
    /// from the module when built.
    /// </para>
    /// </remarks>
    public JSClass Build()
    {
        if (_constructor == null)
            throw new InvalidOperationException("Constructor must be set before building the class");

        var options = new ClassOptions
        {
            Methods = _methods.Count > 0 ? _methods : null,
            StaticMethods = _staticMethods.Count > 0 ? _staticMethods : null,
            Properties = _properties.Count > 0 ? _properties : null,
            StaticProperties = _staticProperties.Count > 0 ? _staticProperties : null,
            Finalizer = _finalizer,
            GCMark = _gcMark
        };

        var jsClass = new JSClass(_context, _name, _constructor, options);

        // If this builder was created from a CModuleInitializer, auto-export the class
        if (_moduleInitializer != null) _moduleInitializer.CompleteClassExport(jsClass);

        return jsClass;
    }

    #region Instance Methods

    /// <summary>
    /// Adds an instance method to the class that returns a value.
    /// </summary>
    /// <param name="name">The name of the method.</param>
    /// <param name="method">The method implementation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="method"/> is <c>null</c>.</exception>
    public JSClassBuilder AddMethod(string name, JSFunction method)
    {
        _methods[name] = method ?? throw new ArgumentNullException(nameof(method));
        return this;
    }

    /// <summary>
    /// Adds an instance method to the class that does not return a value.
    /// </summary>
    /// <param name="name">The name of the method.</param>
    /// <param name="method">The method implementation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="method"/> is <c>null</c>.</exception>
    public JSClassBuilder AddMethod(string name, JSAction method)
    {
        ArgumentNullException.ThrowIfNull(method);

        _methods[name] = (ctx, thisArg, args) =>
        {
            method(ctx, thisArg, args);
            return ctx.Undefined();
        };
        return this;
    }

    /// <summary>
    /// Adds an asynchronous instance method to the class that returns a Promise.
    /// </summary>
    /// <param name="name">The name of the method.</param>
    /// <param name="method">The async method implementation that returns a <see cref="Task{JSValue}"/>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="method"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// The Task is automatically wrapped in a JavaScript Promise. If the Task is faulted,
    /// the Promise is rejected with the exception. If cancelled, the Promise is rejected with
    /// an <see cref="OperationCanceledException"/>.
    /// </para>
    /// </remarks>
    public JSClassBuilder AddMethodAsync(string name, JSAsyncFunction method)
    {
        ArgumentNullException.ThrowIfNull(method);

        _methods[name] = (ctx, thisArg, args) =>
        {
            var deferred = ctx.NewPromise();
            var task = method(ctx, thisArg, args);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    using var error = ctx.NewError(t.Exception?.GetBaseException() ?? t.Exception!);
                    deferred.Reject(error);
                }
                else if (t.IsCanceled)
                {
                    using var error = ctx.NewError(new OperationCanceledException("Task was canceled"));
                    deferred.Reject(error);
                }
                else
                {
                    using var result = t.Result ?? ctx.Undefined();
                    deferred.Resolve(result);
                }
            }, TaskContinuationOptions.RunContinuationsAsynchronously);

            return deferred.Handle;
        };

        return this;
    }

    /// <summary>
    /// Adds an asynchronous instance method to the class that returns a Promise resolving to <c>undefined</c>.
    /// </summary>
    /// <param name="name">The name of the method.</param>
    /// <param name="method">The async method implementation that returns a <see cref="Task"/>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="method"/> is <c>null</c>.</exception>
    public JSClassBuilder AddMethodAsync(string name, JSAsyncAction method)
    {
        ArgumentNullException.ThrowIfNull(method);

        _methods[name] = (ctx, thisArg, args) =>
        {
            var deferred = ctx.NewPromise();
            var task = method(ctx, thisArg, args);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    using var error = ctx.NewError(t.Exception?.GetBaseException() ?? t.Exception!);
                    deferred.Reject(error);
                }
                else if (t.IsCanceled)
                {
                    using var error = ctx.NewError(new OperationCanceledException("Task was canceled"));
                    deferred.Reject(error);
                }
                else
                {
                    using var result = ctx.Undefined();
                    deferred.Resolve(result);
                }
            }, TaskContinuationOptions.RunContinuationsAsynchronously);

            return deferred.Handle;
        };

        return this;
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Adds a static method to the class that returns a value.
    /// </summary>
    /// <param name="name">The name of the static method.</param>
    /// <param name="method">The method implementation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="method"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Static methods are accessible on the constructor function itself, not on instances.
    /// JavaScript usage: <c>ClassName.methodName()</c>
    /// </remarks>
    public JSClassBuilder AddStaticMethod(string name, JSFunction method)
    {
        _staticMethods[name] = method ?? throw new ArgumentNullException(nameof(method));
        return this;
    }

    /// <summary>
    /// Adds a static method to the class that does not return a value.
    /// </summary>
    /// <param name="name">The name of the static method.</param>
    /// <param name="method">The method implementation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="method"/> is <c>null</c>.</exception>
    public JSClassBuilder AddStaticMethod(string name, JSAction method)
    {
        ArgumentNullException.ThrowIfNull(method);

        _staticMethods[name] = (ctx, thisArg, args) =>
        {
            method(ctx, thisArg, args);
            return ctx.Undefined();
        };
        return this;
    }

    /// <summary>
    /// Adds an asynchronous static method to the class that returns a Promise.
    /// </summary>
    /// <param name="name">The name of the static method.</param>
    /// <param name="method">The async method implementation that returns a <see cref="Task{JSValue}"/>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="method"/> is <c>null</c>.</exception>
    public JSClassBuilder AddStaticMethodAsync(string name, JSAsyncFunction method)
    {
        ArgumentNullException.ThrowIfNull(method);

        _staticMethods[name] = (ctx, thisArg, args) =>
        {
            var deferred = ctx.NewPromise();
            var task = method(ctx, thisArg, args);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    using var error = ctx.NewError(t.Exception?.GetBaseException() ?? t.Exception!);
                    deferred.Reject(error);
                }
                else if (t.IsCanceled)
                {
                    using var error = ctx.NewError(new OperationCanceledException("Task was canceled"));
                    deferred.Reject(error);
                }
                else
                {
                    using var result = t.Result ?? ctx.Undefined();
                    deferred.Resolve(result);
                }
            }, TaskContinuationOptions.RunContinuationsAsynchronously);

            return deferred.Handle;
        };

        return this;
    }

    /// <summary>
    /// Adds an asynchronous static method to the class that returns a Promise resolving to <c>undefined</c>.
    /// </summary>
    /// <param name="name">The name of the static method.</param>
    /// <param name="method">The async method implementation that returns a <see cref="Task"/>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="method"/> is <c>null</c>.</exception>
    public JSClassBuilder AddStaticMethodAsync(string name, JSAsyncAction method)
    {
        ArgumentNullException.ThrowIfNull(method);

        _staticMethods[name] = (ctx, thisArg, args) =>
        {
            var deferred = ctx.NewPromise();
            var task = method(ctx, thisArg, args);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    using var error = ctx.NewError(t.Exception?.GetBaseException() ?? t.Exception!);
                    deferred.Reject(error);
                }
                else if (t.IsCanceled)
                {
                    using var error = ctx.NewError(new OperationCanceledException("Task was canceled"));
                    deferred.Reject(error);
                }
                else
                {
                    using var result = ctx.Undefined();
                    deferred.Resolve(result);
                }
            }, TaskContinuationOptions.RunContinuationsAsynchronously);

            return deferred.Handle;
        };

        return this;
    }

    #endregion

    #region Instance Properties

    /// <summary>
    /// Adds an instance property with optional getter and setter.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="getter">The getter function. Must not be <c>null</c>.</param>
    /// <param name="setter">An optional setter function. If <c>null</c>, the property is read-only.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="getter"/> is <c>null</c>.</exception>
    public JSClassBuilder AddProperty(
        string name,
        JSFunction getter,
        JSFunction? setter = null,
        bool enumerable = true,
        bool configurable = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Property name cannot be null or whitespace", nameof(name));
        ArgumentNullException.ThrowIfNull(getter);

        _properties[name] = new ClassOptions.PropertyDefinition
        {
            Name = name,
            Getter = getter,
            Setter = setter,
            Enumerable = enumerable,
            Configurable = configurable
        };
        return this;
    }

    /// <summary>
    /// Adds a read-only instance property.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="getter">The getter function.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="getter"/> is <c>null</c>.</exception>
    public JSClassBuilder AddReadOnlyProperty(
        string name,
        JSFunction getter,
        bool enumerable = true,
        bool configurable = true)
    {
        return AddProperty(name, getter, null, enumerable, configurable);
    }

    /// <summary>
    /// Adds a read-write instance property with both getter and setter.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="getter">The getter function.</param>
    /// <param name="setter">The setter function.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="getter"/>, or <paramref name="setter"/> is <c>null</c>.</exception>
    public JSClassBuilder AddReadWriteProperty(
        string name,
        JSFunction getter,
        JSFunction setter,
        bool enumerable = true,
        bool configurable = true)
    {
        return AddProperty(name, getter, setter, enumerable, configurable);
    }

    #endregion

    #region Static Properties

    /// <summary>
    /// Adds a static property with optional getter and setter.
    /// </summary>
    /// <param name="name">The name of the static property.</param>
    /// <param name="getter">The getter function. Must not be <c>null</c>.</param>
    /// <param name="setter">An optional setter function. If <c>null</c>, the property is read-only.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="getter"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Static properties are accessible on the constructor function itself, not on instances.
    /// JavaScript usage: <c>ClassName.propertyName</c>
    /// </remarks>
    public JSClassBuilder AddStaticProperty(
        string name,
        JSFunction getter,
        JSFunction? setter = null,
        bool enumerable = true,
        bool configurable = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Property name cannot be null or whitespace", nameof(name));
        ArgumentNullException.ThrowIfNull(getter);

        _staticProperties[name] = new ClassOptions.PropertyDefinition
        {
            Name = name,
            Getter = getter,
            Setter = setter,
            Enumerable = enumerable,
            Configurable = configurable
        };
        return this;
    }

    /// <summary>
    /// Adds a read-only static property.
    /// </summary>
    /// <param name="name">The name of the static property.</param>
    /// <param name="getter">The getter function.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="getter"/> is <c>null</c>.</exception>
    public JSClassBuilder AddReadOnlyStaticProperty(
        string name,
        JSFunction getter,
        bool enumerable = true,
        bool configurable = true)
    {
        return AddStaticProperty(name, getter, null, enumerable, configurable);
    }

    /// <summary>
    /// Adds a read-write static property with both getter and setter.
    /// </summary>
    /// <param name="name">The name of the static property.</param>
    /// <param name="getter">The getter function.</param>
    /// <param name="setter">The setter function.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="getter"/>, or <paramref name="setter"/> is <c>null</c>.</exception>
    public JSClassBuilder AddReadWriteStaticProperty(
        string name,
        JSFunction getter,
        JSFunction setter,
        bool enumerable = true,
        bool configurable = true)
    {
        return AddStaticProperty(name, getter, setter, enumerable, configurable);
    }

    #endregion

    #region Finalizer and GC

    /// <summary>
    /// Sets a finalizer callback that is invoked when a JavaScript instance is garbage collected.
    /// </summary>
    /// <param name="finalizer">The finalizer handler that receives the runtime, opaque data, and class ID.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Finalizers are typically used to clean up native resources or remove instances from tracking dictionaries
    /// when the JavaScript object is no longer reachable and is being collected.
    /// </para>
    /// <para>
    /// The finalizer is called on the runtime's garbage collection thread, not the event loop thread.
    /// </para>
    /// </remarks>
    public JSClassBuilder SetFinalizer(ClassFinalizerHandler finalizer)
    {
        _finalizer = finalizer;
        return this;
    }

    /// <summary>
    /// Sets a GC mark callback for tracing additional JavaScript values during garbage collection.
    /// </summary>
    /// <param name="gcMark">The GC mark handler that receives the runtime and opaque data.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The GC mark handler should mark any JavaScript values that are reachable from the native
    /// object to prevent them from being collected prematurely.
    /// </para>
    /// <para>
    /// This is an advanced feature typically only needed when native objects hold strong references
    /// to JavaScript values that aren't otherwise visible to the garbage collector.
    /// </para>
    /// </remarks>
    public JSClassBuilder SetGCMark(ClassGcMarkHandler gcMark)
    {
        _gcMark = gcMark;
        return this;
    }

    #endregion
}