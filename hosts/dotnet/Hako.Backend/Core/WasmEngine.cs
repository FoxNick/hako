namespace HakoJS.Backend.Core;

using HakoJS.Backend.Configuration;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
/// <summary>
/// Factory interface for WebAssembly engines.
/// </summary>
public interface IWasmEngineFactory<out TSelf> where TSelf : WasmEngine
{
    /// <summary>
    /// Creates a new instance of the WebAssembly engine.
    /// </summary>
    static abstract TSelf Create(WasmEngineOptions options);
}

/// <summary>
/// Factory for creating WebAssembly runtime environments and compiling modules.
/// </summary>
public abstract class WasmEngine : IDisposable
{
    /// <summary>
    /// Creates a new isolated execution store.
    /// </summary>
    public abstract WasmStore CreateStore(WasmStoreOptions? options = null);

    /// <summary>
    /// Compiles a WebAssembly module from bytecode.
    /// </summary>
    public abstract WasmModule CompileModule(ReadOnlySpan<byte> wasmBytes, string name);

    /// <summary>
    /// Loads and compiles a WebAssembly module from a file.
    /// </summary>
    public abstract WasmModule LoadModule(string path, string name);

    /// <summary>
    /// Loads and compiles a WebAssembly module from a stream.
    /// </summary>
    public abstract WasmModule LoadModule(Stream stream, string name);

    protected virtual void Dispose(bool disposing) { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}