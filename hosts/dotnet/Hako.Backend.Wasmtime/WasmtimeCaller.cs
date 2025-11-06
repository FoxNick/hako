using HakoJS.Backend.Core;
using Wasmtime;

namespace HakoJS.Backend.Wasmtime;

public sealed class WasmtimeCaller : WasmCaller
{
    internal WasmtimeCaller(Caller caller)
    {
        //  _caller = caller;
    }

    public override WasmMemory? GetMemory(string name)
    {
        return null;
    }
}