namespace HakoJS.Backend.Configuration;

/// <summary>
/// Configuration options for creating a WebAssembly engine.
/// Backends can extend this class for backend-specific options.
/// </summary>
public class WasmEngineOptions
{
    /// <summary>
    /// Enables optimization during compilation.
    /// </summary>
    public bool EnableOptimization { get; init; } = true;

    /// <summary>
    /// Includes debug information in compiled modules.
    /// </summary>
    public bool EnableDebugInfo { get; init; } = false;
}