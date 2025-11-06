using System;
using System.Text;
using HakoJS.Backend.Core;
using Wasmtime;

namespace HakoJS.Backend.Wasmtime;

public sealed class WasmtimeMemory : WasmMemory
{
    private bool _disposed;

    internal WasmtimeMemory(Memory memory)
    {
        UnderlyingMemory = memory;
    }

    internal Memory UnderlyingMemory { get; }

    public override long Size => UnderlyingMemory.GetLength();

    public override Span<byte> GetSpan(int offset, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return UnderlyingMemory.GetSpan(offset, length);
    }

    public override bool Grow(uint deltaPages)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            UnderlyingMemory.Grow(deltaPages);
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
        encoding ??= Encoding.UTF8;
        return UnderlyingMemory.ReadNullTerminatedString(offset);
    }

    public override string ReadString(int offset, int length, Encoding? encoding = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        encoding ??= Encoding.UTF8;
        return UnderlyingMemory.ReadString(offset, length, encoding);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed) _disposed = true;
        base.Dispose(disposing);
    }
}