namespace HakoJS.SourceGeneration;

/// <summary>
/// Exports a [JSClass] type from a module. Can be used multiple times to export multiple classes.
/// </summary>
/// <example>
/// <code>
/// [JSClass]
/// public partial class Vector2
/// {
///     [JSProperty]
///     public double X { get; set; }
///     
///     [JSProperty]
///     public double Y { get; set; }
/// }
/// 
/// [JSModule(Name = "geometry")]
/// [JSModuleClass(ClassType = typeof(Vector2), ExportName = "Vector2")]
/// public partial class GeometryModule
/// {
///     [JSModuleMethod]
///     public static double Distance(Vector2 a, Vector2 b)
///     {
///         var dx = a.X - b.X;
///         var dy = a.Y - b.Y;
///         return Math.Sqrt(dx * dx + dy * dy);
///     }
/// }
/// 
/// // Register
/// realm.RegisterClass&lt;Vector2&gt;();
/// runtime.ConfigureModules()
///     .WithModule&lt;GeometryModule&gt;()
///     .Apply();
/// 
/// // In JavaScript:
/// // import { Vector2, distance } from 'geometry';
/// // const v = new Vector2();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class JSModuleInterfaceAttribute<TType> : Attribute
{
    /// <summary>
    /// The class type to export. Must have [JSClass] attribute.
    /// </summary>
    public TType? InterfaceType { get; set; }
    
    /// <summary>
    /// The export name in JavaScript. Defaults to the class name if not specified.
    /// </summary>
    public string? ExportName { get; set; }
}