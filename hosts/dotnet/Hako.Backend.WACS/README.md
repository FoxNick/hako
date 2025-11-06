# Hako.Backend.WACS

Pure .NET WebAssembly backend for Hako using [WACS](https://github.com/kelnishi/WACS).

## Installation

```bash
dotnet add package Hako.Backend.WACS
```

## Usage

```csharp
using HakoJS;
using HakoJS.Backend.Wacs;

using var runtime = Hako.Initialize<WacsEngine>();

// Use runtime...
```

## Performance vs Portability

WACS is slower than the Wasmtime backend but has the advantage of being implemented entirely in .NET. This means your application can compile and run on **any target where .NET is supported**, including:

- WebAssembly (WASM)
- Blazor (client-side and server-side)
- Mobile platforms (iOS, Android)
- Any other .NET runtime target

Use WACS when maximum portability is more important than raw performance.

## Documentation

See the [main Hako documentation](https://github.com/6over3/hako/tree/main/hosts/dotnet) for complete usage and API reference.