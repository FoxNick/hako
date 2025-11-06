# Hako.Backend.Wasmtime

Wasmtime-based backend for Hako using the Cranelift JIT compiler for high performance.

## Installation

```bash
dotnet add package Hako.Backend.Wasmtime
```

## Usage

```csharp
using HakoJS;
using HakoJS.Backend.Wasmtime;

using var runtime = Hako.Initialize<WasmtimeEngine>();

// Use runtime...
```

## AOT Static Linking

When publishing with native AOT compilation, you can statically link Wasmtime libraries into your executable to produce a single file:

```xml
<PropertyGroup>
  <WasmtimeStaticLink>true</WasmtimeStaticLink>
</PropertyGroup>
```

**Note**: Static linking requires the [Wasmtime.6over3](https://www.nuget.org/packages/Wasmtime.6over3) fork:

```xml
<PackageReference Include="Wasmtime.6over3" Version="38.0.3-dev" />
```

## Documentation

See the [main Hako documentation](https://github.com/6over3/hako/tree/main/hosts/dotnett) for complete usage and API reference.