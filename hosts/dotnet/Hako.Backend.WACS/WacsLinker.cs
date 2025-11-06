using System;
using HakoJS.Backend.Configuration;
using HakoJS.Backend.Core;
using Wacs.Core.Runtime;
using Wacs.Core.Runtime.Types;

namespace HakoJS.Backend.Wacs;

public sealed class WacsLinker : WasmLinker
{
    private MemoryInstance? _boundMemory;
    private bool _disposed;

    internal WacsLinker(WacsStore store, WasmStoreOptions options)
    {
        Store = store;

        // Automatically create and bind memory with configured pages
        var memory = store.CreateMemory(options.InitialMemoryPages, options.MaximumMemoryPages);
        DefineMemory("env", "memory", memory);
    }

    internal WacsStore Store { get; }

    public override void DefineWasi()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Wasi?.BindToRuntime(Store.Runtime);
    }

    public override void DefineMemory(string module, string name, WasmMemory memory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(memory);

        if (memory is not WacsMemory wacsMemory)
            throw new ArgumentException("Memory must be a WacsMemory instance", nameof(memory));

        wacsMemory.MemoryInstance = Store.Runtime.BindHostMemory((module, name), wacsMemory.MemoryInstance.Type);
        _boundMemory = wacsMemory.MemoryInstance;
    }

    public override void DefineFunction<TResult>(string module, string name, Func<WasmCaller, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, TResult>>(
            (module, name),
            execCtx =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller);
            });
    }

    public override void DefineAction(string module, string name, Action<WasmCaller> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext>>(
            (module, name),
            execCtx =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller);
            });
    }

    public override void DefineFunction<T1, TResult>(string module, string name, Func<WasmCaller, T1, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, TResult>>(
            (module, name),
            (execCtx, a1) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1);
            });
    }

    public override void DefineAction<T1>(string module, string name, Action<WasmCaller, T1> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1>>(
            (module, name),
            (execCtx, a1) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1);
            });
    }

    public override void DefineFunction<T1, T2, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, TResult>>(
            (module, name),
            (execCtx, a1, a2) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2);
            });
    }

    public override void DefineAction<T1, T2>(string module, string name, Action<WasmCaller, T1, T2> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2>>(
            (module, name),
            (execCtx, a1, a2) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2);
            });
    }

    public override void DefineFunction<T1, T2, T3, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, T3, TResult>>(
            (module, name),
            (execCtx, a1, a2, a3) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2, a3);
            });
    }

    public override void DefineAction<T1, T2, T3>(string module, string name, Action<WasmCaller, T1, T2, T3> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2, T3>>(
            (module, name),
            (execCtx, a1, a2, a3) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2, a3);
            });
    }

    public override void DefineFunction<T1, T2, T3, T4, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, T3, T4, TResult>>(
            (module, name),
            (execCtx, a1, a2, a3, a4) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2, a3, a4);
            });
    }

    public override void DefineAction<T1, T2, T3, T4>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2, T3, T4>>(
            (module, name),
            (execCtx, a1, a2, a3, a4) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2, a3, a4);
            });
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, T3, T4, T5, TResult>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2, a3, a4, a5);
            });
    }

    public override void DefineAction<T1, T2, T3, T4, T5>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2, T3, T4, T5>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2, a3, a4, a5);
            });
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, T3, T4, T5, T6, TResult>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2, a3, a4, a5, a6);
            });
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2, T3, T4, T5, T6>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2, a3, a4, a5, a6);
            });
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, T3, T4, T5, T6, T7, TResult>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2, a3, a4, a5, a6, a7);
            });
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2, T3, T4, T5, T6, T7>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2, a3, a4, a5, a6, a7);
            });
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, T3, T4, T5, T6, T7, T8, TResult>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7, a8) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2, a3, a4, a5, a6, a7, a8);
            });
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2, T3, T4, T5, T6, T7, T8>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7, a8) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2, a3, a4, a5, a6, a7, a8);
            });
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7, a8, a9) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2, a3, a4, a5, a6, a7, a8, a9);
            });
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2, T3, T4, T5, T6, T7, T8, T9>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7, a8, a9) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2, a3, a4, a5, a6, a7, a8, a9);
            });
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10);
            });
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10);
            });
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(string module,
        string name, Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Func<ExecContext, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                return func(caller, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11);
            });
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Store.Runtime.BindHostFunction<Action<ExecContext, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>(
            (module, name),
            (execCtx, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) =>
            {
                var caller = new WacsCaller(execCtx, Store.Runtime, _boundMemory);
                action(caller, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11);
            });
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(disposing);
    }
}