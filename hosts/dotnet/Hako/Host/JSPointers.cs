using System;
using System.Runtime.InteropServices;

namespace HakoJS.Host;

/// <summary>
/// Represents a pointer to a JSRuntime instance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct JSRuntimePointer(int ptr) : IEquatable<JSRuntimePointer>
{
    private readonly int _ptr = ptr;

    public static JSRuntimePointer Null => new(HakoRegistry.NullPointer);
    public bool IsNull => _ptr == HakoRegistry.NullPointer;

    public static implicit operator int(JSRuntimePointer runtime) => runtime._ptr;
    public static implicit operator JSRuntimePointer(int ptr) => new(ptr);

    public bool Equals(JSRuntimePointer other) => _ptr == other._ptr;
    public override bool Equals(object? obj) => obj is JSRuntimePointer other && Equals(other);
    public override int GetHashCode() => _ptr;
    public override string ToString() => $"JSRuntime(0x{_ptr:X})";

    public static bool operator ==(JSRuntimePointer left, JSRuntimePointer right) => left.Equals(right);
    public static bool operator !=(JSRuntimePointer left, JSRuntimePointer right) => !left.Equals(right);
}

/// <summary>
/// Represents a pointer to a JSContext instance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct JSContextPointer(int ptr) : IEquatable<JSContextPointer>
{
    private readonly int _ptr = ptr;

    public static JSContextPointer Null => new(HakoRegistry.NullPointer);
    public bool IsNull => _ptr == HakoRegistry.NullPointer;

    public static implicit operator int(JSContextPointer context) => context._ptr;
    public static implicit operator JSContextPointer(int ptr) => new(ptr);

    public bool Equals(JSContextPointer other) => _ptr == other._ptr;
    public override bool Equals(object? obj) => obj is JSContextPointer other && Equals(other);
    public override int GetHashCode() => _ptr;
    public override string ToString() => $"JSContext(0x{_ptr:X})";

    public static bool operator ==(JSContextPointer left, JSContextPointer right) => left.Equals(right);
    public static bool operator !=(JSContextPointer left, JSContextPointer right) => !left.Equals(right);
}

/// <summary>
/// Represents a pointer to a JSValue/JSValueConst instance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct JSValuePointer(int ptr) : IEquatable<JSValuePointer>
{
    private readonly int _ptr = ptr;

    public static JSValuePointer Null => new(HakoRegistry.NullPointer);
    public bool IsNull => _ptr == HakoRegistry.NullPointer;

    public static implicit operator int(JSValuePointer value) => value._ptr;
    public static implicit operator JSValuePointer(int ptr) => new(ptr);

    public bool Equals(JSValuePointer other) => _ptr == other._ptr;
    public override bool Equals(object? obj) => obj is JSValuePointer other && Equals(other);
    public override int GetHashCode() => _ptr;
    public override string ToString() => $"JSValue(0x{_ptr:X})";

    public static bool operator ==(JSValuePointer left, JSValuePointer right) => left.Equals(right);
    public static bool operator !=(JSValuePointer left, JSValuePointer right) => !left.Equals(right);
}

/// <summary>
/// Represents a pointer to a JSModuleDef instance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct JSModuleDefPointer(int ptr) : IEquatable<JSModuleDefPointer>
{
    private readonly int _ptr = ptr;

    public static JSModuleDefPointer Null => new(HakoRegistry.NullPointer);
    public bool IsNull => _ptr == HakoRegistry.NullPointer;

    public static implicit operator int(JSModuleDefPointer module) => module._ptr;
    public static implicit operator JSModuleDefPointer(int ptr) => new(ptr);

    public bool Equals(JSModuleDefPointer other) => _ptr == other._ptr;
    public override bool Equals(object? obj) => obj is JSModuleDefPointer other && Equals(other);
    public override int GetHashCode() => _ptr;
    public override string ToString() => $"JSModuleDef(0x{_ptr:X})";

    public static bool operator ==(JSModuleDefPointer left, JSModuleDefPointer right) => left.Equals(right);
    public static bool operator !=(JSModuleDefPointer left, JSModuleDefPointer right) => !left.Equals(right);
}

/// <summary>
/// Represents a JSClassID value.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct JSClassID(int id) : IEquatable<JSClassID>
{
    private readonly int _id = id;

    public static JSClassID Invalid => new(0);
    public bool IsValid => _id != 0;

    public static implicit operator int(JSClassID classId) => classId._id;
    public static implicit operator JSClassID(int id) => new(id);

    public bool Equals(JSClassID other) => _id == other._id;
    public override bool Equals(object? obj) => obj is JSClassID other && Equals(other);
    public override int GetHashCode() => _id;
    public override string ToString() => $"JSClassID({_id})";

    public static bool operator ==(JSClassID left, JSClassID right) => left.Equals(right);
    public static bool operator !=(JSClassID left, JSClassID right) => !left.Equals(right);
}

/// <summary>
/// Represents a pointer to memory allocated by the JS allocator.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct JSMemoryPointer(int ptr) : IEquatable<JSMemoryPointer>
{
    private readonly int _ptr = ptr;

    public static JSMemoryPointer Null => new(HakoRegistry.NullPointer);
    public bool IsNull => _ptr == HakoRegistry.NullPointer;

    public static implicit operator int(JSMemoryPointer ptr) => ptr._ptr;
    public static implicit operator JSMemoryPointer(int ptr) => new(ptr);

    public bool Equals(JSMemoryPointer other) => _ptr == other._ptr;
    public override bool Equals(object? obj) => obj is JSMemoryPointer other && Equals(other);
    public override int GetHashCode() => _ptr;
    public override string ToString() => $"JSMemory(0x{_ptr:X})";

    public static bool operator ==(JSMemoryPointer left, JSMemoryPointer right) => left.Equals(right);
    public static bool operator !=(JSMemoryPointer left, JSMemoryPointer right) => !left.Equals(right);
}

/// <summary>
/// Represents a pointer to a HAKO_PropDescriptor struct in WASM memory.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PropDescriptorPointer(int ptr) : IEquatable<PropDescriptorPointer>
{
    private readonly int _ptr = ptr;

    public static PropDescriptorPointer Null => new(HakoRegistry.NullPointer);
    public bool IsNull => _ptr == HakoRegistry.NullPointer;

    public static implicit operator int(PropDescriptorPointer ptr) => ptr._ptr;
    public static implicit operator PropDescriptorPointer(int ptr) => new(ptr);

    public bool Equals(PropDescriptorPointer other) => _ptr == other._ptr;
    public override bool Equals(object? obj) => obj is PropDescriptorPointer other && Equals(other);
    public override int GetHashCode() => _ptr;
    public override string ToString() => $"PropDescriptor(0x{_ptr:X})";

    public static bool operator ==(PropDescriptorPointer left, PropDescriptorPointer right) => left.Equals(right);
    public static bool operator !=(PropDescriptorPointer left, PropDescriptorPointer right) => !left.Equals(right);
}

/// <summary>
/// Property descriptor flags matching HAKO_PropFlags in hako.h.
/// </summary>
[Flags]
public enum PropFlags : byte
{
    None = 0,
    Configurable = 1 << 0,
    Enumerable = 1 << 1,
    Writable = 1 << 2,
    HasValue = 1 << 3,
    HasWritable = 1 << 4,
    HasGet = 1 << 5,
    HasSet = 1 << 6,
}