using System.Collections.Concurrent;
using HakoJS.Backend.Core;
using HakoJS.Exceptions;
using HakoJS.Memory;
using HakoJS.VM;

namespace HakoJS.Host;

internal enum ModuleSourceType
{
    String = 0,
    Precompiled = 1,
    Error = 2
}

public abstract class ModuleLoaderResult
{
    public bool IsError => this is ErrorResult;

    public static ModuleLoaderResult Source(string sourceCode)
    {
        return new SourceResult(sourceCode);
    }

    public static ModuleLoaderResult Precompiled(int moduleDefPtr)
    {
        return new PrecompiledResult(moduleDefPtr);
    }

    public static ModuleLoaderResult Error()
    {
        return new ErrorResult();
    }

    public bool TryGetSource(out string sourceCode)
    {
        if (this is SourceResult src)
        {
            sourceCode = src.SourceCode;
            return true;
        }

        sourceCode = string.Empty;
        return false;
    }

    public bool TryGetPrecompiled(out int moduleDefPtr)
    {
        if (this is PrecompiledResult pre)
        {
            moduleDefPtr = pre.ModuleDefPtr;
            return true;
        }

        moduleDefPtr = 0;
        return false;
    }

    private sealed class SourceResult(string sourceCode) : ModuleLoaderResult
    {
        public string SourceCode { get; } = sourceCode;
    }

    private sealed class PrecompiledResult(int moduleDefPtr) : ModuleLoaderResult
    {
        public int ModuleDefPtr { get; } = moduleDefPtr;
    }

    private sealed class ErrorResult : ModuleLoaderResult
    {
    }
}

public delegate JSValue? JSFunction(Realm realm, JSValue thisArg, JSValue[] args);
public delegate JSValue? JSConstructor(Realm realm, JSValue instance, JSValue[] args, JSValue newTarget);

public delegate void JSAction(Realm realm, JSValue thisArg, JSValue[] args);
public delegate Task<JSValue?> JSAsyncFunction(Realm realm, JSValue thisArg, JSValue[] args);
public delegate Task JSAsyncAction(Realm realm, JSValue thisArg, JSValue[] args);

public delegate ModuleLoaderResult? ModuleLoaderFunction(HakoRuntime runtime, Realm realm, string moduleName, Dictionary<string, string> attributes);
public delegate string ModuleNormalizerFunction(string baseName, string moduleName);
public delegate bool InterruptHandler(HakoRuntime runtime, Realm realm, int opaque);
public delegate int ModuleInitFunction(CModuleInitializer module);

public delegate JSValue ClassConstructorHandler(Realm realm, JSValue newTarget, JSValue[] args, int classId);
public delegate void ClassFinalizerHandler(HakoRuntime runtime, int opaque, int classId);
public delegate void ClassGcMarkHandler(HakoRuntime runtime, int opaque, int classId, int markFunc);

public delegate void PromiseRejectionTrackerFunction(Realm realm, JSValue promise, JSValue reason, bool isHandled, int opaque);

internal record HostFunction(string Name, JSFunction Callback);

internal sealed class CallbackManager
{
    private readonly ConcurrentDictionary<int, ClassConstructorHandler> _classConstructors = new();
    private readonly ConcurrentDictionary<int, ClassFinalizerHandler> _classFinalizers = new();
    private readonly ConcurrentDictionary<int, ClassGcMarkHandler> _classGcMarks = new();
    private readonly ConcurrentDictionary<int, Realm> _contextRegistry = new();
    private readonly ConcurrentDictionary<int, HakoRuntime> _runtimeRegistry = new();
    
    private readonly ConcurrentDictionary<int, (HostFunction Function, WeakReference<Realm> RealmRef)> _hostFunctions = new();
    
    private readonly ConcurrentDictionary<int, ConcurrentBag<int>> _functionIdsByRealm = new();
    
    private readonly ConcurrentDictionary<string, ModuleInitFunction> _moduleInitHandlers = new();

    private int _nextFunctionId = -32768;
    
    private InterruptHandler? _interruptHandler;
    private bool _isInitialized;
    private MemoryManager? _memory;
    private ModuleLoaderFunction? _moduleLoader;
    private ModuleNormalizerFunction? _moduleNormalizer;
    private PromiseRejectionTrackerFunction? _promiseRejectionTracker;
    private HakoRegistry? _registry;

    internal void Initialize(HakoRegistry registry, MemoryManager memory)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _isInitialized = true;
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized || _registry == null || _memory == null)
            throw new InvalidOperationException("CallbackManager not initialized. Ensure Container has been created.");
    }

    internal void BindToLinker(WasmLinker linker)
    {
        if (linker == null)
            throw new ArgumentNullException(nameof(linker));

        linker.DefineFunction<int, int, int, int, int, int>(
            "hako", "call_function",
            HandleHostFunctionCall);

        linker.DefineFunction<int, int, int, int>(
            "hako", "interrupt_handler",
            HandleInterrupt);

        linker.DefineFunction<int, int, int, int, int, int>(
            "hako", "load_module",
            HandleModuleLoad);

        linker.DefineFunction<int, int, int, int, int, int>(
            "hako", "normalize_module",
            HandleModuleNormalize);

        linker.DefineFunction<int, int, int>(
            "hako", "module_init",
            HandleModuleInit);

        linker.DefineFunction<int, int, int, int, int, int>(
            "hako", "class_constructor",
            HandleClassConstructor);

        linker.DefineAction<int, int, int>(
            "hako", "class_finalizer",
            HandleClassFinalizer);

        linker.DefineAction<int, int, int, int>(
            "hako", "class_gc_mark",
            HandleClassGcMark);

        linker.DefineAction<int, int, int, int, int>(
            "hako", "promise_rejection_tracker",
            HandlePromiseRejectionTracker);
    }

    #region Context/Runtime Registry

    public void RegisterContext(int ctxPtr, Realm realm)
    {
        _contextRegistry[ctxPtr] = realm;
    }

    public void UnregisterContext(int ctxPtr)
    {
        _contextRegistry.TryRemove(ctxPtr, out _);
        CleanupFunctionsForRealm(ctxPtr);
    }

    private Realm? GetContext(int ctxPtr)
    {
        _contextRegistry.TryGetValue(ctxPtr, out var realm);
        return realm;
    }

    public void RegisterRuntime(int rtPtr, HakoRuntime runtime)
    {
        _runtimeRegistry[rtPtr] = runtime;
    }

    public void UnregisterRuntime(int rtPtr)
    {
        _runtimeRegistry.TryRemove(rtPtr, out _);
    }

    private HakoRuntime? GetRuntime(int rtPtr)
    {
        _runtimeRegistry.TryGetValue(rtPtr, out var runtime);
        return runtime;
    }

    #endregion

    #region Function Management

    private int RegisterHostFunction(Realm realm, string name, JSFunction callback)
    {
        var id = Interlocked.Increment(ref _nextFunctionId);
        
        var weakRef = new WeakReference<Realm>(realm);
        if (!_hostFunctions.TryAdd(id, (new HostFunction(name, callback), weakRef)))
        {
            throw new HakoException("Could not register host function: " + name);
        }
        
        var functionIds = _functionIdsByRealm.GetOrAdd(realm.Pointer, _ => []);
        functionIds.Add(id);
        
        return id;
    }

    public void UnregisterHostFunction(int id)
    {
        _hostFunctions.TryRemove(id, out _);
    }

    private void CleanupFunctionsForRealm(int realmPtr)
    {
        if (_functionIdsByRealm.TryRemove(realmPtr, out var functionIds))
        {
            foreach (var id in functionIds)
            {
                _hostFunctions.TryRemove(id, out _);
            }
        }
    }

    public int NewFunction(int ctx, JSFunction callback, string name)
    {
        EnsureInitialized();

        var realm = GetContext(ctx);
        if (realm == null)
            throw new InvalidOperationException($"Context not found for ctxPtr: {ctx}");

        var id = RegisterHostFunction(realm, name, callback);

        int namePtr = _memory!.AllocateString(ctx, name, out _);
        try
        {
            return _registry!.NewFunction(ctx, id, namePtr);
        }
        finally
        {
            _memory.FreeMemory(ctx, namePtr);
        }
    }

    #endregion

    #region Module Management

    public void SetModuleLoader(ModuleLoaderFunction? loader)
    {
        _moduleLoader = loader;
    }

    public void SetModuleNormalizer(ModuleNormalizerFunction? normalizer)
    {
        _moduleNormalizer = normalizer;
    }

    public void RegisterModuleInitHandler(string moduleName, ModuleInitFunction handler)
    {
        _moduleInitHandlers[moduleName] = handler;
    }

    public void UnregisterModuleInitHandler(string moduleName)
    {
        _moduleInitHandlers.TryRemove(moduleName, out _);
    }

    #endregion

    #region Class Management

    public void RegisterClassConstructor(int classId, ClassConstructorHandler handler)
    {
        _classConstructors[classId] = handler;
    }

    public void UnregisterClassConstructor(int classId)
    {
        _classConstructors.TryRemove(classId, out _);
    }

    public void RegisterClassFinalizer(int classId, ClassFinalizerHandler handler)
    {
        _classFinalizers[classId] = handler;
    }

    public void UnregisterClassFinalizer(int classId)
    {
        _classFinalizers.TryRemove(classId, out _);
    }

    public void RegisterClassGcMark(int classId, ClassGcMarkHandler handler)
    {
        _classGcMarks[classId] = handler;
    }

    public void UnregisterClassGcMark(int classId)
    {
        _classGcMarks.TryRemove(classId, out _);
    }

    #endregion

    #region Interrupt & Promise Tracking

    public void SetPromiseRejectionTracker(PromiseRejectionTrackerFunction? handler)
    {
        _promiseRejectionTracker = handler;
    }

    public void SetInterruptHandler(InterruptHandler? handler)
    {
        _interruptHandler = handler;
    }

    #endregion

    #region Callback Handlers

    private int HandleHostFunctionCall(WasmCaller caller, int ctxPtr, int thisPtr,
        int argc, int argvPtr, int funcId)
    {
        EnsureInitialized();

        if (!_hostFunctions.TryGetValue(funcId, out var entry))
            throw new HakoException($"Host function not found for funcId: {funcId}");

        if (!entry.RealmRef.TryGetTarget(out _))
            throw new HakoException($"Realm has been disposed for funcId: {funcId}");

        var ctx = GetContext(ctxPtr);
        if (ctx == null)
            throw new HakoException($"Context not found for ctxPtr: {ctxPtr}");

        using var thisHandle = ctx.BorrowValue(thisPtr);

        var argHandles = new JSValue[argc];
        for (var i = 0; i < argc; i++)
        {
            var argPtr = _registry!.ArgvGetJSValueConstPointer(argvPtr, i);
            argHandles[i] = ctx.DupValue(argPtr);
        }

        try
        {
            var result = entry.Function.Callback(ctx, thisHandle, argHandles) ?? ctx.Undefined();
            return result.GetHandle();
        }
        catch (Exception error)
        {
            using var errorHandle = ctx.NewError(error);
            var errorPtr = errorHandle.GetHandle();
            return _registry!.Throw(ctxPtr, errorPtr);
        }
        finally
        {
            foreach (var arg in argHandles) arg.Dispose();
        }
    }

    private int HandleInterrupt(WasmCaller caller, int rtPtr, int ctxPtr, int opaque)
    {
        if (_interruptHandler == null) return 0;
        
        var runtime = GetRuntime(rtPtr);
        if (runtime == null) return 0;
        var realm = GetContext(ctxPtr);
        if (realm == null) return 0;

        return _interruptHandler(runtime, realm, opaque) ? 1 : 0;
    }

    private int HandleModuleLoad(WasmCaller caller, int rtPtr, int ctxPtr, int moduleNamePtr, int opaque,
        int attributesPtr)
    {
        EnsureInitialized();
        var runtime = GetRuntime(rtPtr);
        if (runtime == null) return CreateModuleSourceError(ctxPtr);

        if (_moduleLoader == null)
            return CreateModuleSourceError(ctxPtr);

        var ctx = GetContext(ctxPtr);
        if (ctx == null)
            return CreateModuleSourceError(ctxPtr);

        var moduleName = _memory!.ReadNullTerminatedString(moduleNamePtr);

        Dictionary<string, string> attributes = new();
        if (attributesPtr != 0)
        {
            using var att = ctx.BorrowValue(attributesPtr);
            
            if (att.IsObject())
            {
                var propertyNames = att.GetOwnPropertyNames();
                foreach (var propName in propertyNames)
                {
                    var propValue = att.GetProperty(propName);
                    var key = propName.Consume(v => v.AsString());
                    attributes[key] = propValue.Consume((v) => v.AsString());
                }
            }
        }

        var moduleResult = _moduleLoader(ctx.Runtime, ctx, moduleName, attributes);
        if (moduleResult == null || moduleResult.IsError)
            return CreateModuleSourceError(ctxPtr);

        if (moduleResult.TryGetSource(out var sourceCode))
            return CreateModuleSourceString(ctxPtr, sourceCode);

        if (moduleResult.TryGetPrecompiled(out var moduleDefPtr))
            return CreateModuleSourcePrecompiled(ctxPtr, moduleDefPtr);

        return CreateModuleSourceError(ctxPtr);
    }

    private int HandleModuleNormalize(WasmCaller caller, int rtPtr, int ctxPtr, int baseNamePtr,
        int moduleNamePtr, int opaque)
    {
        EnsureInitialized();

        if (_moduleNormalizer == null)
            return moduleNamePtr;

        var baseName = _memory!.ReadNullTerminatedString(baseNamePtr);
        var moduleName = _memory.ReadNullTerminatedString(moduleNamePtr);

        var normalizedName = _moduleNormalizer(baseName, moduleName);
        var normalizedPtr = _memory.AllocateRuntimeString(rtPtr, normalizedName, out _);
        return normalizedPtr.Value;
    }

    private int HandleModuleInit(WasmCaller caller, int ctxPtr, int modulePtr)
    {
        EnsureInitialized();

        var ctx = GetContext(ctxPtr);
        if (ctx == null)
            throw new InvalidOperationException($"Context not found for ctxPtr: {ctxPtr}");

        var moduleName = GetModuleName(ctx, modulePtr);
        if (moduleName == null)
            throw new InvalidOperationException("Unable to get module name");

        if (!_moduleInitHandlers.TryGetValue(moduleName, out var handler))
            return -1;

        var initializer = new CModuleInitializer(ctx, modulePtr);
        return handler(initializer);
    }

    private int HandleClassConstructor(WasmCaller caller, int ctxPtr, int newTargetPtr, int argc, int argvPtr,
        int classId)
    {
        EnsureInitialized();

        if (!_classConstructors.TryGetValue(classId, out var handler))
            return _registry!.GetUndefined();

        var ctx = GetContext(ctxPtr);
        if (ctx == null)
            throw new InvalidOperationException($"Context not found for ctxPtr: {ctxPtr}");

        using var newTarget = ctx.BorrowValue(newTargetPtr);

        var args = new JSValue[argc];

        for (var i = 0; i < argc; i++)
        {
            var argPtr = _registry!.ArgvGetJSValueConstPointer(argvPtr, i);
            args[i] = ctx.DupValue(argPtr);
        }

        try
        {
            var result = handler(ctx, newTarget, args, classId);
            var handle = result.GetHandle();
            return handle;
        }
        catch (Exception error)
        {
            using var errorHandle = ctx.NewError(error);
            var errorPtr = errorHandle.GetHandle();
            return _registry!.Throw(ctxPtr, errorPtr);
        }
        finally
        {
            foreach (var arg in args) arg.Dispose();
        }
    }

    private void HandleClassFinalizer(WasmCaller caller, int rtPtr, int opaque, int classId)
    {
        if (!_classFinalizers.TryGetValue(classId, out var handler))
            return;

        var runtime = GetRuntime(rtPtr);
        if (runtime == null)
            return;

        handler(runtime, opaque, classId);
    }

    private void HandleClassGcMark(WasmCaller caller, int rtPtr, int opaque, int classId, int markFunc)
    {
        if (!_classGcMarks.TryGetValue(classId, out var handler))
            return;

        var runtime = GetRuntime(rtPtr);
        if (runtime == null)
            return;

        handler(runtime, opaque, classId, markFunc);
    }

    private void HandlePromiseRejectionTracker(WasmCaller caller, int ctxPtr, int promisePtr,
        int reasonPtr, int isHandled, int opaque)
    {
        if (_promiseRejectionTracker == null)
            return;

        var ctx = GetContext(ctxPtr);
        if (ctx == null)
            return;

        using var promise = ctx.BorrowValue(promisePtr);
        using var reason = ctx.BorrowValue(reasonPtr);

        _promiseRejectionTracker(ctx, promise, reason, isHandled != 0, opaque);
    }

    #endregion

    #region Helper Methods

    private string? GetModuleName(Realm ctx, int modulePtr)
    {
        EnsureInitialized();

        var namePtr = _registry!.GetModuleName(ctx.Pointer, modulePtr);
        if (namePtr == 0)
            return null;

        try
        {
            return _memory!.ReadNullTerminatedString(namePtr);
        }
        finally
        {
            _memory?.FreeCString(ctx.Pointer, namePtr);
        }
    }

    private int CreateModuleSourceString(int ctxPtr, string sourceCode)
    {
        EnsureInitialized();

        const int structSize = 8;
        var structPtr = _registry!.Malloc(ctxPtr, structSize);
        if (structPtr == 0)
            return 0;

        int sourcePtr = _memory!.AllocateString(ctxPtr, sourceCode, out _);
        if (sourcePtr == 0)
        {
            _registry.Free(ctxPtr, structPtr);
            return 0;
        }

        _memory.WriteUint32(structPtr, (uint)ModuleSourceType.String);
        _memory.WriteUint32(structPtr + 4, (uint)sourcePtr);

        return structPtr;
    }

    private int CreateModuleSourcePrecompiled(int ctxPtr, int moduleDefPtr)
    {
        EnsureInitialized();

        const int structSize = 8;
        var structPtr = _registry!.Malloc(ctxPtr, structSize);
        if (structPtr == 0)
            return 0;

        _memory!.WriteUint32(structPtr, (uint)ModuleSourceType.Precompiled);
        _memory.WriteUint32(structPtr + 4, (uint)moduleDefPtr);

        return structPtr;
    }

    private int CreateModuleSourceError(int ctxPtr)
    {
        EnsureInitialized();

        const int structSize = 8;
        var structPtr = _registry!.Malloc(ctxPtr, structSize);
        if (structPtr == 0)
            return 0;

        _memory!.WriteUint32(structPtr, (uint)ModuleSourceType.Error);
        _memory.WriteUint32(structPtr + 4, 0);

        return structPtr;
    }

    #endregion
}