using System;
using HakoJS.Backend.Core;
using HakoJS.Backend.Exceptions;
using Wacs.Core;
using Wacs.Core.Runtime;

namespace HakoJS.Backend.Wacs;

public sealed class WacsModule : WasmModule
{
    private readonly Module _module;
    private bool _disposed;

    internal WacsModule(Module module, string name)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override string Name { get; }

    public override WasmInstance Instantiate(WasmLinker linker)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(linker);

        if (linker is not WacsLinker wacsLinker)
            throw new ArgumentException("Linker must be a WacsLinker instance", nameof(linker));

        try
        {
            var runtime = wacsLinker.Store.Runtime;
            var modInst = runtime.InstantiateModule(_module, new RuntimeOptions
            {
                SkipStartFunction = true
            });

            runtime.RegisterModule(Name, modInst);
            if (!runtime.TryGetExportedFunction((Name, "_initialize"), out var startAddr))
                throw new WasmInstantiationException("Wacs module initialization failed");
            var caller = runtime.CreateInvoker(startAddr, new InvokerOptions());
            caller();
            return new WacsInstance(runtime, modInst, Name);
        }
        catch (Exception ex) when (ex is not WasmException)
        {
            throw new WasmInstantiationException($"Failed to instantiate module '{Name}'", ex)
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