# Hako.Backend

Backend abstraction layer for Hako WebAssembly runtimes.

## Overview

This package provides the core interfaces and abstractions for implementing custom Hako backends. Most users should use a concrete backend implementation instead:

- **[Hako.Backend.Wasmtime](../Hako.Backend.Wasmtime)** - High-performance backend using Cranelift JIT
- **[Hako.Backend.WACS](../Hako.Backend.WACS)** - Pure .NET backend for maximum portability

## Custom Backend Implementation

To implement your own backend, reference this package and implement the required interfaces. See the existing backend implementations as reference:

- [Wasmtime Backend Implementation](https://github.com/6over3/hako/tree/main/hosts/dotnet/Hako.Backend.Wasmtime)
- [WACS Backend Implementation](https://github.com/6over3/hako/tree/main/hosts/dotnet/Hako.Backend.WACS)

## Installation

```bash
dotnet add package Hako.Backend
```

## Documentation

See the [main Hako documentation](https://github.com/6over3/hako/tree/main/hosts/dotnet) for complete API reference.