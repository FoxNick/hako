using System;
using System.IO;
using HakoJS.Backend.Configuration;
using HakoJS.Backend.Core;
using HakoJS.Backend.Exceptions;
using Wacs.Core;

namespace HakoJS.Backend.Wacs;

public sealed class WacsEngine : WasmEngine, IWasmEngineFactory<WacsEngine>
{
    private readonly WacsEngineOptions _options;
    private bool _disposed;

    public WacsEngine(WasmEngineOptions? options = null)
    {
        _options = options as WacsEngineOptions ?? new WacsEngineOptions
        {
            EnableOptimization = options?.EnableOptimization ?? true,
            EnableDebugInfo = options?.EnableDebugInfo ?? false
        };
    }

    public static WacsEngine Create(WasmEngineOptions options)
    {
        return new WacsEngine(options);
    }

    public override WasmStore CreateStore(WasmStoreOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new WacsStore(_options, options);
    }

    public override WasmModule CompileModule(ReadOnlySpan<byte> wasmBytes, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            using var ms = new MemoryStream(wasmBytes.ToArray());
            var module = BinaryModuleParser.ParseWasm(ms);
            return new WacsModule(module, name);
        }
        catch (Exception ex)
        {
            throw new WasmCompilationException($"Failed to compile module '{name}'", ex)
            {
                BackendName = "WACS"
            };
        }
    }

    public override WasmModule LoadModule(string path, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var stream = File.OpenRead(path);
            var module = BinaryModuleParser.ParseWasm(stream);
            return new WacsModule(module, name);
        }
        catch (Exception ex) when (ex is not WasmException)
        {
            throw new WasmCompilationException($"Failed to load module from '{path}'", ex)
            {
                BackendName = "WACS"
            };
        }
    }


    public override WasmModule LoadModule(Stream stream, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            var module = BinaryModuleParser.ParseWasm(stream);
            return new WacsModule(module, name);
        }
        catch (Exception ex) when (ex is not WasmException)
        {
            throw new WasmCompilationException($"Failed to load module '{name}'", ex)
            {
                BackendName = "WACS"
            };
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
    }
}