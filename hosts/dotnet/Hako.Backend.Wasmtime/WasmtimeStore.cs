using System;
using HakoJS.Backend.Configuration;
using HakoJS.Backend.Core;
using Wasmtime;

namespace HakoJS.Backend.Wasmtime;

public sealed class WasmtimeStore : WasmStore
{
    private bool _disposed;

    internal WasmtimeStore(Engine engine, WasmStoreOptions options)
    {
        var wasiConfig = new WasiConfiguration()
            .WithInheritedStandardInput()
            .WithInheritedStandardOutput()
            .WithInheritedStandardError();

        UnderlyingStore = new Store(engine, this);
        Engine = engine;

        UnderlyingStore.SetWasiConfiguration(wasiConfig);
    }

    internal Store UnderlyingStore { get; }

    internal Engine Engine { get; }

    public override WasmLinker CreateLinker()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new WasmtimeLinker(UnderlyingStore);
    }

    public override WasmMemory CreateMemory(MemoryConfiguration config)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var memory = new Memory(UnderlyingStore, config.InitialPages, config.MaximumPages);
        return new WasmtimeMemory(memory);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) UnderlyingStore.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}