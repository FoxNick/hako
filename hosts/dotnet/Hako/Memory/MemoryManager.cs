using System.Text;
using HakoJS.Backend.Core;
using HakoJS.Host;
using HakoJS.Lifetime;

namespace HakoJS.Memory;

internal class MemoryManager
{
    private readonly WasmMemory _memory;
    private readonly HakoRegistry _registry;
    private readonly UTF8Encoding _utf8Encoding;

    internal MemoryManager(HakoRegistry registry, WasmMemory memory)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _utf8Encoding = new UTF8Encoding(false, true);
    }

    private WasmMemory Memory => _memory ?? throw new InvalidOperationException("Memory not initialized");

    #region Memory Allocation

    public DisposableValue<int> AllocateMemory(int ctx, int size)
    {
        if (size <= 0)
            throw new ArgumentException("Size must be greater than 0", nameof(size));

        var ptr = _registry.Malloc(ctx, size);
        if (ptr == 0)
            throw new InvalidOperationException($"Failed to allocate {size} bytes of memory");

        return new DisposableValue<int>(ptr, p => FreeMemory(ctx, p));
    }

    public DisposableValue<int> AllocateRuntimeMemory(int rt, int size)
    {
        if (size <= 0)
            throw new ArgumentException("Size must be greater than 0", nameof(size));

        var ptr = _registry.RuntimeMalloc(rt, size);
        if (ptr == 0)
            throw new InvalidOperationException($"Failed to allocate {size} bytes of memory");

        return new DisposableValue<int>(ptr, p => FreeRuntimeMemory(rt, p));
    }

    public DisposableValue<int> AllocateRuntimeString(int rt, string str, out int length)
    {
        ArgumentNullException.ThrowIfNull(str);

        var bytes = _utf8Encoding.GetBytes(str);
        int ptr = AllocateRuntimeMemory(rt, bytes.Length + 1);

        var memorySpan = Memory.GetSpan(ptr, bytes.Length + 1);
        bytes.AsSpan().CopyTo(memorySpan);
        memorySpan[bytes.Length] = 0;
        length = bytes.Length - 1;
        return new DisposableValue<int>(ptr, p => FreeRuntimeMemory(rt, p));
    }

    public void FreeMemory(int ctx, int ptr)
    {
        if (ptr != HakoRegistry.NullPointer) 
            _registry.Free(ctx, ptr);
    }

    public void FreeRuntimeMemory(int rt, int ptr)
    {
        if (ptr != HakoRegistry.NullPointer) 
            _registry.RuntimeFree(rt, ptr);
    }

    #endregion

    #region String Operations

    public DisposableValue<int> AllocateString(int ctx, string str, out int length)
    {
        ArgumentNullException.ThrowIfNull(str);

        var bytes = _utf8Encoding.GetBytes(str);
        int ptr = AllocateMemory(ctx, bytes.Length + 1);

        var memorySpan = Memory.GetSpan(ptr, bytes.Length + 1);
        bytes.AsSpan().CopyTo(memorySpan);
        memorySpan[bytes.Length] = 0; // Null terminator
        length = memorySpan.Length - 1;
        return new DisposableValue<int>(ptr, p => FreeMemory(ctx, p));
    }


    public DisposableValue<(int Pointer, int Length)> WriteNullTerminatedString(int ctx, string str)
    {
        ArgumentNullException.ThrowIfNull(str);

        var bytes = _utf8Encoding.GetBytes(str);
        int ptr = AllocateMemory(ctx, bytes.Length + 1);

        var memorySpan = Memory.GetSpan(ptr, bytes.Length + 1);
        bytes.AsSpan().CopyTo(memorySpan);
        memorySpan[bytes.Length] = 0;

        return new DisposableValue<(int Pointer, int Length)>((ptr, bytes.Length), p => FreeMemory(ctx, p.Pointer));
    }

    public string ReadNullTerminatedString(int ptr)
    {
        if (ptr == HakoRegistry.NullPointer)
        {
            Console.Error.WriteLine("Reading null pointer");
            return string.Empty;
        }

        return Memory.ReadNullTerminatedString(ptr);
    }

    public string ReadString(int ptr, int length, Encoding? encoding = null)
    {
        if (ptr == HakoRegistry.NullPointer)
        {
            Console.Error.WriteLine("Reading null pointer");
            return string.Empty;
        }
        encoding ??= Encoding.UTF8;

        return Memory.ReadString(ptr, length, encoding);
    }

    public void FreeCString(int ctx, int ptr)
    {
        if (ptr != HakoRegistry.NullPointer) 
            _registry.FreeCString(ctx, ptr);
    }

    #endregion

    #region Byte Operations

    public int WriteBytes(int ctx, ReadOnlySpan<byte> bytes)
    {
        int ptr = AllocateMemory(ctx, bytes.Length);
        var memorySpan = Memory.GetSpan(ptr, bytes.Length);
        bytes.CopyTo(memorySpan);
        return ptr;
    }

    public byte[] Copy(int offset, int length)
    {
        if (length <= 0)
            return [];

        var result = new byte[length];
        Memory.GetSpan(offset, length).CopyTo(result);
        return result;
    }

    public Span<byte> Slice(int offset, int length)
    {
        return Memory.GetSpan(offset, length);
    }

    #endregion

    #region Value Pointer Operations

    public void FreeValuePointer(int ctx, int ptr)
    {
        if (ptr != HakoRegistry.NullPointer) 
            _registry.FreeValuePointer(ctx, ptr);
    }

    public void FreeValuePointerRuntime(int rt, int ptr)
    {
        if (ptr != HakoRegistry.NullPointer) 
            _registry.FreeValuePointerRuntime(rt, ptr);
    }

    public int DupValuePointer(int ctx, int ptr)
    {
        return _registry.DupValuePointer(ctx, ptr);
    }

    public int NewArrayBuffer(int ctx, ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) 
            return _registry.NewArrayBuffer(ctx, HakoRegistry.NullPointer, 0);

        int bufPtr = AllocateMemory(ctx, data.Length);
        var memorySpan = Memory.GetSpan(bufPtr, data.Length);
        data.CopyTo(memorySpan);

        return _registry.NewArrayBuffer(ctx, bufPtr, data.Length);
    }

    #endregion

    #region Pointer Array Operations

    public DisposableValue<int> AllocatePointerArray(int ctx, int count)
    {
        return AllocateMemory(ctx, count * sizeof(int));
    }

    public DisposableValue<int> AllocateRuntimePointerArray(int rt, int count)
    {
        return AllocateRuntimeMemory(rt, count * sizeof(int));
    }

    public int WritePointerToArray(int arrayPtr, int index, int value)
    {
        var address = arrayPtr + index * sizeof(int);
        WriteUint32(address, (uint)value);
        return address;
    }

    public int ReadPointerFromArray(int arrayPtr, int index)
    {
        var address = arrayPtr + index * sizeof(int);
        return (int)ReadUint32(address);
    }

    public int ReadPointer(int address)
    {
        return (int)ReadUint32(address);
    }

    #endregion

    #region Low-Level Memory Operations

    public uint ReadUint32(int address)
    {
        var span = Memory.GetSpan(address, sizeof(uint));
        return BitConverter.ToUInt32(span);
    }

    public void WriteUint32(int address, uint value)
    {
        var span = Memory.GetSpan(address, sizeof(uint));
        BitConverter.TryWriteBytes(span, value);
    }

    public long ReadInt64(int address)
    {
        var span = Memory.GetSpan(address, sizeof(long));
        return BitConverter.ToInt64(span);
    }

    public void WriteInt64(int address, long value)
    {
        var span = Memory.GetSpan(address, sizeof(long));
        BitConverter.TryWriteBytes(span, value);
    }

    #endregion

    #region PropDescriptor Operations

    private const int PropDescriptorSize = 12; // value/get (4) + set (4) + flags (1) + padding (3)

    /// <summary>
    /// Allocates a PropDescriptor for a data property (value-based).
    /// </summary>
    public DisposableValue<PropDescriptorPointer> AllocateDataPropertyDescriptor(
        int ctx,
        JSValuePointer value,
        PropFlags flags)
    {
        int ptr = AllocateMemory(ctx, PropDescriptorSize);

        // Write value pointer at offset 0
        WriteUint32(ptr, (uint)(int)value);
        // Write 0 for set pointer at offset 4 (not used for data descriptor)
        WriteUint32(ptr + 4, 0);
        // Write flags at offset 8
        Memory.GetSpan(ptr + 8, 1)[0] = (byte)(flags | PropFlags.HasValue);

        return new DisposableValue<PropDescriptorPointer>(
            new PropDescriptorPointer(ptr),
            p => FreeMemory(ctx, p));
    }

    /// <summary>
    /// Allocates a PropDescriptor for an accessor property (getter/setter).
    /// </summary>
    public DisposableValue<PropDescriptorPointer> AllocateAccessorPropertyDescriptor(
        int ctx,
        JSValuePointer getter,
        JSValuePointer setter,
        PropFlags flags)
    {
        int ptr = AllocateMemory(ctx, PropDescriptorSize);

        // Write getter pointer at offset 0
        WriteUint32(ptr, (uint)(int)getter);
        // Write setter pointer at offset 4
        WriteUint32(ptr + 4, (uint)(int)setter);
        // Write flags at offset 8 (include HasGet/HasSet based on non-null pointers)
        var effectiveFlags = flags;
        if (!getter.IsNull)
            effectiveFlags |= PropFlags.HasGet;
        if (!setter.IsNull)
            effectiveFlags |= PropFlags.HasSet;
        Memory.GetSpan(ptr + 8, 1)[0] = (byte)effectiveFlags;

        return new DisposableValue<PropDescriptorPointer>(
            new PropDescriptorPointer(ptr),
            p => FreeMemory(ctx, p));
    }

    #endregion
}