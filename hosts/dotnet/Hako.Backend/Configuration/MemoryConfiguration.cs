namespace HakoJS.Backend.Configuration;

/// <summary>
/// WebAssembly memory configuration.
/// </summary>
public sealed class MemoryConfiguration
{
    /// <summary>
    /// Initial memory size in pages (64KB per page).
    /// </summary>
    public required uint InitialPages { get; init; }

    /// <summary>
    /// Maximum memory size in pages. Null for no limit.
    /// </summary>
    public uint? MaximumPages { get; init; }

    /// <summary>
    /// Whether memory can be shared between threads.
    /// </summary>
    public bool Shared { get; init; } = false;
}