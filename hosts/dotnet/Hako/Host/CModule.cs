using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.VM;

namespace HakoJS.Host;

/// <summary>
/// Represents a C module that can be imported in JavaScript code using ES6 module syntax.
/// </summary>
/// <remarks>
/// <para>
/// CModule provides a way to expose C# functionality to JavaScript through the module system.
/// It manages the module's lifecycle, exports, and any classes that are registered within the module.
/// </para>
/// <para>
/// Modules are created using <see cref="HakoRuntime.CreateCModule"/> and configured through
/// a <see cref="CModuleInitializer"/> during initialization. After creation, exports must be
/// declared using <see cref="AddExport"/> or <see cref="AddExports"/> before the module can
/// be imported in JavaScript.
/// </para>
/// <para>
/// The module maintains references to all classes created during initialization and ensures
/// they are properly disposed when the module is disposed.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var module = runtime.CreateCModule("math", init =>
/// {
///     init.SetExport("add", (a, b) => a + b);
///     init.SetExport("PI", 3.14159);
/// })
/// .AddExports("add", "PI");
/// 
/// // JavaScript can now:
/// // import { add, PI } from 'math';
/// // console.log(add(2, 3)); // 5
/// </code>
/// </para>
/// </remarks>
public sealed class CModule : IDisposable
{
    private readonly List<JSClass> _createdClasses = [];
    private readonly List<string> _exports = [];
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CModule"/> class.
    /// </summary>
    /// <param name="context">The realm in which this module exists.</param>
    /// <param name="name">The module name used in JavaScript import statements.</param>
    /// <param name="initHandler">
    /// A callback function that receives a <see cref="CModuleInitializer"/> and configures
    /// the module's exports and behavior.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="initHandler"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="HakoException">Failed to create the C module in the runtime.</exception>
    /// <remarks>
    /// <para>
    /// This constructor is typically not called directly. Instead, use 
    /// <see cref="HakoRuntime.CreateCModule"/> which handles module registration automatically.
    /// </para>
    /// <para>
    /// The initialization handler is registered but not invoked immediately. It will be called
    /// when JavaScript code first imports the module, allowing lazy initialization.
    /// </para>
    /// </remarks>
    internal CModule(Realm context, string name, Action<CModuleInitializer> initHandler)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        var initializerHandler = initHandler ?? throw new ArgumentNullException(nameof(initHandler));

        using var moduleName = context.AllocateString(name, out _);

        var modulePtr = context.Runtime.Registry.NewCModule(context.Pointer, moduleName);
        if (modulePtr == 0) throw new HakoException($"Failed to create C module: {name}");

        Pointer = modulePtr;

        Context.Runtime.Callbacks.RegisterModuleInitHandler(name, initializer =>
        {
            initializer.SetParentBuilder(this);
            initializerHandler(initializer);
            return 0;
        });
    }

    /// <summary>
    /// Gets the realm (execution context) in which this module exists.
    /// </summary>
    /// <value>
    /// The <see cref="Realm"/> that owns this module.
    /// </value>
    public Realm Context { get; }

    /// <summary>
    /// Gets the native pointer to the underlying QuickJS module structure.
    /// </summary>
    /// <value>
    /// An integer pointer to the native module object.
    /// </value>
    /// <remarks>
    /// This is used internally for interop with the QuickJS runtime.
    /// </remarks>
    public int Pointer { get; }

    /// <summary>
    /// Gets a read-only list of all export names that have been declared for this module.
    /// </summary>
    /// <value>
    /// A read-only collection of export names that can be imported in JavaScript.
    /// </value>
    /// <remarks>
    /// Export names must be declared using <see cref="AddExport"/> or <see cref="AddExports"/>
    /// after the module is created. The actual values for these exports are set during
    /// module initialization through the <see cref="CModuleInitializer"/>.
    /// </remarks>
    public IReadOnlyList<string> ExportNames => _exports.AsReadOnly();

    /// <summary>
    /// Gets the name of this module as it appears in JavaScript import statements.
    /// </summary>
    /// <value>
    /// The module name, or <c>null</c> if it cannot be retrieved.
    /// </value>
    public string? Name => Context.GetModuleName(Pointer);

    /// <summary>
    /// Releases all resources used by this module, including any classes that were
    /// registered during initialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method disposes all <see cref="JSClass"/> instances that were created
    /// during module initialization. If any class disposal fails, the error is logged
    /// to the console but does not prevent other classes from being disposed.
    /// </para>
    /// <para>
    /// The module's initialization handler is also unregistered from the runtime callbacks.
    /// </para>
    /// <para>
    /// After disposal, the module should not be used. Attempting to call methods on a
    /// disposed module will throw <see cref="ObjectDisposedException"/>.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        foreach (var classObj in _createdClasses)
            try
            {
                classObj.Dispose();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Error disposing VmClass {classObj.Id}: {error}");
            }

        _createdClasses.Clear();

        if (Name != null) Context.Runtime.Callbacks.UnregisterModuleInitHandler(Name);
        _disposed = true;
    }

    /// <summary>
    /// Sets private data on the module that is accessible during initialization but not
    /// exported to JavaScript.
    /// </summary>
    /// <param name="value">The JavaScript value to store as private module data.</param>
    /// <exception cref="ObjectDisposedException">The module has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Private values are typically used to pass configuration or state into the module
    /// initialization handler. They can be retrieved using <see cref="CModuleInitializer.GetPrivateValue"/>
    /// or <see cref="GetPrivateValue"/>.
    /// </para>
    /// <para>
    /// Consider using <see cref="CModuleExtensions.WithPrivateValue"/> for a more fluent
    /// API that automatically disposes the value after setting it.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var config = ctx.NewObject();
    /// config.SetProperty("debug", ctx.True());
    /// 
    /// var module = runtime.CreateCModule("app", init =>
    /// {
    ///     var privateData = init.GetPrivateValue();
    ///     var debugMode = privateData.GetProperty("debug").AsBoolean();
    ///     init.SetExport("debug", debugMode);
    /// })
    /// .AddExport("debug");
    /// 
    /// module.SetPrivateValue(config);
    /// config.Dispose();
    /// </code>
    /// </para>
    /// </remarks>
    public void SetPrivateValue(JSValue value)
    {
        CheckDisposed();
        Context.Runtime.Registry.SetModulePrivateValue(
            Context.Pointer,
            Pointer,
            value.GetHandle());
    }

    /// <summary>
    /// Retrieves the private data that was set on this module.
    /// </summary>
    /// <returns>
    /// A <see cref="JSValue"/> containing the private module data. The caller is responsible
    /// for disposing this value.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The module has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// If no private value has been set, this returns an undefined value.
    /// </para>
    /// <para>
    /// The returned value must be disposed by the caller to prevent memory leaks.
    /// Consider using <see cref="CModuleExtensions.UsePrivateValue{T}"/> for automatic disposal.
    /// </para>
    /// </remarks>
    /// <seealso cref="SetPrivateValue"/>
    public JSValue GetPrivateValue()
    {
        CheckDisposed();
        return new JSValue(
            Context,
            Context.Runtime.Registry.GetModulePrivateValue(Context.Pointer, Pointer));
    }

    /// <summary>
    /// Declares a single export name for this module.
    /// </summary>
    /// <param name="exportName">The name of the export as it will appear in JavaScript.</param>
    /// <returns>The same module instance for method chaining.</returns>
    /// <exception cref="ObjectDisposedException">The module has been disposed.</exception>
    /// <exception cref="HakoException">Failed to add the export to the module.</exception>
    /// <remarks>
    /// <para>
    /// Export names must be declared before the module can be imported in JavaScript.
    /// The actual values for these exports are set during module initialization using
    /// <see cref="CModuleInitializer.SetExport"/>.
    /// </para>
    /// <para>
    /// This method can be chained to declare multiple exports, or use <see cref="AddExports"/>
    /// to declare multiple exports at once.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var module = runtime.CreateCModule("myModule", init =>
    /// {
    ///     init.SetExport("foo", 42);
    ///     init.SetExport("bar", "hello");
    /// })
    /// .AddExport("foo")
    /// .AddExport("bar");
    /// 
    /// // JavaScript can now:
    /// // import { foo, bar } from 'myModule';
    /// </code>
    /// </para>
    /// </remarks>
    public CModule AddExport(string exportName)
    {
        CheckDisposed();

        using var exportNamePtr = Context.AllocateString(exportName, out _);
        var result = Context.Runtime.Registry.AddModuleExport(Context.Pointer, Pointer, exportNamePtr);

        if (result != 0) throw new HakoException($"Failed to add export: {exportName}");

        _exports.Add(exportName);
        return this;
    }

    /// <summary>
    /// Declares multiple export names for this module at once.
    /// </summary>
    /// <param name="exportNames">An array of export names to declare.</param>
    /// <returns>The same module instance for method chaining.</returns>
    /// <exception cref="ObjectDisposedException">The module has been disposed.</exception>
    /// <exception cref="HakoException">Failed to add one or more exports to the module.</exception>
    /// <remarks>
    /// <para>
    /// This is a convenience method equivalent to calling <see cref="AddExport"/> for each name.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var module = runtime.CreateCModule("utils", init =>
    /// {
    ///     init.SetExport("trim", (string s) => s.Trim());
    ///     init.SetExport("upper", (string s) => s.ToUpper());
    ///     init.SetExport("lower", (string s) => s.ToLower());
    /// })
    /// .AddExports("trim", "upper", "lower");
    /// </code>
    /// </para>
    /// </remarks>
    public CModule AddExports(params string[] exportNames)
    {
        foreach (var exportName in exportNames) AddExport(exportName);
        return this;
    }

    /// <summary>
    /// Registers a class with this module to ensure it is properly disposed when the module is disposed.
    /// </summary>
    /// <param name="classObj">The class to register.</param>
    /// <exception cref="ObjectDisposedException">The module has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This method is typically called automatically by <see cref="CModuleInitializer.SetClass"/>
    /// or <see cref="CModuleInitializer.CompleteClassExport"/> when classes are created during
    /// module initialization.
    /// </para>
    /// <para>
    /// Registered classes are disposed automatically when the module is disposed, ensuring
    /// proper cleanup of native resources.
    /// </para>
    /// </remarks>
    public void RegisterClass(JSClass classObj)
    {
        CheckDisposed();
        _createdClasses.Add(classObj);
    }

    /// <summary>
    /// Unregisters a class from this module without disposing it.
    /// </summary>
    /// <param name="classObj">The class to unregister.</param>
    /// <remarks>
    /// This is used internally when a class needs to be removed from the module's tracking
    /// without being disposed. This is rare and typically only used in advanced scenarios.
    /// </remarks>
    internal void UnregisterClass(JSClass classObj)
    {
        _createdClasses.Remove(classObj);
    }

    /// <summary>
    /// Checks if the module has been disposed and throws if it has.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The module has been disposed.</exception>
    private void CheckDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CModule));
    }
}