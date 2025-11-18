namespace HakoJS.SourceGeneration;

/// <summary>
/// Exports a [JSObject] type as an interface from a module. Can be used multiple times to export multiple interfaces.
/// Unlike [JSModuleClass], interfaces are TypeScript-only and don't require runtime registration.
/// </summary>
/// <remarks>
/// Use this attribute to include TypeScript interface definitions in your module's .d.ts output.
/// This is useful for sharing type definitions between JavaScript and C# without creating class instances.
/// </remarks>
/// <example>
/// <code>
/// [JSObject]
/// public partial record Point(double X, double Y);
/// 
/// [JSObject]
/// public partial record Rectangle(Point TopLeft, Point BottomRight);
/// 
/// [JSModule(Name = "geometry")]
/// [JSModuleInterface(InterfaceType = typeof(Point), ExportName = "Point")]
/// [JSModuleInterface(InterfaceType = typeof(Rectangle), ExportName = "Rectangle")]
/// public partial class GeometryModule
/// {
///     [JSModuleMethod]
///     public static double CalculateArea(Rectangle rect)
///     {
///         var width = rect.BottomRight.X - rect.TopLeft.X;
///         var height = rect.BottomRight.Y - rect.TopLeft.Y;
///         return width * height;
///     }
///     
///     [JSModuleMethod]
///     public static Point[] GetCorners(Rectangle rect)
///     {
///         return new[]
///         {
///             rect.TopLeft,
///             new Point(rect.BottomRight.X, rect.TopLeft.Y),
///             rect.BottomRight,
///             new Point(rect.TopLeft.X, rect.BottomRight.Y)
///         };
///     }
/// }
/// 
/// // Register module (no class registration needed for interfaces)
/// runtime.ConfigureModules()
///     .WithModule&lt;GeometryModule&gt;()
///     .Apply();
/// 
/// // In TypeScript:
/// // import { type Point, type Rectangle, calculateArea, getCorners } from 'geometry';
/// // 
/// // const rect: Rectangle = {
/// //   topLeft: { x: 0, y: 0 },
/// //   bottomRight: { x: 10, y: 10 }
/// // };
/// // 
/// // const area = calculateArea(rect);
/// // const corners: Point[] = getCorners(rect);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class JSModuleInterfaceAttribute : Attribute
{
    /// <summary>
    /// The interface type to export. Must be a record with the [JSObject] attribute.
    /// </summary>
    public Type? InterfaceType { get; set; }
    
    /// <summary>
    /// The export name in TypeScript. Defaults to the type name if not specified.
    /// </summary>
    public string? ExportName { get; set; }
}