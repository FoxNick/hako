namespace HakoJS.SourceGeneration;

/// <summary>
/// Marks a class as a JavaScript ES6 module definition. The class must be partial.
/// </summary>
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
///     
///     [JSModuleMethod]
///     public static async Task&lt;string&gt; FetchAsync(string url)
///     {
///         // Async methods are supported
///     }
/// }
/// 
/// // Register and use
/// runtime.ConfigureModules()
///     .WithModule&lt;MathModule&gt;()
///     .Apply();
/// 
/// // In JavaScript:
/// // import { PI, add, fetchAsync } from 'math';
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public class JSModuleAttribute : Attribute
{
    /// <summary>
    /// The module name in JavaScript (e.g., "math", "fs"). Defaults to the class name if not specified.
    /// </summary>
    public string? Name { get; set; }
}