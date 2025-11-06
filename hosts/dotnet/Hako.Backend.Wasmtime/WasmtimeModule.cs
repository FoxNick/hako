using System;
using HakoJS.Backend.Core;
using Wasmtime;

namespace HakoJS.Backend.Wasmtime;

public sealed class WasmtimeModule : WasmModule
{
    private readonly Module _module;
    private bool _disposed;

    internal WasmtimeModule(Module module, string name)
    {
        _module = module;
        Name = name;
    }

    public override string Name { get; }

    public override WasmInstance Instantiate(WasmLinker linker)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (linker is not WasmtimeLinker wasmtimeLinker)
            throw new ArgumentException("Linker must be a WasmtimeLinker instance", nameof(linker));

        var instance = wasmtimeLinker.UnderlyingLinker.Instantiate(wasmtimeLinker.UnderlyingStore, _module);
        return new WasmtimeInstance(instance);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _module.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}