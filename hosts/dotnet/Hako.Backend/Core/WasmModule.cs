namespace HakoJS.Backend.Core;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
/// <summary>
/// A compiled WebAssembly module ready for instantiation.
/// </summary>
public abstract class WasmModule : IDisposable
{
    /// <summary>
    /// Gets the module name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Instantiates this module using the provided linker.
    /// </summary>
    public abstract WasmInstance Instantiate(WasmLinker linker);

    protected virtual void Dispose(bool disposing) { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}