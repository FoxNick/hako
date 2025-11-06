using HakoJS.VM;

namespace HakoJS.SourceGeneration;

public readonly struct Uint8ArrayValue(byte[] data) : IJSMarshalable<Uint8ArrayValue>
{
    private readonly byte[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        using var buffer = ctx.NewArrayBuffer(_data);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.Uint8Array);
    }

    public static Uint8ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.Uint8Array)
            throw new ArgumentException($"Expected Uint8Array but got {type}");

        var data = value.CopyTypedArray();
        return new Uint8ArrayValue(data);
    }

    public static implicit operator Uint8ArrayValue(byte[] data)
    {
        return new Uint8ArrayValue(data);
    }

    public static implicit operator byte[](Uint8ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator Uint8ArrayValue(ReadOnlySpan<byte> span)
    {
        return new Uint8ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<byte>(Uint8ArrayValue value)
    {
        return value._data;
    }
}

public readonly struct Int8ArrayValue(sbyte[] data) : IJSMarshalable<Int8ArrayValue>
{
    private readonly sbyte[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        var bytes = new byte[_data.Length];
        Buffer.BlockCopy(_data, 0, bytes, 0, _data.Length);

        using var buffer = ctx.NewArrayBuffer(bytes);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.Int8Array);
    }

    public static Int8ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.Int8Array)
            throw new ArgumentException($"Expected Int8Array but got {type}");

        var bytes = value.CopyTypedArray();
        var data = new sbyte[bytes.Length];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new Int8ArrayValue(data);
    }

    public static implicit operator Int8ArrayValue(sbyte[] data)
    {
        return new Int8ArrayValue(data);
    }

    public static implicit operator sbyte[](Int8ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator Int8ArrayValue(ReadOnlySpan<sbyte> span)
    {
        return new Int8ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<sbyte>(Int8ArrayValue value)
    {
        return value._data;
    }
}

public readonly struct Uint8ClampedArrayValue(byte[] data) : IJSMarshalable<Uint8ClampedArrayValue>
{
    private readonly byte[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        using var buffer = ctx.NewArrayBuffer(_data);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.Uint8ClampedArray);
    }

    public static Uint8ClampedArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.Uint8ClampedArray)
            throw new ArgumentException($"Expected Uint8ClampedArray but got {type}");

        var data = value.CopyTypedArray();
        return new Uint8ClampedArrayValue(data);
    }

    public static implicit operator Uint8ClampedArrayValue(byte[] data)
    {
        return new Uint8ClampedArrayValue(data);
    }

    public static implicit operator byte[](Uint8ClampedArrayValue value)
    {
        return value._data;
    }

    public static implicit operator Uint8ClampedArrayValue(ReadOnlySpan<byte> span)
    {
        return new Uint8ClampedArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<byte>(Uint8ClampedArrayValue value)
    {
        return value._data;
    }
}

public readonly struct Int16ArrayValue(short[] data) : IJSMarshalable<Int16ArrayValue>
{
    private readonly short[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        var bytes = new byte[_data.Length * sizeof(short)];
        Buffer.BlockCopy(_data, 0, bytes, 0, bytes.Length);

        using var buffer = ctx.NewArrayBuffer(bytes);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.Int16Array);
    }

    public static Int16ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.Int16Array)
            throw new ArgumentException($"Expected Int16Array but got {type}");

        var bytes = value.CopyTypedArray();
        var data = new short[bytes.Length / sizeof(short)];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new Int16ArrayValue(data);
    }

    public static implicit operator Int16ArrayValue(short[] data)
    {
        return new Int16ArrayValue(data);
    }

    public static implicit operator short[](Int16ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator Int16ArrayValue(ReadOnlySpan<short> span)
    {
        return new Int16ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<short>(Int16ArrayValue value)
    {
        return value._data;
    }
}

public readonly struct Uint16ArrayValue(ushort[] data) : IJSMarshalable<Uint16ArrayValue>
{
    private readonly ushort[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        var bytes = new byte[_data.Length * sizeof(ushort)];
        Buffer.BlockCopy(_data, 0, bytes, 0, bytes.Length);

        using var buffer = ctx.NewArrayBuffer(bytes);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.Uint16Array);
    }

    public static Uint16ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.Uint16Array)
            throw new ArgumentException($"Expected Uint16Array but got {type}");

        var bytes = value.CopyTypedArray();
        var data = new ushort[bytes.Length / sizeof(ushort)];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new Uint16ArrayValue(data);
    }

    public static implicit operator Uint16ArrayValue(ushort[] data)
    {
        return new Uint16ArrayValue(data);
    }

    public static implicit operator ushort[](Uint16ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator Uint16ArrayValue(ReadOnlySpan<ushort> span)
    {
        return new Uint16ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<ushort>(Uint16ArrayValue value)
    {
        return value._data;
    }
}

public readonly struct Int32ArrayValue(int[] data) : IJSMarshalable<Int32ArrayValue>
{
    private readonly int[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        var bytes = new byte[_data.Length * sizeof(int)];
        Buffer.BlockCopy(_data, 0, bytes, 0, bytes.Length);

        using var buffer = ctx.NewArrayBuffer(bytes);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.Int32Array);
    }

    public static Int32ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.Int32Array)
            throw new ArgumentException($"Expected Int32Array but got {type}");

        var bytes = value.CopyTypedArray();
        var data = new int[bytes.Length / sizeof(int)];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new Int32ArrayValue(data);
    }

    public static implicit operator Int32ArrayValue(int[] data)
    {
        return new Int32ArrayValue(data);
    }

    public static implicit operator int[](Int32ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator Int32ArrayValue(ReadOnlySpan<int> span)
    {
        return new Int32ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<int>(Int32ArrayValue value)
    {
        return value._data;
    }
}

public readonly struct Uint32ArrayValue(uint[] data) : IJSMarshalable<Uint32ArrayValue>
{
    private readonly uint[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        var bytes = new byte[_data.Length * sizeof(uint)];
        Buffer.BlockCopy(_data, 0, bytes, 0, bytes.Length);

        using var buffer = ctx.NewArrayBuffer(bytes);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.Uint32Array);
    }

    public static Uint32ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.Uint32Array)
            throw new ArgumentException($"Expected Uint32Array but got {type}");

        var bytes = value.CopyTypedArray();
        var data = new uint[bytes.Length / sizeof(uint)];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new Uint32ArrayValue(data);
    }

    public static implicit operator Uint32ArrayValue(uint[] data)
    {
        return new Uint32ArrayValue(data);
    }

    public static implicit operator uint[](Uint32ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator Uint32ArrayValue(ReadOnlySpan<uint> span)
    {
        return new Uint32ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<uint>(Uint32ArrayValue value)
    {
        return value._data;
    }
}

public readonly struct Float32ArrayValue(float[] data) : IJSMarshalable<Float32ArrayValue>
{
    private readonly float[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        var bytes = new byte[_data.Length * sizeof(float)];
        Buffer.BlockCopy(_data, 0, bytes, 0, bytes.Length);

        using var buffer = ctx.NewArrayBuffer(bytes);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.Float32Array);
    }

    public static Float32ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.Float32Array)
            throw new ArgumentException($"Expected Float32Array but got {type}");

        var bytes = value.CopyTypedArray();
        var data = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new Float32ArrayValue(data);
    }

    public static implicit operator Float32ArrayValue(float[] data)
    {
        return new Float32ArrayValue(data);
    }

    public static implicit operator float[](Float32ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator Float32ArrayValue(ReadOnlySpan<float> span)
    {
        return new Float32ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<float>(Float32ArrayValue value)
    {
        return value._data;
    }
}

public readonly struct Float64ArrayValue(double[] data) : IJSMarshalable<Float64ArrayValue>
{
    private readonly double[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        var bytes = new byte[_data.Length * sizeof(double)];
        Buffer.BlockCopy(_data, 0, bytes, 0, bytes.Length);

        using var buffer = ctx.NewArrayBuffer(bytes);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.Float64Array);
    }

    public static Float64ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.Float64Array)
            throw new ArgumentException($"Expected Float64Array but got {type}");

        var bytes = value.CopyTypedArray();
        var data = new double[bytes.Length / sizeof(double)];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new Float64ArrayValue(data);
    }

    public static implicit operator Float64ArrayValue(double[] data)
    {
        return new Float64ArrayValue(data);
    }

    public static implicit operator double[](Float64ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator Float64ArrayValue(ReadOnlySpan<double> span)
    {
        return new Float64ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<double>(Float64ArrayValue value)
    {
        return value._data;
    }
}

public readonly struct BigInt64ArrayValue(long[] data) : IJSMarshalable<BigInt64ArrayValue>
{
    private readonly long[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        var bytes = new byte[_data.Length * sizeof(long)];
        Buffer.BlockCopy(_data, 0, bytes, 0, bytes.Length);

        using var buffer = ctx.NewArrayBuffer(bytes);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.BigInt64Array);
    }

    public static BigInt64ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.BigInt64Array)
            throw new ArgumentException($"Expected BigInt64Array but got {type}");

        var bytes = value.CopyTypedArray();
        var data = new long[bytes.Length / sizeof(long)];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new BigInt64ArrayValue(data);
    }

    public static implicit operator BigInt64ArrayValue(long[] data)
    {
        return new BigInt64ArrayValue(data);
    }

    public static implicit operator long[](BigInt64ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator BigInt64ArrayValue(ReadOnlySpan<long> span)
    {
        return new BigInt64ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<long>(BigInt64ArrayValue value)
    {
        return value._data;
    }
}

public readonly struct BigUint64ArrayValue(ulong[] data) : IJSMarshalable<BigUint64ArrayValue>
{
    private readonly ulong[] _data = data ?? throw new ArgumentNullException(nameof(data));

    public int Length => _data.Length;

    public JSValue ToJSValue(Realm ctx)
    {
        var bytes = new byte[_data.Length * sizeof(ulong)];
        Buffer.BlockCopy(_data, 0, bytes, 0, bytes.Length);

        using var buffer = ctx.NewArrayBuffer(bytes);
        return ctx.NewTypedArrayWithBuffer(buffer, 0, _data.Length, TypedArrayType.BigUint64Array);
    }

    public static BigUint64ArrayValue FromJSValue(Realm ctx, JSValue value)
    {
        if (!value.IsTypedArray())
            throw new ArgumentException("Value is not a TypedArray");

        var type = value.GetTypedArrayType();
        if (type != TypedArrayType.BigUint64Array)
            throw new ArgumentException($"Expected BigUint64Array but got {type}");

        var bytes = value.CopyTypedArray();
        var data = new ulong[bytes.Length / sizeof(ulong)];
        Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
        return new BigUint64ArrayValue(data);
    }

    public static implicit operator BigUint64ArrayValue(ulong[] data)
    {
        return new BigUint64ArrayValue(data);
    }

    public static implicit operator ulong[](BigUint64ArrayValue value)
    {
        return value._data;
    }

    public static implicit operator BigUint64ArrayValue(ReadOnlySpan<ulong> span)
    {
        return new BigUint64ArrayValue(span.ToArray());
    }

    public static implicit operator ReadOnlySpan<ulong>(BigUint64ArrayValue value)
    {
        return value._data;
    }
}