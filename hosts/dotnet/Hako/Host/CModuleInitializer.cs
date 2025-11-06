using HakoJS.Builders;
using HakoJS.Exceptions;
using HakoJS.VM;

namespace HakoJS.Host;

/// <summary>
/// Provides methods for initializing and configuring a C module during its creation.
/// </summary>
/// <remarks>
/// <para>
/// The CModuleInitializer is passed to the initialization callback when creating a module with
/// <see cref="HakoRuntime.CreateCModule"/>. It provides a fluent API for setting up exports,
/// functions, classes, and private data.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var module = runtime.CreateCModule("calculator", init =>
/// {
///     init.SetExport("version", "1.0.0");
///     init.SetFunction("add", (ctx, thisArg, args) =>
///     {
///         var a = args[0].AsNumber();
///         var b = args[1].AsNumber();
///         return ctx.NewNumber(a + b);
///     });
///     init.SetClass("Calculator", (ctx, instance, args, newTarget) => null);
/// })
/// .AddExports("version", "add", "Calculator");
/// </code>
/// </para>
/// </remarks>
public sealed class CModuleInitializer : IDisposable
{
    private readonly List<JSClass> _createdClasses = [];
    private bool _disposed;
    private CModule? _parentBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CModuleInitializer"/> class.
    /// </summary>
    /// <param name="context">The realm in which the module is being initialized.</param>
    /// <param name="modulePtr">The native pointer to the module being initialized.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
    internal CModuleInitializer(Realm context, int modulePtr)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Pointer = modulePtr;
    }

    /// <summary>
    /// Gets the realm (execution context) in which the module is being initialized.
    /// </summary>
    /// <remarks>
    /// Use this context to create JavaScript values, parse JSON, or perform other
    /// realm-specific operations during module initialization.
    /// </remarks>
    public Realm Context { get; }

    /// <summary>
    /// Gets the name of the module being initialized.
    /// </summary>
    public string? Name => Context.GetModuleName(Pointer);

    /// <summary>
    /// Gets the native pointer to the underlying QuickJS module structure.
    /// </summary>
    internal int Pointer { get; }

    /// <summary>
    /// Releases all resources used by this initializer.
    /// </summary>
    /// <remarks>
    /// Classes created during initialization remain tracked by the parent <see cref="CModule"/>
    /// and are disposed when the module is disposed.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _createdClasses.Clear();
        _disposed = true;
    }

    /// <summary>
    /// Sets the parent module builder that owns this initializer.
    /// </summary>
    /// <param name="builder">The parent <see cref="CModule"/> instance.</param>
    internal void SetParentBuilder(CModule builder)
    {
        _parentBuilder = builder;
    }

    /// <summary>
    /// Sets private data on the module that is accessible during and after initialization but not exported.
    /// </summary>
    /// <param name="value">The JavaScript value to store as private module data.</param>
    /// <exception cref="ObjectDisposedException">The initializer has been disposed.</exception>
    /// <remarks>
    /// The value is stored by reference, not copied. The caller must manage its lifetime appropriately.
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
    /// Converts a C# value to a JavaScript value and stores it as private module data.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert and store.</typeparam>
    /// <param name="value">The C# value to convert and store.</param>
    /// <exception cref="ObjectDisposedException">The initializer has been disposed.</exception>
    /// <remarks>
    /// The converted JavaScript value is automatically disposed after being stored.
    /// </remarks>
    public void SetPrivateValue<T>(T value)
    {
        CheckDisposed();
        using var vmv = Context.NewValue(value);
        Context.Runtime.Registry.SetModulePrivateValue(
            Context.Pointer,
            Pointer,
            vmv.GetHandle());
    }

    /// <summary>
    /// Retrieves the private data that was set on this module.
    /// </summary>
    /// <returns>A <see cref="JSValue"/> containing the private module data. The caller must dispose this value.</returns>
    /// <exception cref="ObjectDisposedException">The initializer has been disposed.</exception>
    /// <remarks>
    /// If no private value has been set, returns an undefined value.
    /// </remarks>
    public JSValue GetPrivateValue()
    {
        CheckDisposed();
        return new JSValue(
            Context,
            Context.Runtime.Registry.GetModulePrivateValue(Context.Pointer, Pointer));
    }

    /// <summary>
    /// Sets a module export to a JavaScript value and disposes the value.
    /// </summary>
    /// <param name="exportName">The name of the export as it appears in JavaScript.</param>
    /// <param name="value">The JavaScript value to export. This value will be disposed after setting.</param>
    /// <exception cref="ObjectDisposedException">The initializer has been disposed.</exception>
    /// <exception cref="HakoException">Failed to set the export.</exception>
    /// <remarks>
    /// The export name must have been declared using <see cref="CModule.AddExport"/> or
    /// <see cref="CModule.AddExports"/> before the module can be imported.
    /// </remarks>
    public void SetExport(string exportName, JSValue value)
    {
        CheckDisposed();
        

        using var exportNamePtr = Context.AllocateString(exportName, out _);

        var result = Context.Runtime.Registry.SetModuleExport(
            Context.Pointer,
            Pointer,
            exportNamePtr,
            value.GetHandle());

        if (result != 0) throw new HakoException($"Failed to set export: {exportName}");
        //consume
        value.Dispose();
    }

    /// <summary>
    /// Converts a C# value to a JavaScript value and sets it as a module export.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert and export.</typeparam>
    /// <param name="exportName">The name of the export as it appears in JavaScript.</param>
    /// <param name="value">The C# value to convert and export.</param>
    /// <exception cref="ObjectDisposedException">The initializer has been disposed.</exception>
    /// <exception cref="HakoException">Failed to set the export.</exception>
    /// <remarks>
    /// The converted JavaScript value is automatically disposed after being set.
    /// </remarks>
    public void SetExport<T>(string exportName, T value)
    {
        CheckDisposed();
        using var convertedValue = Context.NewValue(value);
        SetExport(exportName, convertedValue);
    }

    /// <summary>
    /// Sets multiple module exports from a dictionary of values.
    /// </summary>
    /// <typeparam name="T">The type of the values in the dictionary.</typeparam>
    /// <param name="exports">A dictionary mapping export names to their values.</param>
    /// <exception cref="ObjectDisposedException">The initializer has been disposed.</exception>
    /// <exception cref="HakoException">Failed to set one or more exports.</exception>
    public void SetExports<T>(Dictionary<string, T> exports)
    {
        foreach (var kvp in exports) SetExport(kvp.Key, kvp.Value);
    }

    /// <summary>
    /// Creates a function export using a JavaScript function callback.
    /// </summary>
    /// <param name="exportName">The name of the function export.</param>
    /// <param name="fn">
    /// A callback function that receives the context, 'this' value, and arguments,
    /// and returns a JavaScript value or <c>null</c>.
    /// </param>
    /// <exception cref="ObjectDisposedException">The initializer has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// The callback is wrapped in a <see cref="JSFunction"/> and set as an export.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// init.SetFunction("greet", (ctx, thisArg, args) =>
    /// {
    ///     var name = args.Length > 0 ? args[0].AsString() : "World";
    ///     return ctx.NewString($"Hello, {name}!");
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public void SetFunction(string exportName, JSFunction fn)
    {
        using var func = Context.NewFunction(exportName, fn);
        SetExport(exportName, func);
    }

    /// <summary>
    /// Creates a fluent builder for defining a JavaScript class export.
    /// </summary>
    /// <param name="name">The name of the class as it appears in JavaScript.</param>
    /// <returns>A <see cref="JSClassBuilder"/> for configuring the class.</returns>
    /// <exception cref="ObjectDisposedException">The initializer has been disposed.</exception>
    /// <remarks>
    /// Use this when you need fine-grained control over class construction. For simpler scenarios,
    /// consider using <see cref="SetClass"/> instead.
    /// </remarks>
    public JSClassBuilder Class(string name)
    {
        CheckDisposed();
        return new JSClassBuilder(Context, name, this);
    }

    /// <summary>
    /// Creates and exports a JavaScript class with a constructor function.
    /// </summary>
    /// <param name="name">The name of the class as it appears in JavaScript.</param>
    /// <param name="constructorFn">
    /// A callback function that implements the class constructor. Return <c>null</c> for success,
    /// or return an error JSValue to throw an exception.
    /// </param>
    /// <param name="options">Optional class configuration options.</param>
    /// <exception cref="ObjectDisposedException">The initializer has been disposed.</exception>
    /// <remarks>
    /// The class is automatically registered with the parent module and exported.
    /// For more complex classes with methods and properties, use <see cref="Class"/> instead.
    /// </remarks>
    public void SetClass(
        string name,
        JSConstructor constructorFn,
        ClassOptions? options = null)
    {
        CheckDisposed();

        var classObj = new JSClass(Context, name, constructorFn, options);
        _createdClasses.Add(classObj);

        if (_parentBuilder != null) _parentBuilder.RegisterClass(classObj);

        SetExport(name, classObj.Constructor);
    }

    /// <summary>
    /// Completes the export of a class that was built using <see cref="Class"/>.
    /// </summary>
    /// <param name="classObj">The class to export.</param>
    /// <remarks>
    /// This should be called after building a class with <see cref="JSClassBuilder"/>.
    /// It registers the class with the parent module and exports its constructor.
    /// </remarks>
    public void CompleteClassExport(JSClass classObj)
    {
        _createdClasses.Add(classObj);

        if (_parentBuilder != null) _parentBuilder.RegisterClass(classObj);

        SetExport(classObj.Name, classObj.Constructor);
    }

    private void CheckDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CModuleInitializer));
    }
}