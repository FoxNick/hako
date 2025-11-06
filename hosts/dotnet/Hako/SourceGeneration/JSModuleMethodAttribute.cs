namespace HakoJS.SourceGeneration;

/// <summary>
/// Exports a static method as a module function. Supports async methods and optional parameters.
/// </summary>
/// <example>
/// <code>
/// [JSModule(Name = "math")]
/// public partial class MathModule
/// {
///     [JSModuleMethod]
///     public static double Add(double a, double b) => a + b;
///     
///     [JSModuleMethod(Name = "multiply")]
///     public static double Mul(double a, double b) => a * b;
///     
///     [JSModuleMethod]
///     public static async Task&lt;string&gt; FetchDataAsync(string url)
///     {
///         // Async methods automatically return promises
///     }
///     
///     [JSModuleMethod]
///     public static int Increment(int value, int step = 1) => value + step;
/// }
/// 
/// // In JavaScript:
/// // import { add, multiply, fetchDataAsync, increment } from 'math';
/// // add(2, 3);              // 5
/// // multiply(4, 5);         // 20
/// // await fetchDataAsync(url);
/// // increment(10);          // 11
/// // increment(10, 5);       // 15
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public class JSModuleMethodAttribute : Attribute
{
    /// <summary>
    /// The exported function name in JavaScript. Defaults to camelCase of the method name.
    /// </summary>
    public string? Name { get; set; }
}