namespace HakoJS.Backend.Configuration;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
/// <summary>
/// Configuration options for creating a WebAssembly store.
/// Backends can extend this class for backend-specific options.
/// </summary>
public class WasmStoreOptions
{
    /// <summary>
    /// WASI configuration for this store.
    /// </summary>
    public HakoWasiConfiguration? WasiConfiguration { get; init; } = new();

    /// <summary>
    /// Maximum memory limit in bytes. Null for no limit.
    /// </summary>
    public long? MaxMemoryBytes { get; init; }
    
    public uint InitialMemoryPages { get; set; } = 384;
    public uint MaximumMemoryPages { get; set; } = 4096;
}