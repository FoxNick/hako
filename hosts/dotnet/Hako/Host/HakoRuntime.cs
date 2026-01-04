using System.Collections.Concurrent;
using System.Text.Json;
using HakoJS.Backend.Configuration;
using HakoJS.Backend.Core;
using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.Lifetime;
using HakoJS.Memory;
using HakoJS.Utils;
using HakoJS.VM;

namespace HakoJS.Host;

/// <summary>
/// Represents a QuickJS runtime instance that manages JavaScript execution contexts and resources.
/// </summary>
/// <remarks>
/// <para>
/// The runtime is the top-level container for JavaScript execution. It manages multiple realms (execution contexts),
/// handles memory limits, configures garbage collection, and controls module loading. Each runtime runs QuickJS
/// via WebAssembly using the Wacs engine.
/// </para>
/// <para>
/// Most users should create runtimes using <see cref="Hako.Initialize"/> rather than calling methods directly:
/// <code>
/// using var runtime = Hako.Initialize(opts =>
/// {
///     opts.MemoryLimitBytes = 50 * 1024 * 1024; // 50MB
/// });
/// 
/// using var realm = runtime.CreateRealm();
/// var result = await realm.EvalAsync&lt;int&gt;("2 + 2");
/// </code>
/// </para>
/// <para>
/// The runtime must be disposed to release native resources. All realms created by the runtime
/// become invalid after disposal.
/// </para>
/// </remarks>
public sealed class HakoRuntime : IDisposable
{
    private readonly ConcurrentDictionary<int, ConcurrentBag<CModule>> _modulesByRealm = new();
    private readonly ConcurrentDictionary<int, Realm> _realmMap = new();
    private InterruptHandler? _currentInterruptHandler;
    private PromiseRejectionTrackerFunction? _currentPromiseRejectionTracker;
    private bool _disposed;
    private Realm? _systemRealm;
    internal readonly ConcurrentDictionary<(int RealmPtr, string TypeKey), JSClass> JSClassRegistry = new();
    private readonly WasmEngine _engine;
    private readonly WasmStore _store;
    private readonly WasmInstance _instance;
    
    
    private HakoRuntime(
        HakoRegistry registry, 
        WasmMemory memory, 
        CallbackManager callbacks, 
        int rtPtr,
        WasmEngine engine,
        WasmStore store,
        WasmInstance instance)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        Memory = new MemoryManager(registry, memory);
        Errors = new ErrorManager(registry, Memory);
        Utils = new HakoUtils(registry, Memory);

        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));

        Callbacks.Initialize(registry, Memory);
        Callbacks.RegisterRuntime(rtPtr, this);
        Pointer = rtPtr;
    }

    /// <summary>
    /// Gets the QuickJS runtime pointer.
    /// </summary>
    /// <value>An integer representing the native QuickJS runtime pointer.</value>
    public int Pointer { get; }

    /// <summary>
    /// Gets build information about the QuickJS version and enabled features.
    /// </summary>
    /// <value>A <see cref="HakoBuildInfo"/> containing version and feature flags.</value>
    /// <remarks>
    /// Use this to check if features like BigInt are available in the current build.
    /// </remarks>
    public HakoBuildInfo Build => Utils.GetBuildInfo();

    internal HakoRegistry Registry { get; }
    internal CallbackManager Callbacks { get; }
    internal MemoryManager Memory { get; }
    internal ErrorManager Errors { get; }
    internal HakoUtils Utils { get; }

    #region Disposal
    
    internal bool IsDisposed => _disposed;

    /// <summary>
    /// Disposes the runtime and all associated resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This releases all realms, modules, classes, and native QuickJS resources. All JavaScript values
    /// and contexts become invalid after disposal.
    /// </para>
    /// <para>
    /// Interrupt handlers and promise rejection trackers are automatically disabled during disposal.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        if (_currentInterruptHandler != null) DisableInterruptHandler();

        foreach (var realmPtr in _modulesByRealm.Keys.ToList()) DisposeModulesForRealm(realmPtr);
        _modulesByRealm.Clear();

        // Dispose all JSClasses
        foreach (var kv in JSClassRegistry)
        {
            kv.Value.Dispose();
        }
        JSClassRegistry.Clear();

        foreach (var realm in _realmMap.Values.ToList()) realm.Dispose();

        _systemRealm?.Dispose();
        _realmMap.Clear();
        
        CleanupTypeStripper();

        Callbacks.UnregisterRuntime(Pointer);
        Registry.FreeRuntime(Pointer);

        _disposed = true;
    }

    #endregion

    /// <summary>
    /// Creates a new <see cref="HakoRuntime"/> instance with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the runtime.</param>
    /// <returns>A new <see cref="HakoRuntime"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="HakoException">Failed to create the runtime or load the WebAssembly module.</exception>
    /// <remarks>
    /// <para>
    /// This method initializes the WebAssembly runtime, loads QuickJS, and sets up the execution environment.
    /// Most users should use <see cref="Hako.Initialize"/> instead of calling this directly.
    /// </para>
    /// </remarks>
    internal static HakoRuntime Create<TEngine>(HakoOptions<TEngine> options) where TEngine : WasmEngine, IWasmEngineFactory<TEngine>
    {
        
 
        ArgumentNullException.ThrowIfNull(options);
        

        var engine = TEngine.Create(options.EngineOptions ?? new WasmEngineOptions());
        var storeOptions = options.StoreOptions ?? new WasmStoreOptions();
        var store = engine.CreateStore(storeOptions);
        
        var linker = store.CreateLinker();
        
        
        // Create and define memory
        var memory = store.CreateMemory(new MemoryConfiguration
        {
            InitialPages = storeOptions.InitialMemoryPages,
            MaximumPages = storeOptions.MaximumMemoryPages,
        });
        linker.DefineMemory("env", "memory", memory);
        
        var callbacks = new CallbackManager();
        callbacks.BindToLinker(linker);
        linker.DefineWasi();
        
        WasmModule module;
        if (!string.IsNullOrWhiteSpace(options.WasmPath))
        {
            module = engine.LoadModule(options.WasmPath, "hako");
        }
        else
        {
            using var stream = new MemoryStream(HakoResources.Reactor.ToArray());
            module = engine.LoadModule(stream, "hako");
        }
        
        var instance = module.Instantiate(linker);
        
        var registry = new HakoRegistry(instance);
        var rtPtr = registry.NewRuntime();
        if (rtPtr == 0)
            throw new HakoException("Failed to create runtime");

        var hakoRuntime = new HakoRuntime(registry, memory, callbacks, rtPtr, engine, store, instance);

        if (options.MemoryLimitBytes != -1)
            hakoRuntime.SetMemoryLimit(options.MemoryLimitBytes);

        if (options.StripOptions != null)
            hakoRuntime.SetStripInfo(options.StripOptions);


        hakoRuntime.Registry.InitTypeStripper(hakoRuntime.Pointer);

        return hakoRuntime;

        return hakoRuntime;
    }

    /// <summary>
    /// Registers a C module with the runtime for a specific realm.
    /// </summary>
    /// <param name="module">The module to register.</param>
    /// <exception cref="ObjectDisposedException">The runtime has been disposed.</exception>
    /// <remarks>
    /// Registered modules are automatically disposed when their realm is disposed.
    /// </remarks>
    internal void RegisterModule(CModule module)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var realmPtr = module.Context.Pointer;
        var modules = _modulesByRealm.GetOrAdd(realmPtr, _ => []);
        modules.Add(module);
    }

    #region C Module Support

    /// <summary>
    /// Creates a new C module (native module implemented in .NET).
    /// </summary>
    /// <param name="name">The module name used in JavaScript import statements.</param>
    /// <param name="handler">An action that initializes the module's exports.</param>
    /// <param name="realm">The realm for the module, or <c>null</c> to use the system realm.</param>
    /// <returns>A <see cref="CModule"/> representing the native module.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="handler"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// C modules allow you to expose .NET functionality to JavaScript. The handler is called when the
    /// module is first imported, allowing you to define exports.
    /// </para>
    /// <para>
    /// Most users should use <see cref="HakoRuntimeExtensions.ConfigureModules"/> for a more convenient API:
    /// <code>
    /// runtime.ConfigureModules()
    ///     .WithModule("myModule", init =>
    ///     {
    ///         init.SetExport("hello", realm.NewString("World"));
    ///     })
    ///     .Apply();
    /// </code>
    /// </para>
    /// </remarks>
    public CModule CreateCModule(string name, Action<CModuleInitializer> handler, Realm? realm = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(handler);

        var targetRealm = realm ?? GetSystemRealm();
        return new CModule(targetRealm, name, handler);
    }

    #endregion

    #region Realm Management

    /// <summary>
    /// Creates a new JavaScript execution context (realm).
    /// </summary>
    /// <param name="options">Optional configuration for the realm.</param>
    /// <returns>A new <see cref="Realm"/> instance.</returns>
    /// <exception cref="HakoException">Failed to create the realm.</exception>
    /// <remarks>
    /// <para>
    /// Each realm has its own global object and set of built-in objects. Multiple realms can exist
    /// within a runtime, allowing for sandboxed script execution.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var realm = runtime.CreateRealm(new RealmOptions
    /// {
    ///     Intrinsics = RealmOptions.RealmIntrinsics.Standard,
    /// });
    /// 
    /// var result = await realm.EvalAsync&lt;int&gt;("2 + 2");
    /// </code>
    /// </para>
    /// </remarks>
    public Realm CreateRealm(RealmOptions? options = null)
    {
        options ??= new RealmOptions();

        if (options.RealmPointer.HasValue)
        {
            if (_realmMap.TryGetValue(options.RealmPointer.Value, out var existingRealm)) return existingRealm;
            return new Realm(this, options.RealmPointer.Value);
        }

        var intrinsics = (int)options.Intrinsics;
        var ctxPtr = Registry.NewContext(Pointer, intrinsics);

        if (ctxPtr == 0) throw new HakoException("Failed to create context");

        var realm = new Realm(this, ctxPtr);

        _realmMap.TryAdd(ctxPtr, realm);
        return realm;
    }

    /// <summary>
    /// Gets or creates the system realm, a default realm used for internal operations.
    /// </summary>
    /// <returns>The system <see cref="Realm"/> instance.</returns>
    /// <remarks>
    /// The system realm is lazily created on first access and reused for subsequent calls.
    /// It's used as the default realm when one is not explicitly specified.
    /// </remarks>
    public Realm GetSystemRealm()
    {
        _systemRealm ??= CreateRealm();
        return _systemRealm;
    }

    /// <summary>
    /// Gets all realms currently tracked by this runtime.
    /// </summary>
    /// <returns>An enumerable of all tracked <see cref="Realm"/> instances.</returns>
    internal IEnumerable<Realm> GetTrackedRealms()
    {
        return _realmMap.Values.ToList();
    }

    /// <summary>
    /// Disposes all modules associated with a specific realm.
    /// </summary>
    /// <param name="realmPtr">The realm pointer.</param>
    /// <exception cref="HakoException">Failed to dispose one or more modules.</exception>
    internal void DisposeModulesForRealm(int realmPtr)
    {
        if (_modulesByRealm.TryRemove(realmPtr, out var modules))
            foreach (var module in modules)
                try
                {
                    module.Dispose();
                }
                catch (Exception ex)
                {
                    throw new HakoException($"Error disposing module {module.Name} for realm {realmPtr}", ex);
                }
    }

    /// <summary>
    /// Disposes all JavaScript classes associated with a specific realm.
    /// </summary>
    /// <param name="realmPtr">The realm pointer.</param>
    /// <exception cref="HakoException">Failed to dispose one or more classes.</exception>
    internal void DisposeJSClassesForRealm(int realmPtr)
    {
        // Find and dispose all JSClasses belonging to this realm
        var classesToRemove = JSClassRegistry
            .Where(kv => kv.Key.RealmPtr == realmPtr)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in classesToRemove)
        {
            if (JSClassRegistry.TryRemove(key, out var jsClass))
            {
                try
                {
                    jsClass.Dispose();
                }
                catch (Exception ex)
                {
                    throw new HakoException($"Error disposing JSClass '{key.TypeKey}' for realm {realmPtr}", ex);
                }
            }
        }
    }

    /// <summary>
    /// Removes a realm from tracking and cleans up associated resources.
    /// </summary>
    /// <param name="realm">The realm to drop.</param>
    internal void DropRealm(Realm realm)
    {
        DisposeModulesForRealm(realm.Pointer);
        DisposeJSClassesForRealm(realm.Pointer);
        _realmMap.TryRemove(realm.Pointer, out _);
    }

    #endregion

    #region Runtime Configuration

    /// <summary>
    /// Configures which debug information is stripped from compiled bytecode.
    /// </summary>
    /// <param name="options">The strip options to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Stripping debug information reduces bytecode size but makes debugging more difficult.
    /// </remarks>
    public void SetStripInfo(StripOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Registry.SetStripInfo(Pointer, options.ToNativeFlags());
    }

    /// <summary>
    /// Gets the current strip options for compiled bytecode.
    /// </summary>
    /// <returns>The current <see cref="StripOptions"/>.</returns>
    public StripOptions GetStripInfo()
    {
        var flags = Registry.GetStripInfo(Pointer);
        return StripOptions.FromNativeFlags(flags);
    }

    /// <summary>
    /// Sets the maximum memory limit for the runtime in bytes.
    /// </summary>
    /// <param name="limitBytes">The memory limit in bytes, or -1 for unlimited.</param>
    /// <remarks>
    /// <para>
    /// When the limit is exceeded, memory allocations fail and scripts throw out-of-memory errors.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// runtime.SetMemoryLimit(50 * 1024 * 1024); // 50MB limit
    /// </code>
    /// </para>
    /// </remarks>
    public void SetMemoryLimit(int limitBytes = -1)
    {
        Registry.RuntimeSetMemoryLimit(Pointer, limitBytes);
    }

    /// <summary>
    /// Initializes the TypeScript type stripper.
    /// </summary>
    /// <exception cref="HakoException">Failed to initialize the type stripper.</exception>
    /// <remarks>
    /// This is called automatically during runtime creation. You don't need to call this manually.
    /// </remarks>
    internal void InitTypeStripper()
    {
        var status = Registry.InitTypeStripper(Pointer);
        if (status != 0)
        {
            throw new HakoException($"Failed to initialize type stripper (status: {status})");
        }
    }

    /// <summary>
    /// Cleans up the TypeScript type stripper resources.
    /// </summary>
    /// <remarks>
    /// This is called automatically during runtime disposal. You don't need to call this manually.
    /// </remarks>
    private void CleanupTypeStripper()
    {
        Registry.CleanupTypeStripper(Pointer);
    }

    /// <summary>
    /// Strips TypeScript type annotations from source code, returning pure JavaScript.
    /// </summary>
    /// <param name="typescriptSource">The TypeScript source code to process.</param>
    /// <returns>JavaScript source code with type annotations removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typescriptSource"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The runtime has been disposed.</exception>
    /// <exception cref="HakoException">Failed to strip types from the source code.</exception>
    public string StripTypes(string typescriptSource)
    {
        ArgumentNullException.ThrowIfNull(typescriptSource);
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var sourcePtr = Memory.AllocateRuntimeString(Pointer, typescriptSource, out var length);
        using var outPtrHolder = Memory.AllocateRuntimePointerArray(Pointer, 1);
        using var outLenHolder = Memory.AllocateRuntimePointerArray(Pointer, 1);
        var status = Registry.StripTypes(
            Pointer,
            sourcePtr.Value,
            outPtrHolder.Value,
            outLenHolder.Value
        );
        if (status != 0)
        {
            throw new HakoException($"Failed to strip TypeScript types (status: {status})");
        }
        var jsPtr = Memory.ReadPointerFromArray(outPtrHolder, 0);
        var jsLen = Memory.ReadPointerFromArray(outLenHolder, 0);
        if (jsPtr == 0)
        {
            throw new HakoException("Type stripper returned null output");
        }
        try
        {
            return Memory.ReadString(jsPtr, jsLen);
        }
        finally
        {
            Memory.FreeRuntimeMemory(Pointer, jsPtr);
        }
    }


    #endregion

    #region Memory Management

    /// <summary>
    /// Runs the garbage collector to free unused memory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// QuickJS uses reference counting with a cycle-detecting garbage collector. This method
    /// forces a collection cycle, which can free memory from circular references.
    /// </para>
    /// <para>
    /// Normally you don't need to call this manually, but it can be useful for benchmarking
    /// or when you know you've just finished a memory-intensive operation.
    /// </para>
    /// </remarks>
    public void RunGC()
    {
        Registry.RunGC(Pointer);
    }

    /// <summary>
    /// Computes detailed memory usage statistics for a realm.
    /// </summary>
    /// <param name="realm">The realm to analyze, or <c>null</c> for the system realm.</param>
    /// <returns>A <see cref="MemoryUsage"/> object containing memory statistics.</returns>
    /// <remarks>
    /// <para>
    /// This method provides detailed information about memory allocation including:
    /// heap size, used memory, allocated objects, and memory by type.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var usage = runtime.ComputeMemoryUsage(realm);
    /// Console.WriteLine($"Memory used: {usage.MemoryUsedSize} bytes");
    /// Console.WriteLine($"Objects allocated: {usage.ObjectCount}");
    /// </code>
    /// </para>
    /// </remarks>
    public MemoryUsage? ComputeMemoryUsage(Realm? realm = null)
    {
        var targetRealm = realm ?? GetSystemRealm();
        var realmPtr = targetRealm.Pointer;
        var valuePtr = Registry.RuntimeComputeMemoryUsage(Pointer, realmPtr);

        if (valuePtr == 0)
        {
            return null;
        }

        try
        {
            var jsonValue = Registry.ToJson(realmPtr, valuePtr, 0);
            if (jsonValue == 0) throw new HakoException("Failed to convert memory usage to JSON");

            try
            {
                var strPtr = Registry.ToCString(realmPtr, jsonValue);
                if (strPtr == 0) throw new HakoException("Failed to get string from memory usage");

                try
                {
                    var str = Memory.ReadNullTerminatedString(strPtr);
                    var result = JsonSerializer.Deserialize(str, JsonContext.Default.MemoryUsage);
                    return result;
                }
                finally
                {
                    Memory.FreeCString(realmPtr, strPtr);
                }
            }
            finally
            {
                Memory.FreeValuePointer(realmPtr, jsonValue);
            }
        }
        finally
        {
            Memory.FreeValuePointer(realmPtr, valuePtr);
        }
    }

    /// <summary>
    /// Dumps memory usage information as a formatted string.
    /// </summary>
    /// <returns>A human-readable string describing memory usage.</returns>
    /// <remarks>
    /// This is useful for debugging and logging memory consumption patterns.
    /// </remarks>
    public string DumpMemoryUsage()
    {
        var strPtr = Registry.RuntimeDumpMemoryUsage(Pointer);
        var str = Memory.ReadNullTerminatedString(strPtr);
        Memory.FreeRuntimeMemory(Pointer, strPtr);
        return str;
    }

    internal DisposableValue<int> AllocateMemory(int size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Memory.AllocateRuntimeMemory(Pointer, size);
    }

    internal void FreeMemory(DisposableValue<int> ptr)
    {
        Memory.FreeRuntimeMemory(Pointer, ptr);
    }

    #endregion

    #region Module Loading

    /// <summary>
    /// Enables the ES6 module loader with custom resolution logic.
    /// </summary>
    /// <param name="loader">A function that resolves module imports.</param>
    /// <param name="normalizer">Optional function to normalize relative module paths.</param>
    /// <exception cref="ArgumentNullException"><paramref name="loader"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// The loader function is called when JavaScript code imports a module. It should return
    /// a <see cref="ModuleLoaderResult"/> containing either a pre-compiled module, source code, or an error.
    /// </para>
    /// <para>
    /// Most users should use <see cref="HakoRuntimeExtensions.ConfigureModules"/> instead:
    /// <code>
    /// runtime.ConfigureModules()
    ///     .WithJsonModule("config", jsonString)
    ///     .WithLoader((name, attrs) =>
    ///     {
    ///         if (File.Exists($"{name}.js"))
    ///             return ModuleLoaderResult.Source(File.ReadAllText($"{name}.js"));
    ///         return ModuleLoaderResult.Error();
    ///     })
    ///     .Apply();
    /// </code>
    /// </para>
    /// </remarks>
    public void EnableModuleLoader(
        ModuleLoaderFunction loader,
        ModuleNormalizerFunction? normalizer = null)
    {
        ArgumentNullException.ThrowIfNull(loader);

        Callbacks.SetModuleLoader(loader);

        if (normalizer != null) Callbacks.SetModuleNormalizer(normalizer);

        Registry.RuntimeEnableModuleLoader(Pointer, normalizer != null ? 1 : 0, 0);
    }

    /// <summary>
    /// Disables the module loader, preventing any module imports.
    /// </summary>
    public void DisableModuleLoader()
    {
        Callbacks.SetModuleLoader(null);
        Callbacks.SetModuleNormalizer(null);
        Registry.RuntimeDisableModuleLoader(Pointer);
    }

    #endregion

    #region Interrupt Handling

    /// <summary>
    /// Enables an interrupt handler that can stop JavaScript execution.
    /// </summary>
    /// <param name="handler">A function called periodically during execution that returns <c>true</c> to interrupt.</param>
    /// <param name="opaque">An optional opaque value passed to the handler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// The handler is called periodically during JavaScript execution. Return <c>true</c> to interrupt
    /// execution and throw an error, or <c>false</c> to continue.
    /// </para>
    /// <para>
    /// Example with timeout:
    /// <code>
    /// var handler = HakoRuntime.CreateDeadlineInterruptHandler(5000); // 5 second timeout
    /// runtime.EnableInterruptHandler(handler);
    /// 
    /// try
    /// {
    ///     await realm.EvalAsync("while(true) {}"); // Will be interrupted after 5s
    /// }
    /// catch (HakoException ex)
    /// {
    ///     Console.WriteLine("Execution interrupted");
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public void EnableInterruptHandler(InterruptHandler handler, int opaque = 0)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _currentInterruptHandler = handler;
        Callbacks.SetInterruptHandler(handler);
        Registry.RuntimeEnableInterruptHandler(Pointer, opaque);
    }

    /// <summary>
    /// Disables the interrupt handler.
    /// </summary>
    public void DisableInterruptHandler()
    {
        _currentInterruptHandler = null;
        Callbacks.SetInterruptHandler(null);
        Registry.RuntimeDisableInterruptHandler(Pointer);
    }

    /// <summary>
    /// Creates an interrupt handler that stops execution after a time limit.
    /// </summary>
    /// <param name="deadlineMs">The timeout in milliseconds.</param>
    /// <returns>An <see cref="InterruptHandler"/> that interrupts after the deadline.</returns>
    /// <remarks>
    /// Use this for time-based execution limits. The handler checks the current time on each call.
    /// </remarks>
    public static InterruptHandler CreateDeadlineInterruptHandler(int deadlineMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(deadlineMs);
        return (_, _, _) => DateTime.UtcNow >= deadline;
    }

    /// <summary>
    /// Creates an interrupt handler that stops execution after a maximum number of operations.
    /// </summary>
    /// <param name="maxGas">The maximum number of operations allowed.</param>
    /// <returns>An <see cref="InterruptHandler"/> that interrupts after reaching the gas limit.</returns>
    /// <remarks>
    /// <para>
    /// "Gas" refers to a simple operation counter. Each handler invocation increments the counter.
    /// This provides a rough CPU usage limit.
    /// </para>
    /// </remarks>
    public static InterruptHandler CreateGasInterruptHandler(int maxGas)
    {
        var gas = 0;
        return (_, _, _) =>
        {
            gas++;
            return gas >= maxGas;
        };
    }

    /// <summary>
    /// Creates an interrupt handler that stops execution when memory usage exceeds a limit.
    /// </summary>
    /// <param name="maxMemoryBytes">The maximum memory in bytes.</param>
    /// <param name="checkIntervalSteps">How often to check memory (every N handler calls).</param>
    /// <returns>An <see cref="InterruptHandler"/> that interrupts on memory limit exceeded.</returns>
    /// <remarks>
    /// <para>
    /// Memory checks are expensive, so the handler only checks every <paramref name="checkIntervalSteps"/> calls.
    /// Higher values improve performance but reduce check frequency.
    /// </para>
    /// </remarks>
    public static InterruptHandler CreateMemoryInterruptHandler(long maxMemoryBytes, int checkIntervalSteps = 1000)
    {
        var steps = 0;
        return (runtime, realm, _) =>
        {
            steps++;
            if (steps % checkIntervalSteps == 0)
            {
                var memoryUsage = runtime.ComputeMemoryUsage(realm);
                if (memoryUsage.MemoryUsedSize > maxMemoryBytes) return true;
            }

            return false;
        };
    }

    /// <summary>
    /// Combines multiple interrupt handlers into a single handler.
    /// </summary>
    /// <param name="handlers">The handlers to combine.</param>
    /// <returns>An <see cref="InterruptHandler"/> that interrupts if any handler returns <c>true</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handlers"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// Use this to enforce multiple limits simultaneously:
    /// <code>
    /// var combined = HakoRuntime.CombineInterruptHandlers(
    ///     HakoRuntime.CreateDeadlineInterruptHandler(5000),
    ///     HakoRuntime.CreateMemoryInterruptHandler(10 * 1024 * 1024)
    /// );
    /// runtime.EnableInterruptHandler(combined);
    /// </code>
    /// </para>
    /// </remarks>
    public static InterruptHandler CombineInterruptHandlers(params InterruptHandler[] handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        return (runtime, realm, opaque) =>
        {
            foreach (var handler in handlers)
                if (handler(runtime, realm, opaque))
                    return true;

            return false;
        };
    }

    #endregion

    #region Promise Rejection Tracking

    /// <summary>
    /// Sets a callback to track unhandled promise rejections.
    /// </summary>
    /// <param name="handler">The callback function invoked for promise rejections.</param>
    /// <param name="opaque">An optional opaque value passed to the handler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// This is similar to the browser's <c>unhandledrejection</c> event. Use it to log or handle
    /// promises that reject without a catch handler.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// runtime.OnUnhandledRejection((rt, isHandled, promise, reason, ctx) =>
    /// {
    ///     if (!isHandled)
    ///     {
    ///         Console.WriteLine($"Unhandled rejection: {reason.AsString()}");
    ///     }
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public void OnUnhandledRejection(PromiseRejectionTrackerFunction handler, int opaque = 0)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _currentPromiseRejectionTracker = handler;
        Callbacks.SetPromiseRejectionTracker(handler);
        Registry.SetPromiseRejectionHandler(Pointer, opaque);
    }

    /// <summary>
    /// Disables promise rejection tracking.
    /// </summary>
    public void DisablePromiseRejectionTracker()
    {
        _currentPromiseRejectionTracker = null;
        Callbacks.SetPromiseRejectionTracker(null);
        Registry.ClearPromiseRejectionHandler(Pointer);
    }

    #endregion

    #region Microtask and Macrotask Execution

    /// <summary>
    /// Checks if there are pending microtasks (promise jobs) waiting to be executed.
    /// </summary>
    /// <returns><c>true</c> if microtasks are pending; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This is used internally by the event loop to determine if there's work to do.
    /// Microtasks include promise callbacks and other queued operations that run before macrotasks.
    /// </remarks>
    internal bool IsMicrotaskPending()
    {
        return Registry.IsJobPending(Pointer) != 0;
    }

    /// <summary>
    /// Executes pending microtasks (promise jobs) in the microtask queue.
    /// </summary>
    /// <param name="maxMicrotasksToExecute">Maximum number of microtasks to execute, or -1 for unlimited.</param>
    /// <returns>
    /// An <see cref="ExecuteMicrotasksResult"/> indicating success or containing an error if a microtask threw.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is used internally by the event loop. Most users don't need to call this directly,
    /// as <see cref="Hako.Dispatcher"/> handles microtask execution automatically.
    /// </para>
    /// <para>
    /// Microtasks include promise callbacks, queueMicrotask(), and other operations that run
    /// before the next macrotask (timer, I/O callback, etc.).
    /// </para>
    /// </remarks>
    internal ExecuteMicrotasksResult ExecuteMicrotasks(int maxMicrotasksToExecute = -1)
    {
        using var realmPtrOut = Memory.AllocateRuntimePointerArray(Pointer, 1);
        var result = Registry.ExecutePendingJob(Pointer, maxMicrotasksToExecute, realmPtrOut);

        if (result == -1)
        {
            var realmPtr = Memory.ReadPointerFromArray(realmPtrOut, 0);
            if (realmPtr > 0)
            {
                var realm = CreateRealm(new RealmOptions { RealmPointer = realmPtr });
                var exception = Errors.GetLastErrorPointer(realmPtr);
                if (exception > 0)
                    return ExecuteMicrotasksResult.Failure(
                        JSValue.FromHandle(realm, exception, ValueLifecycle.Owned), realm);
            }
        }

        return ExecuteMicrotasksResult.Success(result);
    }

    /// <summary>
    /// Executes due macrotasks (timers: setTimeout/setInterval) across all realms.
    /// </summary>
    /// <returns>The time in milliseconds until the next timer is due, or -1 if no timers are pending.</returns>
    /// <remarks>
    /// <para>
    /// This is used internally by the event loop. Most users don't need to call this directly.
    /// </para>
    /// <para>
    /// Macrotasks are executed after all microtasks have been flushed. Timers are one type of macrotask
    /// in the JavaScript event loop.
    /// </para>
    /// </remarks>
    internal int ExecuteTimers()
    {
        var nextTimerDue = int.MaxValue;

        // Take a snapshot to avoid collection modified exception
        // The realm map can be modified during timer execution
        var realms = _realmMap.Values.ToList();

        foreach (var realm in realms)
        {
            var nextTimerMs = realm.Timers.ProcessTimers();

            if (nextTimerMs > 0)
                nextTimerDue = Math.Min(nextTimerDue, nextTimerMs);
            else if (nextTimerMs == 0)
                nextTimerDue = 0;
        }

        return nextTimerDue == int.MaxValue ? -1 : nextTimerDue;
    }

    #endregion
}