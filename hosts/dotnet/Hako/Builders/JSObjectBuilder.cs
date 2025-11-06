using HakoJS.Host;
using HakoJS.VM;

namespace HakoJS.Builders;

/// <summary>
/// Provides a fluent API for building JavaScript objects with properties, functions, and custom descriptors.
/// </summary>
/// <remarks>
/// <para>
/// This builder allows you to create JavaScript objects with precise control over property attributes
/// (writable, enumerable, configurable), including support for read-only, hidden, and locked properties.
/// It also supports Object.seal() and Object.freeze() operations.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var obj = realm.BuildObject()
///     .WithProperty("name", "John")
///     .WithReadOnly("version", "1.0.0")
///     .WithFunction("greet", (ctx, _, args) => 
///     {
///         return ctx.NewString("Hello!");
///     })
///     .Build();
/// 
/// // JavaScript can now use:
/// // obj.name = "Jane";  // writable
/// // obj.version = "2.0";  // throws in strict mode
/// // obj.greet();  // "Hello!"
/// </code>
/// </para>
/// </remarks>
public sealed class JSObjectBuilder : IDisposable
{
    private readonly Realm _context;
    private readonly List<IPropertyEntry> _properties;
    private bool _built;
    private bool _disposed;
    private bool _frozen;
    private JSValue? _prototype;
    private bool _sealed;

    private JSObjectBuilder(Realm context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _properties = [];
    }

    /// <summary>
    /// Releases all resources used by the <see cref="JSObjectBuilder"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    /// <summary>
    /// Creates a new <see cref="JSObjectBuilder"/> for the specified realm.
    /// </summary>
    /// <param name="context">The realm in which to create objects.</param>
    /// <returns>A new <see cref="JSObjectBuilder"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
    public static JSObjectBuilder Create(Realm context)
    {
        return new JSObjectBuilder(context);
    }

    /// <summary>
    /// Adds a property with custom descriptor attributes.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="key">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <param name="writable">Whether the property value can be changed.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The builder has already been built or disposed.</exception>
    public JSObjectBuilder WithDescriptor<T>(
        string key,
        T value,
        bool writable = true,
        bool enumerable = true,
        bool configurable = true)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<T>(key, value, writable, enumerable, configurable));
        return this;
    }

    /// <summary>
    /// Adds a function property that returns a value.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="callback">The function implementation.</param>
    /// <param name="writable">Whether the property can be reassigned.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The builder has already been built or disposed.</exception>
    public JSObjectBuilder WithFunction(
        string key,
        JSFunction callback,
        bool writable = true,
        bool enumerable = true,
        bool configurable = true)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new FunctionPropertyEntry(
            key,
            callback,
            writable,
            enumerable,
            configurable,
            key));
        return this;
    }

    /// <summary>
    /// Adds a function property that does not return a value.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="callback">The function implementation.</param>
    /// <param name="writable">Whether the property can be reassigned.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The builder has already been built or disposed.</exception>
    public JSObjectBuilder WithFunction(
        string key,
        JSAction callback,
        bool writable = true,
        bool enumerable = true,
        bool configurable = true)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new ActionFunctionPropertyEntry(
            key,
            callback,
            writable,
            enumerable,
            configurable,
            key));
        return this;
    }

    /// <summary>
    /// Adds a read-only function property.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="callback">The function implementation that returns a value.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public JSObjectBuilder WithReadOnlyFunction(
        string key,
        JSFunction callback)
    {
        return WithFunction(key, callback, false);
    }

    /// <summary>
    /// Adds a read-only function property that does not return a value.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="callback">The function implementation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public JSObjectBuilder WithReadOnlyFunction(
        string key,
        JSAction callback)
    {
        return WithFunction(key, callback, false);
    }

    /// <summary>
    /// Adds an asynchronous function property that returns a Promise.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="callback">The async function implementation.</param>
    /// <param name="writable">Whether the property can be reassigned.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The builder has already been built or disposed.</exception>
    public JSObjectBuilder WithFunctionAsync(
        string key,
        JSAsyncFunction callback,
        bool writable = true,
        bool enumerable = true,
        bool configurable = true)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new AsyncFunctionPropertyEntry(
            key,
            callback,
            writable,
            enumerable,
            configurable,
            key));
        return this;
    }

    /// <summary>
    /// Adds an asynchronous function property that returns a Promise resolving to <c>undefined</c>.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="callback">The async function implementation.</param>
    /// <param name="writable">Whether the property can be reassigned.</param>
    /// <param name="enumerable">Whether the property appears in for-in loops and Object.keys().</param>
    /// <param name="configurable">Whether the property can be deleted or its attributes changed.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The builder has already been built or disposed.</exception>
    public JSObjectBuilder WithFunctionAsync(
        string key,
        JSAsyncAction callback,
        bool writable = true,
        bool enumerable = true,
        bool configurable = true)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new AsyncVoidFunctionPropertyEntry(
            key,
            callback,
            writable,
            enumerable,
            configurable,
            key));
        return this;
    }

    /// <summary>
    /// Adds a read-only asynchronous function property.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="callback">The async function implementation that returns a value.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public JSObjectBuilder WithReadOnlyFunctionAsync(
        string key,
        JSAsyncFunction callback)
    {
        return WithFunctionAsync(key, callback, false);
    }

    /// <summary>
    /// Adds a read-only asynchronous function property that does not return a value.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="callback">The async function implementation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public JSObjectBuilder WithReadOnlyFunctionAsync(
        string key,
        JSAsyncAction callback)
    {
        return WithFunctionAsync(key, callback, false);
    }

    /// <summary>
    /// Adds multiple properties from a collection of key-value pairs.
    /// </summary>
    /// <typeparam name="T">The type of the property values.</typeparam>
    /// <param name="properties">The collection of properties to add.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="properties"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The builder has already been built or disposed.</exception>
    /// <remarks>
    /// All properties added through this method are writable, enumerable, and configurable.
    /// </remarks>
    public JSObjectBuilder WithProperties<T>(IEnumerable<KeyValuePair<string, T>> properties)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (properties == null)
            throw new ArgumentNullException(nameof(properties));

        foreach (var kvp in properties)
            _properties.Add(new PropertyEntry<T>(
                kvp.Key,
                kvp.Value,
                true,
                true,
                true));

        return this;
    }

    /// <summary>
    /// Sets a custom prototype for the object being built.
    /// </summary>
    /// <param name="prototype">The prototype object.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prototype"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The builder has already been built or disposed.</exception>
    /// <remarks>
    /// By default, objects are created with <c>Object.prototype</c>. Use this method to create
    /// objects with custom inheritance chains.
    /// </remarks>
    public JSObjectBuilder WithPrototype(JSValue prototype)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        _prototype = prototype ?? throw new ArgumentNullException(nameof(prototype));
        return this;
    }

    /// <summary>
    /// Marks the object to be sealed after construction.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder has already been built or disposed.</exception>
    /// <remarks>
    /// <para>
    /// A sealed object prevents new properties from being added and marks all existing properties
    /// as non-configurable. Property values can still be changed if they are writable.
    /// </para>
    /// <para>
    /// This is equivalent to calling <c>Object.seal()</c> on the object.
    /// </para>
    /// </remarks>
    public JSObjectBuilder Sealed()
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        _sealed = true;
        return this;
    }

    /// <summary>
    /// Marks the object to be frozen after construction.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder has already been built or disposed.</exception>
    /// <remarks>
    /// <para>
    /// A frozen object prevents new properties from being added, existing properties from being removed,
    /// and all property values from being changed. The object becomes completely immutable.
    /// </para>
    /// <para>
    /// This is equivalent to calling <c>Object.freeze()</c> on the object. Frozen implies sealed.
    /// </para>
    /// </remarks>
    public JSObjectBuilder Frozen()
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        _frozen = true;
        _sealed = true; // Frozen implies sealed
        return this;
    }

    /// <summary>
    /// Builds and returns the configured JavaScript object.
    /// </summary>
    /// <returns>A <see cref="JSObject"/> containing the built object.</returns>
    /// <exception cref="InvalidOperationException">
    /// The builder has already been built, disposed, or an error occurred during construction.
    /// </exception>
    /// <remarks>
    /// <para>
    /// After calling this method, the builder cannot be modified further. All configured properties
    /// are added to the object with their specified attributes, and seal/freeze operations are applied
    /// if requested.
    /// </para>
    /// <para>
    /// The returned <see cref="JSObject"/> owns the underlying <see cref="JSValue"/> and should be
    /// disposed when no longer needed.
    /// </para>
    /// </remarks>
    public JSObject Build()
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        _built = true;

        var vmValue = _prototype != null
            ? _context.NewObjectWithPrototype(_prototype)
            : _context.NewObject();

        try
        {
            // Add all properties with their descriptors
            foreach (var entry in _properties)
            {
                var (propValue, shouldDispose) = entry.GetValue(_context);

                try
                {
                    // If all attributes are default (true), use simple SetProperty
                    if (entry is { Writable: true, Enumerable: true, Configurable: true })
                        vmValue.SetProperty(entry.Key, propValue);
                    else
                        // Use native defineProperty for custom descriptors
                        DefineProperty(vmValue, entry.Key, propValue, entry);
                }
                finally
                {
                    if (shouldDispose) propValue.Dispose();
                }
            }

            // Apply seal/freeze if requested
            if (_frozen)
                FreezeObject(vmValue);
            else if (_sealed) SealObject(vmValue);

            return new JSObject(_context, vmValue);
        }
        catch
        {
            vmValue.Dispose();
            throw;
        }
    }

    private void DefineProperty(JSValue obj, string key, JSValue value, IPropertyEntry entry)
    {
        using var keyValue = _context.NewString(key);

        var result = _context.Runtime.Registry.DefineProp(
            _context.Pointer,
            obj.GetHandle(), // this_obj
            keyValue.GetHandle(), // prop_name (string)
            value.GetHandle(), // prop_value (data)
            _context.Runtime.Registry.GetUndefined(), // get
            _context.Runtime.Registry.GetUndefined(), // set
            entry.Configurable ? 1 : 0, // configurable (presence+value handled in native)
            entry.Enumerable ? 1 : 0, // enumerable
            1, // hasValue (we always pass a value here)
            1, // hasWritable (we always specify writability)
            entry.Writable ? 1 : 0 // writable
        );

        if (result == -1)
        {
            var error = _context.GetLastError();
            if (error is not null) throw new InvalidOperationException($"Failed to define property '{key}'", error);
            throw new InvalidOperationException($"Failed to define property '{key}': unknown error");
        }

        if (result == 0)
            throw new InvalidOperationException($"Failed to define property '{key}': operation returned FALSE");
    }

    private void SealObject(JSValue obj)
    {
        using var globalObj = _context.GetGlobalObject();
        using var objectConstructor = globalObj.GetProperty("Object");
        using var sealFunc = objectConstructor.GetProperty("seal");
        using var result = _context.CallFunction(sealFunc, _context.Undefined(), obj);

        if (result.TryGetFailure(out var error))
            using (error)
            {
                throw new InvalidOperationException($"Failed to seal object: {error.AsString()}");
            }
    }

    private void FreezeObject(JSValue obj)
    {
        using var globalObj = _context.GetGlobalObject();
        using var objectConstructor = globalObj.GetProperty("Object");
        using var freezeFunc = objectConstructor.GetProperty("freeze");
        using var result = _context.CallFunction(freezeFunc, _context.Undefined(), obj);

        if (result.TryGetFailure(out var error))
            using (error)
            {
                throw new InvalidOperationException($"Failed to freeze object: {error.AsString()}");
            }
    }

    private void ThrowIfBuilt()
    {
        if (_built)
            throw new InvalidOperationException("Cannot modify builder after Build() has been called");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(JSObjectBuilder));
    }

    #region String Properties

    /// <summary>
    /// Adds a writable string property.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="value">The string value.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public JSObjectBuilder WithProperty(string key, string value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<string>(
            key,
            value,
            true,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a read-only string property.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="value">The string value.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// Read-only properties cannot be reassigned but are still enumerable and configurable.
    /// </remarks>
    public JSObjectBuilder WithReadOnly(string key, string value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<string>(
            key,
            value,
            false,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a hidden string property that doesn't appear in enumerations.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="value">The string value.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// Hidden properties are writable and configurable but don't appear in for-in loops or Object.keys().
    /// </remarks>
    public JSObjectBuilder WithHidden(string key, string value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<string>(
            key,
            value,
            true,
            false,
            true));
        return this;
    }

    #endregion

    #region Int Properties

    /// <summary>
    /// Adds a writable integer property.
    /// </summary>
    public JSObjectBuilder WithProperty(string key, int value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<int>(
            key,
            value,
            true,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a read-only integer property.
    /// </summary>
    public JSObjectBuilder WithReadOnly(string key, int value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<int>(
            key,
            value,
            false,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a hidden integer property that doesn't appear in enumerations.
    /// </summary>
    public JSObjectBuilder WithHidden(string key, int value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<int>(
            key,
            value,
            true,
            false,
            true));
        return this;
    }

    #endregion

    #region Long Properties

    /// <summary>
    /// Adds a writable long integer property.
    /// </summary>
    public JSObjectBuilder WithProperty(string key, long value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<long>(
            key,
            value,
            true,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a read-only long integer property.
    /// </summary>
    public JSObjectBuilder WithReadOnly(string key, long value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<long>(
            key,
            value,
            false,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a hidden long integer property that doesn't appear in enumerations.
    /// </summary>
    public JSObjectBuilder WithHidden(string key, long value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<long>(
            key,
            value,
            true,
            false,
            true));
        return this;
    }

    #endregion

    #region Double Properties

    /// <summary>
    /// Adds a writable double-precision floating-point property.
    /// </summary>
    public JSObjectBuilder WithProperty(string key, double value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<double>(
            key,
            value,
            true,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a read-only double-precision floating-point property.
    /// </summary>
    public JSObjectBuilder WithReadOnly(string key, double value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<double>(
            key,
            value,
            false,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a hidden double-precision floating-point property that doesn't appear in enumerations.
    /// </summary>
    public JSObjectBuilder WithHidden(string key, double value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<double>(
            key,
            value,
            true,
            false,
            true));
        return this;
    }

    #endregion

    #region Bool Properties

    /// <summary>
    /// Adds a writable boolean property.
    /// </summary>
    public JSObjectBuilder WithProperty(string key, bool value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<bool>(
            key,
            value,
            true,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a read-only boolean property.
    /// </summary>
    public JSObjectBuilder WithReadOnly(string key, bool value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<bool>(
            key,
            value,
            false,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a hidden boolean property that doesn't appear in enumerations.
    /// </summary>
    public JSObjectBuilder WithHidden(string key, bool value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<bool>(
            key,
            value,
            true,
            false,
            true));
        return this;
    }

    #endregion

    #region JSValue Properties

    /// <summary>
    /// Adds a writable JSValue property.
    /// </summary>
    public JSObjectBuilder WithProperty(string key, JSValue value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _properties.Add(new PropertyEntry<JSValue>(
            key,
            value,
            true,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a read-only JSValue property.
    /// </summary>
    public JSObjectBuilder WithReadOnly(string key, JSValue value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _properties.Add(new PropertyEntry<JSValue>(
            key,
            value,
            false,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a hidden JSValue property that doesn't appear in enumerations.
    /// </summary>
    public JSObjectBuilder WithHidden(string key, JSValue value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _properties.Add(new PropertyEntry<JSValue>(
            key,
            value,
            true,
            false,
            true));
        return this;
    }

    #endregion

    #region JSObject Properties

    /// <summary>
    /// Adds a writable JSObject property.
    /// </summary>
    public JSObjectBuilder WithProperty(string key, JSObject value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _properties.Add(new PropertyEntry<JSObject>(
            key,
            value,
            true,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a read-only JSObject property.
    /// </summary>
    public JSObjectBuilder WithReadOnly(string key, JSObject value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _properties.Add(new PropertyEntry<JSObject>(
            key,
            value,
            false,
            true,
            true));
        return this;
    }

    /// <summary>
    /// Adds a hidden JSObject property that doesn't appear in enumerations.
    /// </summary>
    public JSObjectBuilder WithHidden(string key, JSObject value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _properties.Add(new PropertyEntry<JSObject>(
            key,
            value,
            true,
            false,
            true));
        return this;
    }

    #endregion

    #region Locked Properties

    /// <summary>
    /// Adds a locked string property that cannot be deleted or reconfigured.
    /// </summary>
    /// <remarks>
    /// Locked properties are writable and enumerable but have configurable set to false,
    /// preventing deletion and attribute changes.
    /// </remarks>
    public JSObjectBuilder WithLocked(string key, string value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<string>(
            key,
            value,
            true,
            true,
            false));
        return this;
    }

    /// <summary>
    /// Adds a locked integer property that cannot be deleted or reconfigured.
    /// </summary>
    public JSObjectBuilder WithLocked(string key, int value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<int>(
            key,
            value,
            true,
            true,
            false));
        return this;
    }

    /// <summary>
    /// Adds a locked long integer property that cannot be deleted or reconfigured.
    /// </summary>
    public JSObjectBuilder WithLocked(string key, long value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<long>(
            key,
            value,
            true,
            true,
            false));
        return this;
    }

    /// <summary>
    /// Adds a locked double-precision floating-point property that cannot be deleted or reconfigured.
    /// </summary>
    public JSObjectBuilder WithLocked(string key, double value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<double>(
            key,
            value,
            true,
            true,
            false));
        return this;
    }

    /// <summary>
    /// Adds a locked boolean property that cannot be deleted or reconfigured.
    /// </summary>
    public JSObjectBuilder WithLocked(string key, bool value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        _properties.Add(new PropertyEntry<bool>(
            key,
            value,
            true,
            true,
            false));
        return this;
    }

    /// <summary>
    /// Adds a locked JSValue property that cannot be deleted or reconfigured.
    /// </summary>
    public JSObjectBuilder WithLocked(string key, JSValue value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _properties.Add(new PropertyEntry<JSValue>(
            key,
            value,
            true,
            true,
            false));
        return this;
    }

    /// <summary>
    /// Adds a locked JSObject property that cannot be deleted or reconfigured.
    /// </summary>
    public JSObjectBuilder WithLocked(string key, JSObject value)
    {
        ThrowIfBuilt();
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _properties.Add(new PropertyEntry<JSObject>(
            key,
            value,
            true,
            true,
            false));
        return this;
    }

    #endregion

    #region Property Entry Types

    private interface IPropertyEntry
    {
        string Key { get; }
        bool Writable { get; }
        bool Enumerable { get; }
        bool Configurable { get; }

        (JSValue value, bool shouldDispose) GetValue(Realm context);
    }

    private readonly struct PropertyEntry<T>(
        string key,
        T value,
        bool writable,
        bool enumerable,
        bool configurable)
        : IPropertyEntry
    {
        public string Key { get; } = key;
        private T Value { get; } = value;
        public bool Writable { get; } = writable;
        public bool Enumerable { get; } = enumerable;
        public bool Configurable { get; } = configurable;

        public (JSValue value, bool shouldDispose) GetValue(Realm context)
        {
            return Value switch
            {
                // Handle JSValue - use directly without conversion
                JSValue jsValue => (jsValue, false),
                // Handle JSObject - use its underlying value
                JSObject jsObject => (jsObject.Value(), false),
                _ => (context.NewValue(Value), true)
            };

            // Handle all other types - convert to JSValue
        }
    }

    private readonly struct FunctionPropertyEntry(
        string key,
        JSFunction callback,
        bool writable,
        bool enumerable,
        bool configurable,
        string functionName)
        : IPropertyEntry
    {
        public string Key { get; } = key;
        public bool Writable { get; } = writable;
        public bool Enumerable { get; } = enumerable;
        public bool Configurable { get; } = configurable;

        public (JSValue value, bool shouldDispose) GetValue(Realm context)
        {
            return (context.NewFunction(functionName, callback), true);
        }
    }

    private readonly struct ActionFunctionPropertyEntry(
        string key,
        JSAction callback,
        bool writable,
        bool enumerable,
        bool configurable,
        string functionName)
        : IPropertyEntry
    {
        public string Key { get; } = key;
        public bool Writable { get; } = writable;
        public bool Enumerable { get; } = enumerable;
        public bool Configurable { get; } = configurable;

        public (JSValue value, bool shouldDispose) GetValue(Realm context)
        {
            return (context.NewFunction(functionName, callback), true);
        }
    }

    private readonly struct AsyncFunctionPropertyEntry(
        string key,
        JSAsyncFunction callback,
        bool writable,
        bool enumerable,
        bool configurable,
        string functionName)
        : IPropertyEntry
    {
        public string Key { get; } = key;
        public bool Writable { get; } = writable;
        public bool Enumerable { get; } = enumerable;
        public bool Configurable { get; } = configurable;

        public (JSValue value, bool shouldDispose) GetValue(Realm context)
        {
            return (context.NewFunctionAsync(functionName, callback), true);
        }
    }

    private readonly struct AsyncVoidFunctionPropertyEntry(
        string key,
        JSAsyncAction callback,
        bool writable,
        bool enumerable,
        bool configurable,
        string functionName)
        : IPropertyEntry
    {
        public string Key { get; } = key;
        public bool Writable { get; } = writable;
        public bool Enumerable { get; } = enumerable;
        public bool Configurable { get; } = configurable;

        public (JSValue value, bool shouldDispose) GetValue(Realm context)
        {
            return (context.NewFunctionAsync(functionName, callback), true);
        }
    }

    #endregion
}