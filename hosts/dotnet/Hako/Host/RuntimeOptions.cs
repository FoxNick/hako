using HakoJS.Backend.Configuration;
using HakoJS.Backend.Core;

namespace HakoJS.Host;

public class HakoOptions<TEngine> where TEngine : WasmEngine, IWasmEngineFactory<TEngine>
{
    public string? WasmPath { get; set; }
    public StripOptions? StripOptions { get; set; }
    public int MemoryLimitBytes { get; set; } = -1;
    public WasmEngineOptions? EngineOptions { get; set; }
    public WasmStoreOptions? StoreOptions { get; set; }
}