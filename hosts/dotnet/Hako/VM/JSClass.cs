using HakoJS.Exceptions;
using HakoJS.Host;

namespace HakoJS.VM;

/// <summary>
/// Represents a JavaScript class with constructor, prototype, methods, and properties that can be instantiated from JavaScript.
/// </summary>
/// <remarks>
/// <para>
/// This class bridges the gap between .NET and JavaScript class systems, allowing you to define
/// JavaScript classes with native implementations. It manages the class ID, constructor function,
/// prototype object, and instance creation.
/// </para>
/// <para>
/// Classes created with <see cref="JSClass"/> support:
/// <list type="bullet">
/// <item>Constructor functions with custom logic</item>
/// <item>Instance and static methods</item>
/// <item>Property getters and setters</item>
/// <item>Finalizers for resource cleanup</item>
/// <item>GC mark handlers for memory management</item>
/// <item>Opaque data storage for native state</item>
/// </list>
/// </para>
/// <para>
/// Example:
/// <code>
/// var options = new ClassOptions
/// {
///     Methods = new Dictionary&lt;string, Func&lt;Realm, JSValue, JSValue[], JSValue?&gt;&gt;
///     {
///         ["greet"] = (ctx, thisArg, args) => ctx.NewString("Hello!")
///     }
/// };
/// 
/// var jsClass = new JSClass(realm, "Person", (ctx, instance, args, newTarget) =>
/// {
///     // Initialize instance
///     var name = args.Length > 0 ? args[0].AsString() : "Anonymous";
///     instance.SetProperty("name", name);
///     return null; // Success
/// }, options);
/// 
/// jsClass.RegisterGlobal();
/// 
/// // JavaScript can now:
/// // const person = new Person("Alice");
/// // person.greet(); // "Hello!"
/// </code>
/// </para>
/// </remarks>
public class JSClass : IDisposable
{
    private readonly Realm _context;
    private JSValue? _ctorFunction;
    private bool _disposed;
    private string _name;
    private JSValue? _proto;
    
    /// <summary>
    /// Gets the realm in which this class was created.
    /// </summary>
    internal Realm Context => _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="JSClass"/> class.
    /// </summary>
    /// <param name="context">The realm in which to create the class.</param>
    /// <param name="name">The name of the JavaScript class (used for the constructor name).</param>
    /// <param name="constructorFn">
    /// The constructor function that initializes new instances. Receives the realm, instance,
    /// constructor arguments, and new.target. Return <c>null</c> for success, or a <see cref="JSValue"/>
    /// to replace the auto-created instance.
    /// </param>
    /// <param name="options">Optional configuration for methods, properties, and lifecycle hooks.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="name"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="HakoException">
    /// Failed to allocate a class ID, create the prototype, or set up the constructor.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This constructor is typically used internally. Most users should use <see cref="JSClassBuilder"/>
    /// for a more convenient fluent API, or source-generated classes via the [JSClass] attribute.
    /// </para>
    /// <para>
    /// The constructor function is called when JavaScript code uses <c>new ClassName(...)</c>.
    /// The function receives a pre-created instance with opaque storage ready for native data.
    /// </para>
    /// </remarks>
    internal JSClass(
        Realm context,
        string name,
        JSConstructor constructorFn,
        ClassOptions? options = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        options ??= new ClassOptions();

        using var classIdPtr = context.AllocatePointerArray(1);
        context.WritePointerToArray(classIdPtr, 0, 0);

        var classId = context.Runtime.Registry.NewClassID(classIdPtr);
        if (classId == 0) throw new HakoException($"Failed to allocate class ID for: {name}");
        Id = classId;

        try
        {
            SetupClass(name, options);

            // Register constructor wrapper
            ClassConstructorHandler internalConstructor = (ctx, newTarget, args, _) =>
            {
                JSValue? instance = null;
                try
                {
                    // For inheritance: check if newTarget has a custom prototype
                    var isSubclass = false;

                    if (isSubclass)
                    {
                        // Subclass constructor: get prototype from newTarget
                        using var protoProperty = newTarget.GetProperty("prototype");
                        instance = CreateInstanceWithPrototype(protoProperty);
                    }
                    else
                    {
                        // Regular constructor: use the registered class prototype
                        instance = CreateInstance();
                    }

                    // Call user's constructor function to initialize the instance
                    var returnedValue = constructorFn(ctx, instance, args, newTarget);
                
                    // If constructor returns a value, use that instead of the auto-created instance
                    if (returnedValue != null)
                    {
                        instance?.Dispose(); // Dispose the auto-created instance
                        return returnedValue;
                    }
                
                    return instance;
                }
                catch
                {
                    instance?.Dispose();
                    throw;
                }
            };

            context.Runtime.Callbacks.RegisterClassConstructor(Id, internalConstructor);

            if (options.Finalizer != null) context.Runtime.Callbacks.RegisterClassFinalizer(Id, options.Finalizer);

            // Register GC mark handler if provided
            if (options.GCMark != null) context.Runtime.Callbacks.RegisterClassGcMark(Id, options.GCMark);
        }
        catch
        {
            // Clean up on construction failure
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the unique identifier for this class within the QuickJS runtime.
    /// </summary>
    /// <value>
    /// An integer class ID used internally by QuickJS to identify instances of this class.
    /// </value>
    public JSClassID Id { get; }

    /// <summary>
    /// Gets the name of the JavaScript class.
    /// </summary>
    /// <value>
    /// The class name as it appears in JavaScript (e.g., the name shown in error messages
    /// and used for the constructor function).
    /// </value>
    public string Name { get; }

    /// <summary>
    /// Gets the JavaScript constructor function for this class.
    /// </summary>
    /// <value>
    /// A <see cref="JSValue"/> representing the constructor function that can be called
    /// with <c>new</c> to create instances.
    /// </value>
    /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
    /// <exception cref="HakoException">The constructor was not properly initialized.</exception>
    /// <remarks>
    /// This is typically exposed to JavaScript via <see cref="RegisterGlobal"/> or by setting
    /// it as a property on an object. Static methods and properties are attached to this constructor.
    /// </remarks>
    public JSValue Constructor
    {
        get
        {
            CheckDisposed();
            if (_ctorFunction == null) throw new HakoException("Constructor not initialized");
            return _ctorFunction;
        }
    }

    /// <summary>
    /// Gets the JavaScript prototype object for this class.
    /// </summary>
    /// <value>
    /// A <see cref="JSValue"/> representing the prototype object that all instances inherit from.
    /// </value>
    /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
    /// <exception cref="HakoException">The prototype was not properly initialized.</exception>
    /// <remarks>
    /// Instance methods and properties are attached to this prototype object.
    /// All instances created with <see cref="CreateInstance"/> inherit from this prototype.
    /// </remarks>
    public JSValue Prototype
    {
        get
        {
            CheckDisposed();
            if (_proto == null) throw new HakoException("Prototype not initialized");
            return _proto;
        }
    }

    /// <summary>
    /// Disposes the class and all associated resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This unregisters all callbacks (constructor, finalizer, GC mark), disposes the constructor
    /// and prototype objects, and releases the class ID.
    /// </para>
    /// <para>
    /// After disposal, the class cannot be used to create new instances, but existing instances
    /// remain valid until they are garbage collected.
    /// </para>
    /// <para>
    /// Errors during callback unregistration are logged but do not throw exceptions.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        // Dispose managed state (managed objects)
        try
        {
            _context.Runtime.Callbacks.UnregisterClassConstructor(Id);
            _context.Runtime.Callbacks.UnregisterClassFinalizer(Id);
            _context.Runtime.Callbacks.UnregisterClassGcMark(Id);
        }
        catch (Exception error)
        {
            // Log but don't throw during dispose
            Console.Error.WriteLine($"Error unregistering callbacks for JSClass {Id}: {error}");
        }

        // Cascade dispose calls
        _ctorFunction?.Dispose();
        _ctorFunction = null;

        _proto?.Dispose();
        _proto = null;

        _disposed = true;
    }

    private void SetupClass(string name, ClassOptions options)
    {
        // Create the prototype object
        var protoPtr = _context.Runtime.Registry.NewObject(_context.Pointer);
        var protoError = _context.GetLastError(protoPtr);
        if (protoError != null) throw new HakoException($"Prototype creation exception for {name}", protoError);

        _proto = new JSValue(_context, protoPtr);

        // Add instance methods to prototype
        if (options.Methods != null)
            foreach (var kvp in options.Methods)
            {
                using var method = _context.NewFunction(kvp.Key, kvp.Value);
                var methodError = _context.GetLastError(method.GetHandle());
                if (methodError != null) throw new HakoException($"Failed to create method {kvp.Key}", methodError);

                _proto.SetProperty(kvp.Key, method);
            }

        // Add instance properties to prototype
        if (options.Properties != null)
            foreach (var prop in options.Properties.Values)
                DefineProperty(_proto, prop, false);

        // Create the constructor function
        using var namePtr = _context.AllocateString(name, out _);

        var constructorPtr = _context.Runtime.Registry.NewClass(
            _context.Pointer,
            Id,
            namePtr,
            options.Finalizer != null ? 1 : 0,
            options.GCMark != null ? 1 : 0);

        var ctorError = _context.GetLastError(constructorPtr);
        if (ctorError != null) throw new HakoException($"Class constructor exception for {name}", ctorError);

        _ctorFunction = new JSValue(_context, constructorPtr);

        // Add static methods to constructor
        if (options.StaticMethods != null)
            foreach (var kvp in options.StaticMethods)
            {
                using var method = _context.NewFunction(kvp.Key, kvp.Value);
                var methodError = _context.GetLastError(method.GetHandle());
                if (methodError != null)
                    throw new HakoException($"Failed to create static method {kvp.Key}", methodError);

                _ctorFunction.SetProperty(kvp.Key, method);
            }

        // Add static properties to constructor
        if (options.StaticProperties != null)
            foreach (var prop in options.StaticProperties.Values)
                DefineProperty(_ctorFunction, prop, true);

        _context.Runtime.Registry.SetConstructor(
            _context.Pointer,
            _ctorFunction.GetHandle(),
            _proto.GetHandle());

        _context.Runtime.Registry.SetClassProto(
            _context.Pointer,
            Id,
            _proto.GetHandle());
    }

    private void DefineProperty(JSValue target, ClassOptions.PropertyDefinition prop, bool isStatic)
    {
        using var propName = _context.NewString(prop.Name);

        // Create getter function if provided
        JSValue? getterFunc = null;
        if (prop.Getter != null)
        {
            var getterName = $"get {prop.Name}";
            getterFunc = _context.NewFunction(getterName, prop.Getter);
        }

        // Create setter function if provided
        JSValue? setterFunc = null;
        if (prop.Setter != null)
        {
            var setterName = $"set {prop.Name}";
            setterFunc = _context.NewFunction(setterName, prop.Setter);
        }

        try
        {
            var result = _context.Runtime.Registry.DefineProp(
                _context.Pointer,
                target.GetHandle(),
                propName.GetHandle(),
                _context.Runtime.Registry.GetUndefined(), // value (not used for accessor)
                getterFunc?.GetHandle() ?? _context.Runtime.Registry.GetUndefined(), // getter
                setterFunc?.GetHandle() ?? _context.Runtime.Registry.GetUndefined(), // setter
                prop.Configurable ? 1 : 0, // configurable
                prop.Enumerable ? 1 : 0, // enumerable
                0, // hasValue (accessor property)
                0, // hasWritable (accessor property)
                0 // writable (not used)
            );

            if (result == -1)
            {
                var error = _context.GetLastError();
                if (error != null)
                    throw new HakoException(
                        $"Failed to define {(isStatic ? "static " : "")}property '{prop.Name}'", error);
                throw new HakoException(
                    $"Failed to define {(isStatic ? "static " : "")}property '{prop.Name}': unknown error");
            }

            if (result == 0)
                throw new HakoException(
                    $"Failed to define {(isStatic ? "static " : "")}property '{prop.Name}': operation returned FALSE");
        }
        finally
        {
            getterFunc?.Dispose();
            setterFunc?.Dispose();
        }
    }

    /// <summary>
    /// Registers the class constructor in the global scope, making it accessible from JavaScript.
    /// </summary>
    /// <param name="globalName">
    /// The name to use in the global scope, or <c>null</c> to use the class's <see cref="Name"/>.
    /// </param>
    /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// After calling this method, JavaScript code can create instances using <c>new ClassName(...)</c>.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// jsClass.RegisterGlobal("Person");
    /// 
    /// // JavaScript can now:
    /// // const p = new Person("Alice");
    /// </code>
    /// </para>
    /// </remarks>
    public void RegisterGlobal(string? globalName = null)
    {
        CheckDisposed();
        var name = globalName ?? Name;
        using var global = _context.GetGlobalObject();
        global.SetProperty(name, Constructor);
        _name = name;
    }

    /// <summary>
    /// Creates a new instance of the class with the registered prototype.
    /// </summary>
    /// <param name="opaque">An optional opaque value to store with the instance for native state.</param>
    /// <returns>A <see cref="JSValue"/> representing the new instance.</returns>
    /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
    /// <exception cref="HakoException">Instance creation failed.</exception>
    /// <remarks>
    /// <para>
    /// This method creates a new JavaScript object with the class's prototype and optionally
    /// stores an opaque integer value that can be used to associate native data with the instance.
    /// </para>
    /// <para>
    /// Note: This method does NOT call the JavaScript constructor function. It only creates
    /// the object structure. If you need constructor logic, call the constructor from JavaScript
    /// or manually invoke the constructor function.
    /// </para>
    /// </remarks>
    public JSValue CreateInstance(int? opaque = null)
    {
        CheckDisposed();

        // This uses the prototype registered with SetClassProto
        var instancePtr = _context.Runtime.Registry.NewObjectClass(_context.Pointer, Id);

        var error = _context.GetLastError(instancePtr);
        if (error != null) throw new HakoException("Instance creation exception", error);

        var instance = new JSValue(_context, instancePtr);

        if (opaque.HasValue) _context.Runtime.Registry.SetOpaque(instance.GetHandle(), opaque.Value);

        return instance;
    }

    /// <summary>
    /// Creates a new instance of the class with a custom prototype.
    /// </summary>
    /// <param name="customProto">The prototype object to use for the new instance.</param>
    /// <param name="opaque">An optional opaque value to store with the instance for native state.</param>
    /// <returns>A <see cref="JSValue"/> representing the new instance.</returns>
    /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
    /// <exception cref="HakoException">Instance creation failed.</exception>
    /// <remarks>
    /// <para>
    /// This is an advanced method used for implementing JavaScript inheritance where subclasses
    /// may have custom prototypes. Most users should use <see cref="CreateInstance"/> instead.
    /// </para>
    /// </remarks>
    public JSValue CreateInstanceWithPrototype(JSValue customProto, int? opaque = null)
    {
        CheckDisposed();

        var instancePtr = _context.Runtime.Registry.NewObjectProtoClass(
            _context.Pointer,
            customProto.GetHandle(),
            Id);

        var error = _context.GetLastError(instancePtr);
        if (error != null) throw new HakoException("Instance creation with prototype exception", error);

        var instance = new JSValue(_context, instancePtr);

        if (opaque.HasValue) _context.Runtime.Registry.SetOpaque(instance.GetHandle(), opaque.Value);

        return instance;
    }

    /// <summary>
    /// Gets the opaque value stored with an instance of this class.
    /// </summary>
    /// <param name="instance">The class instance to retrieve the opaque value from.</param>
    /// <returns>The opaque integer value associated with the instance.</returns>
    /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
    /// <exception cref="HakoException">Failed to retrieve the opaque value.</exception>
    /// <remarks>
    /// <para>
    /// Opaque values are typically used to store pointers or identifiers that link JavaScript
    /// instances to native .NET objects. The value is stored as an integer but can represent
    /// a pointer, hash code, or dictionary key.
    /// </para>
    /// </remarks>
    public int GetOpaque(JSValue instance)
    {
        CheckDisposed();
        var opaque = _context.Runtime.Registry.GetOpaque(_context.Pointer, instance.GetHandle(), Id);
        var lastError = _context.GetLastError();
        if (lastError != null) throw new HakoException("Unable to get opaque", lastError);
        return opaque;
    }
    

    /// <summary>
    /// Checks if a JavaScript value is an instance of this class.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><c>true</c> if the value is an instance of this class; otherwise, <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
    /// <remarks>
    /// This checks if the value's internal class ID matches this class's ID, which is more
    /// reliable than checking prototypes since it works even if the prototype chain has been modified.
    /// </remarks>
    public bool IsInstance(JSValue value)
    {
        CheckDisposed();
        var classId = _context.Runtime.Registry.GetClassID(value.GetHandle());
        return classId == Id;
    }

    private void CheckDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}