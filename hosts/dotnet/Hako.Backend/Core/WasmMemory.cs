using System.Text;

namespace HakoJS.Backend.Core;

/// <summary>
/// Provides access to WebAssembly linear memory.
/// </summary>
public abstract class WasmMemory : IDisposable
{
    /// <summary>
    /// Gets a span view of memory at the specified offset and length.
    /// </summary>
    public abstract Span<byte> GetSpan(int offset, int length);

    /// <summary>
    /// Gets the current memory size in bytes.
    /// </summary>
    public abstract long Size { get; }

    /// <summary>
    /// Attempts to grow the memory by the specified number of pages.
    /// </summary>
    /// <returns>True if growth succeeded, false otherwise.</returns>
    public abstract bool Grow(uint deltaPages);

    /// <summary>
    /// Reads a null-terminated UTF-8 string from memory.
    /// </summary>
    /// <param name="offset">The offset in memory where the string begins</param>
    /// <param name="encoding">Optional encoding (defaults to UTF8)</param>
    public abstract string ReadNullTerminatedString(int offset, Encoding? encoding = null);
    
    /// <summary>
    /// Reads a UTF-8 string from memory
    /// </summary>
    /// <param name="offset">The offset in memory where the string begins</param>
    /// <param name="length">The length of the string</param>
    /// <param name="encoding">Optional encoding (defaults to UTF8)</param>
    /// <returns></returns>
    public abstract string ReadString(int offset, int length, Encoding? encoding = null);

    protected virtual void Dispose(bool disposing) { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}