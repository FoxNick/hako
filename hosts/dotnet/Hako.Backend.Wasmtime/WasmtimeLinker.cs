using System;
using HakoJS.Backend.Core;
using Wasmtime;

namespace HakoJS.Backend.Wasmtime;

public sealed class WasmtimeLinker : WasmLinker
{
    private bool _disposed;

    internal WasmtimeLinker(Store store)
    {
        UnderlyingStore = store;
        var storeData = (WasmtimeStore?)store.GetData() ?? throw new InvalidOperationException("Store data cannot be null");
        UnderlyingLinker = new Linker(storeData.Engine);
    }

    internal Linker UnderlyingLinker { get; }

    internal Store UnderlyingStore { get; }

    public override void DefineWasi()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineWasi();
    }

    public override void DefineMemory(string module, string name, WasmMemory memory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (memory is not WasmtimeMemory wasmtimeMemory)
            throw new ArgumentException("Memory must be a WasmtimeMemory instance", nameof(memory));

        UnderlyingLinker.Define(module, name, wasmtimeMemory.UnderlyingMemory);
    }

    public override void DefineFunction<TResult>(string module, string name, Func<WasmCaller, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name, caller => func(new WasmtimeCaller(caller)));
    }

    public override void DefineAction(string module, string name, Action<WasmCaller> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name, caller => action(new WasmtimeCaller(caller)));
    }

    public override void DefineFunction<T1, TResult>(string module, string name, Func<WasmCaller, T1, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name, (Caller caller, T1? a) => func(new WasmtimeCaller(caller), a!));
    }

    public override void DefineAction<T1>(string module, string name, Action<WasmCaller, T1> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name, (Caller caller, T1? a) => action(new WasmtimeCaller(caller), a!));
    }

    public override void DefineFunction<T1, T2, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b) => func(new WasmtimeCaller(caller), a!, b!));
    }

    public override void DefineAction<T1, T2>(string module, string name, Action<WasmCaller, T1, T2> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b) => action(new WasmtimeCaller(caller), a!, b!));
    }

    public override void DefineFunction<T1, T2, T3, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c) => func(new WasmtimeCaller(caller), a!, b!, c!));
    }

    public override void DefineAction<T1, T2, T3>(string module, string name, Action<WasmCaller, T1, T2, T3> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c) => action(new WasmtimeCaller(caller), a!, b!, c!));
    }

    public override void DefineFunction<T1, T2, T3, T4, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d) => func(new WasmtimeCaller(caller), a!, b!, c!, d!));
    }

    public override void DefineAction<T1, T2, T3, T4>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d) => action(new WasmtimeCaller(caller), a!, b!, c!, d!));
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e) => func(new WasmtimeCaller(caller), a!, b!, c!, d!, e!));
    }

    public override void DefineAction<T1, T2, T3, T4, T5>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e) => action(new WasmtimeCaller(caller), a!, b!, c!, d!, e!));
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f) => func(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!));
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f) =>
                action(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!));
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g) =>
                func(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!));
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g) =>
                action(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!));
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g, T8? h) =>
                func(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!, h!));
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g, T8? h) =>
                action(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!, h!));
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g, T8? h, T9? i) =>
                func(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!, h!, i!));
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g, T8? h, T9? i) =>
                action(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!, h!, i!));
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g, T8? h, T9? i, T10? j) =>
                func(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!, h!, i!, j!));
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g, T8? h, T9? i, T10? j) =>
                action(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!, h!, i!, j!));
    }

    public override void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(string module,
        string name, Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g, T8? h, T9? i, T10? j, T11? k) =>
                func(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!, h!, i!, j!, k!));
    }

    public override void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        UnderlyingLinker.DefineFunction(module, name,
            (Caller caller, T1? a, T2? b, T3? c, T4? d, T5? e, T6? f, T7? g, T8? h, T9? i, T10? j, T11? k) =>
                action(new WasmtimeCaller(caller), a!, b!, c!, d!, e!, f!, g!, h!, i!, j!, k!));
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) UnderlyingLinker.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}