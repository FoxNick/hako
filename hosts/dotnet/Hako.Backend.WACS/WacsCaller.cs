using System;
using HakoJS.Backend.Core;
using Wacs.Core.Runtime;
using Wacs.Core.Runtime.Types;

namespace HakoJS.Backend.Wacs;

public sealed class WacsCaller : WasmCaller
{
    private readonly MemoryInstance? _boundMemory;
    private readonly ExecContext _execContext;
    private readonly WasmRuntime _runtime;

    internal WacsCaller(ExecContext execContext, WasmRuntime runtime, MemoryInstance? boundMemory)
    {
        _execContext = execContext ?? throw new ArgumentNullException(nameof(execContext));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _boundMemory = boundMemory;
    }

    public override WasmMemory? GetMemory(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // First try the bound memory if it exists (typical case for "memory" export)
        if (_boundMemory != null && name == "memory")
            return new WacsMemory(_boundMemory);

        // Otherwise try to look it up by name
        // WACS doesn't provide a way to query memory by name from ExecContext,
        // so we return null if no bound memory matches
        return null;
    }
}