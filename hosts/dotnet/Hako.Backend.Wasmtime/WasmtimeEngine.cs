using System;
using System.IO;
using HakoJS.Backend.Configuration;
using HakoJS.Backend.Core;
using Wasmtime;

namespace HakoJS.Backend.Wasmtime;

public sealed class WasmtimeEngine : WasmEngine, IWasmEngineFactory<WasmtimeEngine>
{
    private readonly Engine _engine;
    private bool _disposed;

    private WasmtimeEngine(WasmEngineOptions options)
    {
        var config = new Config();
        config.WithBulkMemory(true);
        config.WithReferenceTypes(true);
        config.WithMultiValue(true);
        config.WithSIMD(true);
        config.WithOptimizationLevel(OptimizationLevel.Speed);

        _engine = new Engine(config);
    }

    public static WasmtimeEngine Create(WasmEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new WasmtimeEngine(options);
    }

    public override WasmStore CreateStore(WasmStoreOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new WasmtimeStore(_engine, options ?? new WasmStoreOptions());
    }

    public override WasmModule CompileModule(ReadOnlySpan<byte> wasmBytes, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var module = Module.FromBytes(_engine, name, wasmBytes);
        return new WasmtimeModule(module, name);
    }

    public override WasmModule LoadModule(string path, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!File.Exists(path))
            throw new FileNotFoundException($"WebAssembly module not found: {path}", path);

        var module = Module.FromFile(_engine, path);
        return new WasmtimeModule(module, name);
    }

    public override WasmModule LoadModule(Stream stream, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var module = Module.FromStream(_engine, name, stream);
        return new WasmtimeModule(module, name);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _engine.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}