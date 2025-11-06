// examples/classes

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.SourceGeneration;

var runtime = Hako.Initialize<WasmtimeEngine>();
var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

realm.RegisterClass<Point>();

await realm.EvalAsync(@"
    const a = new Point(3, 4);
    const b = new Point(6, 8);
    
    console.log('Point A:', a.toString());
    console.log('Distance:', a.distanceTo(b));
    
    const mid = Point.midpoint(a, b);
    console.log('Midpoint:', mid.toString());
");

// JS to C#
// using ToInstance gets the .NET backing class instance from the JSValue
var jsPoint = await realm.EvalAsync("new Point(10, 20)");
var csPoint = jsPoint.ToInstance<Point>();
Console.WriteLine($"C# Point: X={csPoint.X}, Y={csPoint.Y}");

// C# to JS
var newPoint = new Point(5, 15);
var jsValue = newPoint.ToJSValue(realm);
var toStringMethod = jsValue.GetProperty("toString");
// bind the 'this' instance
var pointString = await toStringMethod.Bind(jsValue).InvokeAsync<string>();
Console.WriteLine($"JS Point: {pointString}");


toStringMethod.Dispose();
jsValue.Dispose();
jsPoint.Dispose();
realm.Dispose();
runtime.Dispose();

await Hako.ShutdownAsync();


[JSClass(Name = "Point")]
partial class Point
{
    [JSConstructor]
    public Point(double x = 0, double y = 0) { X = x; Y = y; }

    [JSProperty(Name = "x")]
    public double X { get; set; }

    [JSProperty(Name = "y")]
    public double Y { get; set; }

    [JSMethod(Name = "distanceTo")]
    public double DistanceTo(Point other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    [JSMethod(Name = "toString")]
    public override string ToString() => $"Point({X}, {Y})";

    [JSMethod(Name = "midpoint", Static = true)]
    public static Point Midpoint(Point a, Point b) =>
        new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
}