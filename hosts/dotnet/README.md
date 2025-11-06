<div align="center">

# Hako for .NET

**箱 (Hako) means "box" in Japanese**

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](https://www.apache.org/licenses/LICENSE-2.0.txt)
[![NuGet](https://img.shields.io/nuget/v/Hako.svg)](https://www.nuget.org/packages/Hako/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Hako.svg)](https://www.nuget.org/packages/Hako/)

*Embeddable, lightweight, secure, high-performance JavaScript engine for .NET*

</div>

---

## What is Hako?

Hako is an embeddable JavaScript engine that brings modern ES2023+ JavaScript execution to your .NET applications. Built on [6over3's fork of QuickJS](https://github.com/6over6/quickjs), Hako compiles QuickJS to WebAssembly and hosts it safely within your .NET process.

Hako supports the **ES2023 specification** and Phase 4 proposals including modules, asynchronous generators, **top-level await**, proxies, BigInt, and **built-in TypeScript support**.

## What is Hako for .NET?

**Hako for .NET** is the official .NET host implementation that provides:

- **Secure Execution**: JavaScript runs in a WebAssembly sandbox with configurable memory limits, execution timeouts, and resource controls
- **High Performance**: Powered by QuickJS compiled to WASM, with multiple backend options (Wasmtime, WACS)
- **Modern JavaScript**: Full ES2023+ support including async/await, modules, promises, classes, and more
- **TypeScript Support**: Built-in type stripping for TypeScript code (type annotations are removed at runtime)
- **Top-Level Await**: Use await at the module top level without wrapping in async functions
- **Deep .NET Integration**: Expose .NET functions to JavaScript, pass complex types bidirectionally, and marshal data seamlessly
- **Lightweight**: Minimal dependencies, small runtime footprint
- **Developer Friendly**: Source generators for automatic binding, rich extension methods

## Quick Start

```bash
dotnet add package Hako
dotnet add package Hako.Backend.Wasmtime
```

```csharp
using Hako;
using Hako.Backend.Wasmtime;
using Hako.Extensions;

// Initialize the runtime
using var runtime = Hako.Initialize<WasmtimeEngine>();
using var realm = runtime.CreateRealm()
    .WithGlobals(g => g.WithConsole());

// Execute JavaScript with type-safe results
var result = await realm.EvalAsync<int>(@"
    const numbers = [1, 2, 3, 4, 5];
    const sum = numbers.reduce((a, b) => a + b, 0);
    console.log('Sum:', sum);
    sum;
");

Console.WriteLine($"Result from JS: {result}"); // 15

await Hako.ShutdownAsync();
```

**[Read the full technical documentation →](./Hako/README.md)**

## Top-Level Await

Use `await` directly at the module top level without wrapping in async functions:

```csharp
var result = await realm.EvalAsync<string>(@"
    const response = await fetch('https://api.example.com/data');
    const data = await response.json();
    data.message;
", new() { FileName = "app.js", Async = true  });
```

## TypeScript Support

Hako automatically strips TypeScript type annotations when you use a `.ts` file extension:

```csharp
var result = await realm.EvalAsync<int>(@"
    interface User {
        name: string;
        age: number;
    }

    function greet(user: User): string {
        return `${user.name} is ${user.age} years old`;
    }

    const alice: User = { name: 'Alice', age: 30 };
    console.log(greet(alice));

    alice.age + 12;
", new() { FileName = "app.ts" });
```

You can also manually strip types:

```csharp
var typescript = "const add = (a: number, b: number): number => a + b;";
var javascript = runtime.StripTypes(typescript);
```

## What Makes Hako Secure?

### WebAssembly Sandboxing
All JavaScript execution happens inside a WebAssembly sandbox. The WASM runtime provides memory isolation, preventing JavaScript from accessing host memory outside its allocated boundaries.

### Configurable Resource Limits
```csharp
var runtime = Hako.Initialize<WasmtimeEngine>(opts => {
    opts.MemoryLimitBytes = 50 * 1024 * 1024; // 50 MB max
    opts.MaxStackSize = 1024 * 1024;          // 1 MB stack
});

realm.SetInterruptHandler(InterruptHandlers.Deadline(
    TimeSpan.FromSeconds(5) // 5 second timeout
));
```

### Controlled Host Access
JavaScript can only access .NET functionality you explicitly expose:
```csharp
realm.WithGlobals(g => g
    .WithConsole()
    .WithFunction("readFile", ReadFileFunction)
    .WithFunction("allowedAPI", AllowedAPIFunction));
// No file system, no network, no reflection—unless you provide it
```


## Architecture

```
┌─────────────────────────────────────────┐
│         Your .NET Application           │
├─────────────────────────────────────────┤
│           Hako Runtime API              │
│  (Realm, JSValue, Module System, etc.)  │
├─────────────────────────────────────────┤
│         Backend Abstraction             │
│    ┌──────────────┬──────────────┐      │
│    │  Wasmtime    │    WACS      │      │
│    │  Backend     │   Backend    │      │
│    └──────────────┴──────────────┘      │
├─────────────────────────────────────────┤
│      Hako Engine (compiled to WASM)     │
│          ES2023+ JavaScript             │
└─────────────────────────────────────────┘
```

## Projects

This repository contains multiple packages:

| Package | Description | NuGet |
|---------|-------------|-------|
| **[Hako](./Hako/)** | Core JavaScript runtime and APIs | [![NuGet](https://img.shields.io/nuget/v/Hako.svg)](https://www.nuget.org/packages/Hako/) |
| **[Hako.Backend.Wasmtime](./Hako.Backend.Wasmtime/)** | Production Wasmtime backend | [![NuGet](https://img.shields.io/nuget/v/Hako.Backend.Wasmtime.svg)](https://www.nuget.org/packages/Hako.Backend.Wasmtime/) |
| **[Hako.Backend.WACS](./Hako.Backend.WACS/)** | Experimental WACS backend (AOT) | [![NuGet](https://img.shields.io/nuget/v/Hako.Backend.WACS.svg)](https://www.nuget.org/packages/Hako.Backend.WACS/) |
| **[Hako.SourceGenerator](./Hako.SourceGenerator/)** | Automatic binding generator | [![NuGet](https://img.shields.io/nuget/v/Hako.SourceGenerator.svg)](https://www.nuget.org/packages/Hako.SourceGenerator/) |
| **[Hako.Backend](./Hako.Backend/)** | Backend abstraction interfaces | [![NuGet](https://img.shields.io/nuget/v/Hako.Backend.svg)](https://www.nuget.org/packages/Hako.Backend/) |

## Features

- ES2023 specification and Phase 4 proposals
- Top-level await
- TypeScript type stripping (not type checking)
- Async/await and Promises
- Asynchronous generators
- ES6 Modules (import/export)
- Proxies and BigInt
- Timers (setTimeout, setInterval, setImmediate)
- Expose .NET functions to JavaScript
- Expose .NET classes to JavaScript ([JSClass] source generation)
- Marshal complex types bidirectionally
- Custom module loaders
- Bytecode compilation and caching
- Multiple isolated realms
- Memory and execution limits
- Rich extension methods for safe API usage

## Resources

- **[Technical Documentation](./Hako/README.md)** - Complete API reference and usage guide
- **[Hako Project](https://github.com/6over3/hako)** - Main Hako project organization
- **[Blog: Introducing Hako](https://andrews.substack.com/p/embedding-typescript)** - Design philosophy and architecture
- **[GitHub Issues](https://github.com/6over3/hako/issues)** - Bug reports and feature requests
- **[NuGet Packages](https://www.nuget.org/packages?q=Hako)** - Download packages

## Examples

Check out the [examples/](./examples/) directory for complete samples:

- **[basics](./examples/basics/)** - Hello world and basic evaluation
- **[host-functions](./examples/host-functions/)** - Exposing .NET functions to JavaScript
- **[classes](./examples/classes/)** - Creating JavaScript classes backed by .NET
- **[modules](./examples/modules/)** - ES6 module system and custom loaders
- **[typescript](./examples/typescript/)** - TypeScript type stripping
- **[marshaling](./examples/marshaling/)** - Complex type marshaling between .NET and JS
- **[timers](./examples/timers/)** - setTimeout, setInterval, and event loop
- **[iteration](./examples/iteration/)** - Iterating over arrays, maps, and sets
- **[scopes](./examples/scopes/)** - Resource management with DisposableScope
- **[safety](./examples/safety/)** - Memory limits, timeouts, and sandboxing
- **[raylib](./examples/raylib/)** - Full game engine bindings example

## License

Licensed under the Apache License 2.0. See [LICENSE](./LICENSE) for details.

---

<div align="center">

**Built for the .NET community**

[Star us on GitHub](https://github.com/6over3/hako) • [Download from NuGet](https://www.nuget.org/packages/Hako/)

</div>