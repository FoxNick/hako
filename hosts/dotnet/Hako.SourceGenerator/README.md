# Hako.SourceGenerator

Source generator for creating JavaScript/TypeScript bindings from .NET code for the Hako JavaScript engine.

## Installation

```bash
dotnet add package Hako.SourceGenerator
```

## Requirements

Enable XML documentation generation in your project:

```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

## Usage

### Class Bindings

Expose .NET classes to JavaScript with `[JSClass]`:

```csharp
using HakoJS.SourceGeneration;

namespace MyApp;

/// <summary>
/// A 2D vector
/// </summary>
[JSClass(Name = "Vector2")]
public partial class Vector2
{
    [JSConstructor]
    public Vector2(double x = 0, double y = 0)
    {
        X = x;
        Y = y;
    }

    [JSProperty(Name = "x")]
    public double X { get; set; }

    [JSProperty(Name = "y")]
    public double Y { get; set; }

    [JSMethod(Name = "add")]
    public Vector2 Add(Vector2 other)
    {
        return new Vector2(X + other.X, Y + other.Y);
    }

    [JSMethod(Name = "length")]
    public double Length()
    {
        return Math.Sqrt(X * X + Y * Y);
    }

    [JSMethod(Name = "lerp", Static = true)]
    public static Vector2 Lerp(Vector2 a, Vector2 b, double t = 0.5)
    {
        return new Vector2(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t
        );
    }
}
```

### Module Bindings

Create JavaScript modules with static members using `[JSModule]`:

```csharp
using HakoJS.SourceGeneration;

namespace MyApp;

/// <summary>
/// Math utility functions
/// </summary>
[JSModule(Name = "math")]
[JSModuleClass(ClassType = typeof(Vector2), ExportName = "Vector2")]
public partial class MathModule
{
    [JSModuleValue(Name = "PI")]
    public static double Pi => Math.PI;

    [JSModuleMethod(Name = "sqrt")]
    public static double Sqrt(double x) => Math.Sqrt(x);
}
```

### Object Bindings

Marshal record types between .NET and JavaScript with `[JSObject]`:

```csharp
using HakoJS.SourceGeneration;

namespace MyApp;

/// <summary>
/// User configuration
/// </summary>
[JSObject]
public partial record Config(
    string Name,
    int Port = 8080,
    string? Host = null
);
```

### Delegate Support

Records can include delegates that are marshaled as JavaScript functions:

```csharp
using HakoJS.SourceGeneration;
using System;
using System.Threading.Tasks;

namespace MyApp;

[JSObject]
public partial record EventHandler(
    string EventName,
    Action<string> OnEvent,
    Func<int, Task<bool>> Validator
);
```

## Runtime Usage

After the source generator creates the bindings, register and use them at runtime:

### Using Classes

```csharp
using var runtime = Hako.Initialize<WasmtimeEngine>();

await Hako.Dispatcher.InvokeAsync(async () =>
{
    var realm = runtime.CreateRealm();
    
    // Register the class
    realm.RegisterClass<Vector2>();
    
    // Use from C#
    var vector = new Vector2(3, 4);
    using var jsValue = vector.ToJSValue(realm);
    
    // Use from JavaScript
    var result = await realm.EvalAsync(@"
        const v = new Vector2(1, 2);
        console.log(v.length());
        v.add(new Vector2(3, 4));
    ", new RealmEvalOptions { Type = EvalType.Module });
});
```

### Using Modules

```csharp
// Register module
runtime.ConfigureModules()
    .WithModule<MathModule>()
    .Apply();

// Use from JavaScript
var result = await realm.EvalAsync(@"
    import { PI, sqrt, Vector2 } from 'math';
    
    console.log('PI:', PI);
    console.log('sqrt(16):', sqrt(16));
    
    const v = new Vector2(3, 4);
    console.log(v.toString());
", new RealmEvalOptions { Type = EvalType.Module });
```

### Using Records (JSObject)

```csharp
// C# to JS
var config = new Config("test", 8080);
using var jsConfig = config.ToJSValue(realm);

using var jsObj = await realm.EvalAsync("({ name: 'test', port: 3000 })");
var csharpConfig = jsObj.As<Config>();

Console.WriteLine(csharpConfig); // Config { Name = test, Port = 3000, Host =  }

// With delegates (must dispose to release captured JS functions)
using var jsHandler = await realm.EvalAsync(@"
    ({ 
        eventName: 'click', 
        onEvent: (msg) => console.log(msg),
        validator: async (n) => n > 0
    })");
using var handler = HakoSandbox.EventHandler.FromJSValue(realm, jsHandler);
handler.OnEvent("test"); // Calls JS function

```

## Generated Output

The source generator produces:

- **C# binding code**: Marshaling logic between .NET and JavaScript
- **TypeScript definitions**: Accessible via `YourType.TypeDefinition` property with complete type information

TypeScript definitions are automatically generated for all types with `[JSClass]`, `[JSObject]`, `[JSModule]`, or
implementing `IJSMarshalable<T>`. XML documentation comments (`///`) are converted to JSDoc format in the definitions.

## Supported Types

- Primitives: `string`, `bool`, `int`, `long`, `float`, `double`, etc.
- Arrays: `T[]` (primitive element types)
- Byte buffers: `byte[]` to `ArrayBuffer`
- Typed arrays: `Uint8ArrayValue`, `Int32ArrayValue`, `Float64ArrayValue`, etc.
- Custom types: Any type with `[JSClass]` or `[JSObject]`, or manually implementing `IJSMarshalable<T>`
- Delegates: `Action<T>`, `Func<T>`, named delegates to JavaScript functions (sync and async)
- Nullable types: `T?` to `T | null`
- Optional parameters: Default values supported

## Attributes

### Class Attributes

- `[JSClass(Name)]`: Expose class as JavaScript class
- `[JSConstructor]`: Mark constructor to expose (optional, uses default if not specified)
- `[JSProperty(Name, Static, ReadOnly)]`: Expose property to JavaScript
- `[JSMethod(Name, Static)]`: Expose method to JavaScript
- `[JSIgnore]`: Exclude member from JavaScript binding

### Module Attributes

- `[JSModule(Name)]`: Create JavaScript module from static class
- `[JSModuleValue(Name)]`: Expose static field/property as module export
- `[JSModuleMethod(Name)]`: Expose static method as module function
- `[JSModuleClass(ClassType, ExportName)]`: Export a JSClass from a module

### Record Attributes

- `[JSObject]`: Marshal record types to/from JavaScript objects
- `[JSPropertyName(Name)]`: Customize JavaScript property name for record parameters

## Documentation

XML documentation comments (`///`) are automatically converted to JSDoc format in the generated TypeScript definitions.

See the [main Hako documentation](https://github.com/6over3/hako/tree/main/hosts/dotnet) for complete usage and API
reference