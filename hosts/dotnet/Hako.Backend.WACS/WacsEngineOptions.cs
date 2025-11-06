using HakoJS.Backend.Configuration;

namespace HakoJS.Backend.Wacs;

public sealed class WacsEngineOptions : WasmEngineOptions
{
    public bool TranspileModules { get; init; } = true;
    public bool TraceExecution { get; init; } = false;
}