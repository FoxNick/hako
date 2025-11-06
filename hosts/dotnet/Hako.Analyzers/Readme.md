# Hako.Analyzers

Roslyn analyzers and code fixes for the Hako JavaScript engine.

## Installation

```bash
dotnet add package Hako.Analyzers
```

The analyzer is automatically integrated into your IDE and build process.

## Analyzers

### HAKO100: Missing Module Export

Detects when module exports are set but not declared in the module definition.

**Problem:**
```csharp
var module = runtime.CreateCModule("myModule", init =>
{
    init.SetExport("foo", 42);
    init.SetFunction("greet", greeter);
    init.SetClass("Calculator", calculatorClass);
});
// Warning: Exports 'foo', 'greet', and 'Calculator' are not declared
```

**Fixed:**
```csharp
var module = runtime.CreateCModule("myModule", init =>
{
    init.SetExport("foo", 42);
    init.SetFunction("greet", greeter);
    init.SetClass("Calculator", calculatorClass);
})
.AddExport("foo")
.AddExport("greet")
.AddExport("Calculator");
```

**Code Fix:**
The analyzer provides an automatic code fix that adds missing `AddExport` calls. Use "Fix All" to batch fix multiple missing exports in a single module.

## Why This Matters

Module exports must be explicitly declared using `AddExport` or `AddExports`. Forgetting to declare an export means the value won't be accessible from JavaScript imports, causing runtime errors that are hard to debug.

## Documentation

See the [main Hako documentation](https://github.com/6over3/hako/tree/main/hosts/dotnet) for complete API reference.