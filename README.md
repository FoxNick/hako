<div align="center">

<p>
  <img width="500" alt="Hako logo" src="./assets/banner.png" />
  <p>Hako (箱) means "box" in Japanese</p>
</p>

[![Build](https://github.com/6over3/hako/actions/workflows/build-module.yml/badge.svg)](https://github.com/6over3/hako/actions/workflows/build-module.yml)
[![.NET Tests](https://github.com/6over3/hako/actions/workflows/dotnet-test.yml/badge.svg)](https://github.com/6over3/hako/actions/workflows/dotnet-test.yml)
[![Release](https://github.com/6over3/hako/actions/workflows/release.yml/badge.svg)](https://github.com/6over3/hako/actions/workflows/release.yml)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](https://www.apache.org/licenses/LICENSE-2.0.txt)

</div>

---

## Overview

Hako is an embeddable JavaScript engine that compiles to WebAssembly. Built on [6over3's fork of QuickJS](https://github.com/6over3/quickjs), Hako provides a secure, lightweight runtime for executing modern JavaScript with ES2023+ support, Phase 4 TC39 proposals, top-level await, and built-in TypeScript type stripping.

The engine compiles to a single `hako.wasm` reactor module (~800KB) that can be embedded in any application with a WebAssembly runtime. JavaScript executes within a memory-safe WASM sandbox with configurable resource limits and uses WASM-JIT to maximize performance.

## Host Implementations

| Host | Package | Documentation |
|------|---------|---------------|
| **.NET** | [![NuGet](https://img.shields.io/nuget/v/Hako.svg)](https://www.nuget.org/packages/Hako/) | [hosts/dotnet](./hosts/dotnet/) |

## Resources

| Resource | Link |
|----------|------|
| **Engine** | [github.com/6over3/quickjs](https://github.com/6over3/quickjs) |
| **Blog Post** | [Introducing Hako](https://andrews.substack.com/p/embedding-typescript) |
| **Issues** | [GitHub Issues](https://github.com/6over3/hako/issues) |
| **License** | [Apache 2.0](./LICENSE) |
