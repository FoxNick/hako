using System;
using System.Diagnostics.CodeAnalysis;
using HakoJS.Backend.Core;
using Wacs.Core.Runtime;
using Wacs.Core.Runtime.Types;
[assembly: UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Wasmtime callback wrappers work correctly with AOT")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Wasmtime callback wrappers work correctly with AOT")]

namespace HakoJS.Backend.Wacs;

public sealed class WacsInstance : WasmInstance
{
    private readonly ModuleInstance _moduleInstance;
    private readonly string _moduleName;
    private readonly WasmRuntime _runtime;
    private bool _disposed;

    internal WacsInstance(WasmRuntime runtime, ModuleInstance moduleInstance, string moduleName)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _moduleInstance = moduleInstance ?? throw new ArgumentNullException(nameof(moduleInstance));
        _moduleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
    }

    public override WasmMemory? GetMemory(string name)
    {
        throw new NotImplementedException();
    }

    private FuncAddr? GetFunctionAddress(string name)
    {
        if (_runtime.TryGetExportedFunction((_moduleName, name), out var funcAddr))
            return funcAddr;
        return null;
    }

    // ===== Int32 Functions =====

    public override Func<int>? GetFunctionInt32(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return () =>
        {
            var results = invoker([]);
            return results[0];
        };
    }

    public override Func<int, int>? GetFunctionInt32<T1>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return a1 =>
        {
            var results = invoker([new Value(a1)]);
            return results[0];
        };
    }

    public override Func<int, int, int>? GetFunctionInt32<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2) =>
        {
            var results = invoker([new Value(a1), new Value(a2)]);
            return results[0];
        };
    }

    public override Func<int, int, int, int>? GetFunctionInt32<T1, T2, T3>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3) =>
        {
            var results = invoker([new Value(a1), new Value(a2), new Value(a3)]);
            return results[0];
        };
    }

    public override Func<int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4) =>
        {
            var results = invoker([new Value(a1), new Value(a2), new Value(a3), new Value(a4)]);
            return results[0];
        };
    }

    public override Func<int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5) =>
        {
            var results = invoker([new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5)]);
            return results[0];
        };
    }

    public override Func<int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6) =>
        {
            var results = invoker([
                new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6)
            ]);
            return results[0];
        };
    }

    public override Func<int, int, int, int, int, int, int, int>?
        GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7) =>
        {
            var results = invoker([
                new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7)
            ]);
            return results[0];
        };
    }

    public override Func<int, int, int, int, int, int, int, int, int>?
        GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7, T8>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7, a8) =>
        {
            var results = invoker([
                new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7),
                new Value(a8)
            ]);
            return results[0];
        };
    }

    public override Func<int, int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7,
        T8, T9>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7, a8, a9) =>
        {
            var results = invoker([
                new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7),
                new Value(a8), new Value(a9)
            ]);
            return results[0];
        };
    }

    public override Func<int, int, int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5,
        T6, T7, T8, T9, T10>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) =>
        {
            var results = invoker([
                new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7),
                new Value(a8), new Value(a9), new Value(a10)
            ]);
            return results[0];
        };
    }

    public override Func<int, int, int, int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4,
        T5, T6, T7, T8, T9, T10, T11>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) =>
        {
            var results = invoker([
                new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7),
                new Value(a8), new Value(a9), new Value(a10), new Value(a11)
            ]);
            return results[0];
        };
    }

    // ===== Int64 Functions =====

    public override Func<long>? GetFunctionInt64(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return () =>
        {
            var results = invoker([]);
            return results[0];
        };
    }

    public override Func<int, long>? GetFunctionInt64<T1>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return a1 =>
        {
            var results = invoker([new Value(a1)]);
            return results[0];
        };
    }

    public override Func<int, int, long>? GetFunctionInt64<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2) =>
        {
            var results = invoker([new Value(a1), new Value(a2)]);
            return results[0];
        };
    }

    // ===== Double Functions =====

    public override Func<double>? GetFunctionDouble(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return () =>
        {
            var results = invoker([]);
            return results[0];
        };
    }

    public override Func<int, double>? GetFunctionDouble<T1>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return a1 =>
        {
            var results = invoker([new Value(a1)]);
            return results[0];
        };
    }

    public override Func<int, int, double>? GetFunctionDouble<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2) =>
        {
            var results = invoker([new Value(a1), new Value(a2)]);
            return results[0];
        };
    }

    // ===== Mixed Functions =====

    public override Func<int, double, int>? GetFunctionInt32WithDouble<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2) =>
        {
            var results = invoker([new Value(a1), new Value(a2)]);
            return results[0];
        };
    }

    public override Func<int, long, int>? GetFunctionInt32WithLong<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2) =>
        {
            var results = invoker([new Value(a1), new Value(a2)]);
            return results[0];
        };
    }

    // ===== Actions =====

    public override Action? GetAction(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return () => invoker([]);
    }

    public override Action<int>? GetAction<T1>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return a1 => invoker([new Value(a1)]);
    }

    public override Action<int, int>? GetAction<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2) => invoker([new Value(a1), new Value(a2)]);
    }

    public override Action<int, long>? GetActionWithLong<T1, T2>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2) => invoker([new Value(a1), new Value(a2)]);
    }

    public override Action<int, int, int>? GetAction<T1, T2, T3>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3) => invoker([new Value(a1), new Value(a2), new Value(a3)]);
    }

    public override Action<int, int, int, int>? GetAction<T1, T2, T3, T4>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4) => invoker([new Value(a1), new Value(a2), new Value(a3), new Value(a4)]);
    }

    public override Action<int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5) =>
            invoker([new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5)]);
    }

    public override Action<int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6) => invoker([
            new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6)
        ]);
    }

    public override Action<int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7) => invoker([
            new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7)
        ]);
    }

    public override Action<int, int, int, int, int, int, int, int>?
        GetAction<T1, T2, T3, T4, T5, T6, T7, T8>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7, a8) => invoker([
            new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7),
            new Value(a8)
        ]);
    }

    public override Action<int, int, int, int, int, int, int, int, int>?
        GetAction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7, a8, a9) => invoker([
            new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7),
            new Value(a8), new Value(a9)
        ]);
    }

    public override Action<int, int, int, int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7, T8,
        T9, T10>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) => invoker([
            new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7),
            new Value(a8), new Value(a9), new Value(a10)
        ]);
    }

    public override Action<int, int, int, int, int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7,
        T8, T9, T10, T11>(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var funcAddr = GetFunctionAddress(name);
        if (funcAddr == null) return null;

        var invoker = _runtime.CreateStackInvoker(funcAddr.Value);
        return (a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) => invoker([
            new Value(a1), new Value(a2), new Value(a3), new Value(a4), new Value(a5), new Value(a6), new Value(a7),
            new Value(a8), new Value(a9), new Value(a10), new Value(a11)
        ]);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
    }
}