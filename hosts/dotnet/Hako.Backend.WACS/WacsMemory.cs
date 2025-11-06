using System;
using System.Text;
using HakoJS.Backend.Core;
using Wacs.Core.Runtime.Types;

namespace HakoJS.Backend.Wacs;

public sealed class WacsMemory : WasmMemory
{
    private bool _disposed;
    internal MemoryInstance MemoryInstance;

    internal WacsMemory(MemoryInstance memoryInstance)
    {
        MemoryInstance = memoryInstance ?? throw new ArgumentNullException(nameof(memoryInstance));
    }

    public override long Size => MemoryInstance.Data.Length;

    public override Span<byte> GetSpan(int offset, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return MemoryInstance.Data.AsSpan(offset, length);
    }

    public override bool Grow(uint deltaPages)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            MemoryInstance.Grow(deltaPages);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override string ReadNullTerminatedString(int offset, Encoding? encoding = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (offset < 0 || offset >= Size)
            return string.Empty;

        encoding ??= Encoding.UTF8; 
        var end = offset;
        var memory = MemoryInstance.Data.AsSpan();

        while (end < memory.Length && memory[end] != 0)
            end++;

        var length = end - offset;
        if (length == 0)
            return string.Empty;

        return encoding.GetString(memory.Slice(offset, length));
    }

    public override string ReadString(int offset, int length, Encoding? encoding = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (offset < 0 || offset >= Size || length > Size) return string.Empty;
        encoding ??= Encoding.UTF8; 
        var memory = MemoryInstance.Data.AsSpan();
        return encoding.GetString(memory.Slice(offset, length));
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
    }
}