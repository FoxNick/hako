namespace HakoJS.Backend.Extensions;

using HakoJS.Backend.Core;

/// <summary>
/// Extension methods for WasmMemory operations.
/// </summary>
public static class WasmMemoryExtensions
{
    /// <summary>
    /// Writes bytes to memory at the specified offset.
    /// </summary>
    public static void WriteBytes(this WasmMemory memory, int offset, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return;
        bytes.CopyTo(memory.GetSpan(offset, bytes.Length));
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer from memory.
    /// </summary>
    public static uint ReadUInt32(this WasmMemory memory, int offset)
    {
        var span = memory.GetSpan(offset, sizeof(uint));
        return BitConverter.ToUInt32(span);
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer to memory.
    /// </summary>
    public static void WriteUInt32(this WasmMemory memory, int offset, uint value)
    {
        var span = memory.GetSpan(offset, sizeof(uint));
        BitConverter.TryWriteBytes(span, value);
    }

    /// <summary>
    /// Reads a 64-bit signed integer from memory.
    /// </summary>
    public static long ReadInt64(this WasmMemory memory, int offset)
    {
        var span = memory.GetSpan(offset, sizeof(long));
        return BitConverter.ToInt64(span);
    }

    /// <summary>
    /// Writes a 64-bit signed integer to memory.
    /// </summary>
    public static void WriteInt64(this WasmMemory memory, int offset, long value)
    {
        var span = memory.GetSpan(offset, sizeof(long));
        BitConverter.TryWriteBytes(span, value);
    }

    /// <summary>
    /// Copies a region of memory to a new byte array.
    /// </summary>
    public static byte[] Copy(this WasmMemory memory, int offset, int length)
    {
        if (length <= 0) return [];
        
        var result = new byte[length];
        memory.GetSpan(offset, length).CopyTo(result);
        return result;
    }

    /// <summary>
    /// Gets a slice view of memory as a span.
    /// </summary>
    public static Span<byte> Slice(this WasmMemory memory, int offset, int length) 
        => memory.GetSpan(offset, length);
}
