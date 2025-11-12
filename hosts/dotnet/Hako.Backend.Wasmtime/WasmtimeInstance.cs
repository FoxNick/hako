using System;
using System.Diagnostics.CodeAnalysis;
using HakoJS.Backend.Core;
using Wasmtime;
[assembly: UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Wasmtime callback wrappers work correctly with AOT")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Wasmtime callback wrappers work correctly with AOT")]

namespace HakoJS.Backend.Wasmtime;

public sealed class WasmtimeInstance : WasmInstance
{
    private readonly Instance _instance;
    private bool _disposed;

    internal WasmtimeInstance(Instance instance)
    {
        _instance = instance;
    }

    public override WasmMemory? GetMemory(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var memory = _instance.GetMemory(name);
        return memory != null ? new WasmtimeMemory(memory) : null;
    }

    public override Func<int>? GetFunctionInt32(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int>(name);
    }

    public override Func<int, int>? GetFunctionInt32<T1>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int>(name);
    }

    public override Func<int, int, int>? GetFunctionInt32<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int>(name);
    }

    public override Func<int, int, int, int>? GetFunctionInt32<T1, T2, T3>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int, int>(name);
    }

    public override Func<int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int, int, int>(name);
    }

    public override Func<int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int, int, int, int>(name);
    }

    public override Func<int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int, int, int, int, int>(name);
    }

    public override Func<int, int, int, int, int, int, int, int>?
        GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int, int, int, int, int, int>(name);
    }

    public override Func<int, int, int, int, int, int, int, int, int>?
        GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7, T8>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int, int, int, int, int, int, int>(name);
    }

    public override Func<int, int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7,
        T8, T9>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int, int, int, int, int, int, int, int>(name);
    }

    public override Func<int, int, int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5,
        T6, T7, T8, T9, T10>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int, int, int, int, int, int, int, int, int>(name);
    }

    public override Func<int, int, int, int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4,
        T5, T6, T7, T8, T9, T10, T11>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, int, int, int, int, int, int, int, int, int, int>(name);
    }

    public override Func<long>? GetFunctionInt64(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<long>(name);
    }

    public override Func<int, long>? GetFunctionInt64<T1>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, long>(name);
    }

    public override Func<int, int, long>? GetFunctionInt64<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, long>(name);
    }

    public override Func<double>? GetFunctionDouble(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<double>(name);
    }

    public override Func<int, double>? GetFunctionDouble<T1>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, double>(name);
    }

    public override Func<int, int, double>? GetFunctionDouble<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, int, double>(name);
    }

    public override Func<int, double, int>? GetFunctionInt32WithDouble<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, double, int>(name);
    }
    
    public override Func<int, long, int>? GetFunctionInt32WithLong<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction<int, long, int>(name);
    }

    public override Action? GetAction(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetAction(name);
    }

    public override Action<int>? GetAction<T1>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetAction<int>(name);
    }

    public override Action<int, int>? GetAction<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetAction<int, int>(name);
    }

    public override Action<int, long>? GetActionWithLong<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetAction<int, long>(name);
    }

    public override Action<int, int, int>? GetAction<T1, T2, T3>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetAction<int, int, int>(name);
    }

    public override Action<int, int, int, int>? GetAction<T1, T2, T3, T4>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetAction<int, int, int, int>(name);
    }

    public override Action<int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetAction<int, int, int, int, int>(name);
    }

    public override Action<int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction(name)?.WrapAction<int, int, int, int, int, int>();
    }

    public override Action<int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction(name)?.WrapAction<int, int, int, int, int, int, int>();
    }

    public override Action<int, int, int, int, int, int, int, int>?
        GetAction<T1, T2, T3, T4, T5, T6, T7, T8>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction(name)?.WrapAction<int, int, int, int, int, int, int, int>();
    }

    public override Action<int, int, int, int, int, int, int, int, int>?
        GetAction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction(name)?.WrapAction<int, int, int, int, int, int, int, int, int>();
    }

    public override Action<int, int, int, int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7, T8,
        T9, T10>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction(name)?.WrapAction<int, int, int, int, int, int, int, int, int, int>();
    }

    public override Action<int, int, int, int, int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7,
        T8, T9, T10, T11>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _instance.GetFunction(name)?.WrapAction<int, int, int, int, int, int, int, int, int, int, int>();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed) _disposed = true;
        base.Dispose(disposing);
    }
}