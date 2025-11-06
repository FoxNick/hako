namespace HakoJS.Backend.Core;

/// <summary>
/// Links host functions and resources to WebAssembly modules.
/// </summary>
public abstract class WasmLinker : IDisposable
{
    /// <summary>
    /// Defines WASI imports in this linker.
    /// </summary>
    public abstract void DefineWasi();

    /// <summary>
    /// Defines a memory export that can be shared with modules.
    /// </summary>
    public abstract void DefineMemory(string module, string name, WasmMemory memory);

    // ===== Function Definitions (0 params) =====

    public abstract void DefineFunction<TResult>(
        string module, string name,
        Func<WasmCaller, TResult> func);

    public abstract void DefineAction(
        string module, string name,
        Action<WasmCaller> action);

    // ===== Function Definitions (1 param) =====

    public abstract void DefineFunction<T1, TResult>(
        string module, string name,
        Func<WasmCaller, T1, TResult> func);

    public abstract void DefineAction<T1>(
        string module, string name,
        Action<WasmCaller, T1> action);

    // ===== Function Definitions (2 params) =====

    public abstract void DefineFunction<T1, T2, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, TResult> func);

    public abstract void DefineAction<T1, T2>(
        string module, string name,
        Action<WasmCaller, T1, T2> action);

    // ===== Function Definitions (3 params) =====

    public abstract void DefineFunction<T1, T2, T3, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, T3, TResult> func);

    public abstract void DefineAction<T1, T2, T3>(
        string module, string name,
        Action<WasmCaller, T1, T2, T3> action);

    // ===== Function Definitions (4 params) =====

    public abstract void DefineFunction<T1, T2, T3, T4, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, TResult> func);

    public abstract void DefineAction<T1, T2, T3, T4>(
        string module, string name,
        Action<WasmCaller, T1, T2, T3, T4> action);

    // ===== Function Definitions (5 params) =====

    public abstract void DefineFunction<T1, T2, T3, T4, T5, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, TResult> func);

    public abstract void DefineAction<T1, T2, T3, T4, T5>(
        string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5> action);

    // ===== Function Definitions (6 params) =====

    public abstract void DefineFunction<T1, T2, T3, T4, T5, T6, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, TResult> func);

    public abstract void DefineAction<T1, T2, T3, T4, T5, T6>(
        string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6> action);

    // ===== Function Definitions (7 params) =====

    public abstract void DefineFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, TResult> func);

    public abstract void DefineAction<T1, T2, T3, T4, T5, T6, T7>(
        string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7> action);

    // ===== Function Definitions (8 params) =====

    public abstract void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, TResult> func);

    public abstract void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8>(
        string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8> action);

    // ===== Function Definitions (9 params) =====

    public abstract void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> func);

    public abstract void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
        string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9> action);

    // ===== Function Definitions (10 params) =====

    public abstract void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> func);

    public abstract void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(
        string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action);

    // ===== Function Definitions (11 params) =====

    public abstract void DefineFunction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(
        string module, string name,
        Func<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> func);

    public abstract void DefineAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(
        string module, string name,
        Action<WasmCaller, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> action);

    protected virtual void Dispose(bool disposing) { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}