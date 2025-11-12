using HakoJS.Backend.Configuration;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace HakoJS.Backend.Core;

/// <summary>
/// An isolated execution environment for WebAssembly instances.
/// </summary>
public abstract class WasmStore : IDisposable
{
    /// <summary>
    /// Creates a new linker for defining imports.
    /// </summary>
    public abstract WasmLinker CreateLinker();

    /// <summary>
    /// Creates a new WebAssembly memory instance.
    /// </summary>
    public abstract WasmMemory CreateMemory(MemoryConfiguration config);

    protected virtual void Dispose(bool disposing) { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}