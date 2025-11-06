using HakoJS.Host;
using HakoJS.Memory;

namespace HakoJS.Utils;

/// <summary>
/// Contains build and version information for the HakoJS runtime.
/// </summary>
/// <param name="Version">The HakoJS version string.</param>
/// <param name="BuildDate">The date and time when the runtime was built.</param>
/// <param name="QuickJsVersion">The QuickJS engine version.</param>
/// <param name="WasiSdkVersion">The WASI SDK version used for compilation.</param>
/// <param name="WasiLibc">The WASI libc commit hash or version.</param>
/// <param name="Llvm">The LLVM commit hash or version.</param>
/// <param name="Config">The configuration hash or settings.</param>
public record HakoBuildInfo(
    string Version,
    string BuildDate,
    string QuickJsVersion,
    string WasiSdkVersion,
    string WasiLibc,
    string Llvm,
    string Config);

/// <summary>
/// Provides utility methods for interacting with the HakoJS runtime.
/// </summary>
internal class HakoUtils
{
    private readonly MemoryManager _memory;
    private readonly HakoRegistry _registry;
    private HakoBuildInfo? _buildInfo;


    /// <summary>
    /// Initializes a new instance of the <see cref="HakoUtils"/> class.
    /// </summary>
    /// <param name="registry">The HakoJS registry for function calls.</param>
    /// <param name="memory">The memory manager for WASM memory operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when registry or memory is null.</exception>
    internal HakoUtils(HakoRegistry registry, MemoryManager memory)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }


    /// <summary>
    /// Gets build information for the HakoJS runtime.
    /// </summary>
    /// <returns>A <see cref="HakoBuildInfo"/> containing version and build details.</returns>
    /// <remarks>
    /// This method caches the build info after the first call and returns the cached value on subsequent calls.
    /// </remarks>
    public HakoBuildInfo GetBuildInfo()
    {
        if (_buildInfo != null) return _buildInfo;
        var buildPtr = _registry.BuildInfo();

        const int ptrSize = 4; // 32-bit pointers in WASM32
        
        var versionPtr = _memory.ReadPointer(buildPtr);
        var version = _memory.ReadNullTerminatedString(versionPtr);

        // Skip flags at offset ptrSize (index 1)

        var buildDatePtr = _memory.ReadPointer(buildPtr + ptrSize * 2);
        var buildDate = _memory.ReadNullTerminatedString(buildDatePtr);

        var quickJsVersionPtr = _memory.ReadPointer(buildPtr + ptrSize * 3);
        var quickJsVersion = _memory.ReadNullTerminatedString(quickJsVersionPtr);

        var wasiSdkVersionPtr = _memory.ReadPointer(buildPtr + ptrSize * 4);
        var wasiSdkVersion = _memory.ReadNullTerminatedString(wasiSdkVersionPtr);

        var wasiLibcPtr = _memory.ReadPointer(buildPtr + ptrSize * 5);
        var wasiLibc = _memory.ReadNullTerminatedString(wasiLibcPtr);

        var llvmPtr = _memory.ReadPointer(buildPtr + ptrSize * 6);
        var llvm = _memory.ReadNullTerminatedString(llvmPtr);

        var configPtr = _memory.ReadPointer(buildPtr + ptrSize * 7);
        var config = _memory.ReadNullTerminatedString(configPtr);

        _buildInfo = new HakoBuildInfo(
            version,
            buildDate,
            quickJsVersion,
            wasiSdkVersion,
            wasiLibc,
            llvm,
            config);

        return _buildInfo;
    }


    /// <summary>
    /// Gets the length property of a JavaScript object (typically an array or string).
    /// </summary>
    /// <param name="ctx">The JavaScript context handle.</param>
    /// <param name="ptr">Pointer to the JavaScript value.</param>
    /// <returns>The length as an integer, or -1 if the operation failed.</returns>
    public int GetLength(int ctx, int ptr)
    {
        int lenPtrPtr = _memory.AllocateMemory(ctx, sizeof(int));
        try
        {
            var result = _registry.GetLength(ctx, lenPtrPtr, ptr);
            if (result != 0) return -1;

            return (int)_memory.ReadUint32(lenPtrPtr);
        }
        finally
        {
            _memory.FreeMemory(ctx, lenPtrPtr);
        }
    }


    /// <summary>
    /// Checks if two JavaScript values are equal using the specified equality operation.
    /// </summary>
    /// <param name="ctx">The JavaScript context handle.</param>
    /// <param name="aPtr">Pointer to the first JavaScript value.</param>
    /// <param name="bPtr">Pointer to the second JavaScript value.</param>
    /// <param name="op">The equality operation to use (default is strict equality).</param>
    /// <returns><c>true</c> if the values are equal according to the specified operation; otherwise, <c>false</c>.</returns>
    public bool IsEqual(int ctx, int aPtr, int bPtr, EqualityOp op = EqualityOp.Strict)
    {
        return _registry.IsEqual(ctx, aPtr, bPtr, (int)op) == 1;
    }


    /// <summary>
    /// Checks if the HakoJS runtime was built in debug mode.
    /// </summary>
    /// <returns><c>true</c> if this is a debug build; otherwise, <c>false</c>.</returns>
    public bool IsDebugBuild()
    {
        return _registry.BuildIsDebug() == 1;
    }
}

/// <summary>
/// Specifies the type of equality comparison to perform on JavaScript values.
/// </summary>
public enum EqualityOp
{
    /// <summary>
    /// Strict equality (===). Values must be of the same type and value.
    /// </summary>
    Strict = 0,

    /// <summary>
    /// SameValue comparison as defined in the ECMAScript specification.
    /// Similar to strict equality but treats NaN as equal to NaN and -0 as different from +0.
    /// </summary>
    SameValue = 1,

    /// <summary>
    /// SameValueZero comparison as defined in the ECMAScript specification.
    /// Similar to SameValue but treats -0 and +0 as equal.
    /// </summary>
    SameValueZero = 2
}