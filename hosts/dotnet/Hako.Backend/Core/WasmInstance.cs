namespace HakoJS.Backend.Core;

/// <summary>
/// An instantiated WebAssembly module with callable exports.
/// </summary>
public abstract class WasmInstance : IDisposable
{
    /// <summary>
    /// Gets an exported memory by name.
    /// </summary>
    public abstract WasmMemory? GetMemory(string name);

    // ===== Int32 Functions (0 params) =====
    public abstract Func<int>? GetFunctionInt32(string name);
    
    // ===== Int32 Functions (1 param) =====
    public abstract Func<int, int>? GetFunctionInt32<T1>(string name);
    
    // ===== Int32 Functions (2 params) =====
    public abstract Func<int, int, int>? GetFunctionInt32<T1, T2>(string name);
    
    // ===== Int32 Functions (3 params) =====
    public abstract Func<int, int, int, int>? GetFunctionInt32<T1, T2, T3>(string name);
    
    // ===== Int32 Functions (4 params) =====
    public abstract Func<int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4>(string name);
    
    // ===== Int32 Functions (5 params) =====
    public abstract Func<int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5>(string name);
    
    // ===== Int32 Functions (6 params) =====
    public abstract Func<int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6>(string name);
    
    // ===== Int32 Functions (7 params) =====
    public abstract Func<int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7>(string name);
    
    // ===== Int32 Functions (8 params) =====
    public abstract Func<int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7, T8>(string name);
    
    // ===== Int32 Functions (9 params) =====
    public abstract Func<int, int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string name);
    
    // ===== Int32 Functions (10 params) =====
    public abstract Func<int, int, int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string name);
    
    // ===== Int32 Functions (11 params) =====
    public abstract Func<int, int, int, int, int, int, int, int, int, int, int, int>? GetFunctionInt32<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string name);

    // ===== Int64 Functions (0 params) =====
    public abstract Func<long>? GetFunctionInt64(string name);
    
    // ===== Int64 Functions (1 param) =====
    public abstract Func<int, long>? GetFunctionInt64<T1>(string name);
    
    // ===== Int64 Functions (2 params) =====
    public abstract Func<int, int, long>? GetFunctionInt64<T1, T2>(string name);

    // ===== Double Functions (0 params) =====
    public abstract Func<double>? GetFunctionDouble(string name);
    
    // ===== Double Functions (1 param) =====
    public abstract Func<int, double>? GetFunctionDouble<T1>(string name);
    
    // ===== Double Functions (2 params) =====
    public abstract Func<int, int, double>? GetFunctionDouble<T1, T2>(string name);

    // ===== Mixed Functions (int, double -> int) =====
    public abstract Func<int, double, int>? GetFunctionInt32WithDouble<T1, T2>(string name);

    // ===== Actions (0 params) =====
    public abstract Action? GetAction(string name);

    // ===== Actions (1 param) =====
    public abstract Action<int>? GetAction<T1>(string name);

    // ===== Actions (2 params) =====
    public abstract Action<int, int>? GetAction<T1, T2>(string name);
    
    // ===== Actions with long (2 params) =====
    public abstract Action<int, long>? GetActionWithLong<T1, T2>(string name);

    // ===== Actions (3 params) =====
    public abstract Action<int, int, int>? GetAction<T1, T2, T3>(string name);

    // ===== Actions (4 params) =====
    public abstract Action<int, int, int, int>? GetAction<T1, T2, T3, T4>(string name);

    // ===== Actions (5 params) =====
    public abstract Action<int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5>(string name);

    // ===== Actions (6 params) =====
    public abstract Action<int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6>(string name);

    // ===== Actions (7 params) =====
    public abstract Action<int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7>(string name);

    // ===== Actions (8 params) =====
    public abstract Action<int, int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7, T8>(string name);

    // ===== Actions (9 params) =====
    public abstract Action<int, int, int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string name);

    // ===== Actions (10 params) =====
    public abstract Action<int, int, int, int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string name);

    // ===== Actions (11 params) =====
    public abstract Action<int, int, int, int, int, int, int, int, int, int, int>? GetAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string name);

    protected virtual void Dispose(bool disposing) { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}