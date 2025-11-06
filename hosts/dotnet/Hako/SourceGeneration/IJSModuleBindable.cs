namespace HakoJS.SourceGeneration;

/// <summary>
/// Interface for module bindings. Automatically implemented by the source generator for classes with [JSModule].
/// </summary>
/// <remarks>
/// You don't implement this interface manually - the source generator creates the implementation.
/// Use this to register ES6 modules with the runtime's module loader.
/// </remarks>
/// <example>
/// <code>
/// [JSModule(Name = "math")]
/// public partial class MathModule
/// {
///     [JSModuleValue]
///     public static readonly double PI = Math.PI;
///     
///     [JSModuleMethod]
///     public static double Add(double a, double b) => a + b;
/// }
/// 
/// // Register module
/// runtime.ConfigureModules()
///     .WithModule&lt;MathModule&gt;()
///     .Apply();
/// 
/// // Use in JavaScript
/// // import { PI, add } from 'math';
/// // console.log(PI);        // 3.14159...
/// // console.log(add(2, 3)); // 5
/// </code>
/// </example>
public interface IJSModuleBindable
{
    /// <summary>
    /// Gets the module name (e.g., "math", "fs", "crypto").
    /// </summary>
    static abstract string Name { get; }
    
    /// <summary>
    /// Creates and registers the module with the runtime. Called internally during module loading.
    /// </summary>
    /// <param name="runtime">The HakoRuntime to register with.</param>
    /// <param name="context">Optional realm context. Uses system realm if null.</param>
    /// <returns>The created CModule with all exports configured.</returns>
    static abstract HakoJS.Host.CModule Create(HakoJS.Host.HakoRuntime runtime, HakoJS.VM.Realm? context = null);
}