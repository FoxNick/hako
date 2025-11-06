namespace HakoJS.Backend.Configuration;

/// <summary>
/// WASI environment configuration.
/// </summary>
public sealed class HakoWasiConfiguration
{
    /// <summary>
    /// Command-line arguments. Null to inherit from host process.
    /// </summary>
    public string[]? Arguments { get; init; }

    /// <summary>
    /// Environment variables. Null to inherit from host process.
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; init; }

    /// <summary>
    /// Inherits host process environment if EnvironmentVariables is null.
    /// </summary>
    public bool InheritEnvironment { get; init; } = false;

    /// <summary>
    /// Inherits host standard I/O streams.
    /// </summary>
    public bool InheritStdio { get; init; } = true;

    /// <summary>
    /// Directory to preopen for filesystem access. Null for no filesystem access.
    /// </summary>
    public string? PreopenDirectory { get; init; }
}