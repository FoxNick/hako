namespace HakoJS.Backend.Core;

/// <summary>
/// Provides context to host functions during invocation.
/// </summary>
public abstract class WasmCaller
{
    /// <summary>
    /// Gets an exported memory by name from the calling instance.
    /// </summary>
    public abstract WasmMemory? GetMemory(string name);
}