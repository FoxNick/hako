using System.Text.Json;
using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.Host;
using HakoJS.Lifetime;
using HakoJS.SourceGeneration;
using HakoJS.Utils;

namespace HakoJS.VM;

/// <summary>
/// Represents a JavaScript execution context (realm) with its own global object and built-in objects.
/// </summary>
/// <remarks>
/// <para>
/// A realm is an isolated JavaScript environment where code is evaluated and executed. Each realm
/// has its own global scope, built-in prototypes, and object instances. Multiple realms can exist
/// within a single <see cref="HakoRuntime"/>, allowing for sandboxed script execution.
/// </para>
/// <para>
/// Most users should use extension methods from <see cref="RealmExtensions"/> for common operations
/// like <see cref="RealmExtensions.EvalAsync"/> instead of calling low-level methods directly.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// using var realm = runtime.CreateRealm();
/// 
/// // Evaluate code (use extension method)
/// var result = await realm.EvalAsync&lt;int&gt;("2 + 2"); // 4
/// 
/// // Create and manipulate objects
/// using var obj = realm.NewObject();
/// obj.SetProperty("name", "Alice");
/// 
/// // Create functions
/// using var func = realm.NewFunction("add", (ctx, thisArg, args) =>
/// {
///     var a = args[0].AsNumber();
///     var b = args[1].AsNumber();
///     return ctx.NewNumber(a + b);
/// });
/// </code>
/// </para>
/// </remarks>
public sealed class Realm : IDisposable
{
    private readonly ValueFactory _valueFactory;
    private bool _disposed;
    private int _opaqueDataPointer;

    private JSValue? _symbol;
    private JSValue? _symbolAsyncIterator;
    private JSValue? _symbolIterator;
    private TimerManager? _timerManager;
    private readonly ConcurrentHashSet<JSValue> _trackedValues = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Realm"/> class.
    /// </summary>
    /// <param name="runtime">The runtime that owns this realm.</param>
    /// <param name="ctxPtr">The QuickJS context pointer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runtime"/> is <c>null</c>.</exception>
    /// <remarks>
    /// This constructor is internal. Use <see cref="HakoRuntime.CreateRealm"/> to create realm instances.
    /// </remarks>
    internal Realm(HakoRuntime runtime, int ctxPtr)
    {
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Pointer = ctxPtr;
        _valueFactory = new ValueFactory(this);
        Runtime.Callbacks.RegisterContext(ctxPtr, this);
    }

    /// <summary>
    /// Gets the timer manager for this realm, used internally for setTimeout/setInterval support.
    /// </summary>
    internal TimerManager Timers
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _timerManager ??= new TimerManager(this);
        }
    }
    

    /// <summary>
    /// Gets the QuickJS context pointer for this realm.
    /// </summary>
    /// <value>An integer representing the native QuickJS context pointer.</value>
    public int Pointer { get; }

    /// <summary>
    /// Gets the runtime that owns this realm.
    /// </summary>
    /// <value>The <see cref="HakoRuntime"/> instance that created this realm.</value>
    public HakoRuntime Runtime { get; }

    #region Disposal

    /// <summary>
    /// Disposes the realm, releasing all associated resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All <see cref="JSValue"/> instances created by this realm become invalid after disposal
    /// and will throw exceptions if used.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        
        foreach (var value in _trackedValues)
        {
           value.Dispose();
        }
        
        _trackedValues.Clear();
        _trackedValues.Dispose();

        _timerManager?.Dispose();
        _valueFactory.Dispose();
        _symbol?.Dispose();
        _symbolAsyncIterator?.Dispose();
        _symbolIterator?.Dispose();

        Runtime.Callbacks.UnregisterContext(Pointer);

        if (_opaqueDataPointer != 0)
        {
            FreeMemory(_opaqueDataPointer);
            _opaqueDataPointer = 0;
            Runtime.Registry.SetContextData(Pointer, 0);
        }

        Runtime.Registry.FreeContext(Pointer);
        Runtime.DropRealm(this);

        _disposed = true;
    }
    
    /// <summary>
    /// Registers a JSValue for automatic cleanup when the realm is disposed.
    /// Use this for captured delegates or other long-lived JSValues in JSObjects.
    /// </summary>
    /// <param name="value">The JSValue to track.</param>
    /// <remarks>
    /// Tracked values are automatically disposed when the realm is disposed,
    /// preventing memory leaks from captured delegate closures.
    /// </remarks>
    public void TrackValue(JSValue? value)
    {
        if (value == null) return;
        if(!_trackedValues.Add(value))
        {
            throw new HakoException("Tracked value already tracked");
        }
    }
    
    /// <summary>
    /// Unregisters a previously tracked JSValue.
    /// Call this if you manually dispose a tracked value before the realm is disposed.
    /// </summary>
    /// <param name="value">The JSValue to stop tracking.</param>
    public void UntrackValue(JSValue? value)
    {
        if (value == null) return;
        if (!_trackedValues.Remove(value))
        {
            throw new HakoException("Tracked value already untracked");
        }
    }

    #endregion

    #region Function Calls

    /// <summary>
    /// Calls a JavaScript function with optional 'this' binding and arguments.
    /// </summary>
    /// <param name="func">The function to call.</param>
    /// <param name="thisArg">The value to use as 'this', or <c>null</c> for <c>undefined</c>.</param>
    /// <param name="args">The arguments to pass to the function.</param>
    /// <returns>
    /// A <see cref="DisposableResult{TSuccess, TFailure}"/> containing either the return value
    /// or an error if the function threw an exception.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is the fundamental method for calling JavaScript functions from .NET.
    /// Most users should use extension methods like <see cref="JSValueExtensions.Invoke"/> instead.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var func = await realm.EvalAsync("(x, y) => x + y");
    /// using var arg1 = realm.NewNumber(5);
    /// using var arg2 = realm.NewNumber(3);
    /// 
    /// using var result = realm.CallFunction(func, null, arg1, arg2);
    /// if (result.TryGetSuccess(out var value))
    /// {
    ///     Console.WriteLine(value.AsNumber()); // 8
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public DisposableResult<JSValue, JSValue> CallFunction(JSValue func, JSValue? thisArg = null, params JSValue[] args)
    {
        JSValue? tempThisArg = null;
        try
        {
            if (thisArg == null)
            {
                tempThisArg = Undefined();
                thisArg = tempThisArg;
            }

            var thisPtr = thisArg.GetHandle();
            int resultPtr;

            if (args.Length > 0)
            {
                using var argvPtr = AllocatePointerArray(args.Length);

                for (var i = 0; i < args.Length; i++) WritePointerToArray(argvPtr, i, args[i].GetHandle());

                resultPtr = Runtime.Registry.Call(
                    Pointer,
                    func.GetHandle(),
                    thisPtr,
                    args.Length,
                    argvPtr);
            }
            else
            {
                resultPtr = Runtime.Registry.Call(
                    Pointer,
                    func.GetHandle(),
                    thisPtr,
                    0,
                    0);
            }

            var exceptionPtr = Runtime.Errors.GetLastErrorPointer(Pointer, resultPtr);
            if (exceptionPtr != 0)
            {
                FreeValuePointer(resultPtr);
                return DisposableResult<JSValue, JSValue>.Failure(
                    new JSValue(this, exceptionPtr));
            }

            return DisposableResult<JSValue, JSValue>.Success(
                new JSValue(this, resultPtr));
        }
        finally
        {
            tempThisArg?.Dispose();
        }
    }

    #endregion

    #region Symbol Operations

    /// <summary>
    /// Gets a well-known symbol by name (e.g., "iterator", "asyncIterator", "toStringTag").
    /// </summary>
    /// <param name="name">The symbol name (without "Symbol." prefix).</param>
    /// <returns>A <see cref="JSValue"/> representing the symbol.</returns>
    /// <remarks>
    /// <para>
    /// Well-known symbols are cached after first access for performance.
    /// Common symbols include: iterator, asyncIterator, hasInstance, toStringTag, toPrimitive.
    /// </para>
    /// </remarks>
    public JSValue GetWellKnownSymbol(string name)
    {
        if (_symbol == null)
        {
            using var globalObject = GetGlobalObject();
            _symbol = globalObject.GetProperty("Symbol");
        }

        return _symbol.GetProperty(name);
    }

    #endregion

    #region Iterator Support

    /// <summary>
    /// Gets an iterator for a JavaScript iterable (arrays, sets, maps, etc.).
    /// </summary>
    /// <param name="iterableHandle">The iterable value.</param>
    /// <returns>
    /// A result containing a <see cref="JSIterator"/> or an error if the object is not iterable.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This calls the object's Symbol.iterator method to obtain an iterator.
    /// Most users should use <see cref="JSValueExtensions.Iterate"/> instead.
    /// </para>
    /// </remarks>
    public DisposableResult<JSIterator, JSValue> GetIterator(JSValue iterableHandle)
    {
        _symbolIterator ??= GetWellKnownSymbol("iterator");

        using var methodHandle = iterableHandle.GetProperty(_symbolIterator);
        var iteratorCallResult = CallFunction(methodHandle, iterableHandle);

        if (iteratorCallResult.TryGetFailure(out var error))
            return DisposableResult<JSIterator, JSValue>.Failure(error);

        if (iteratorCallResult.TryGetSuccess(out var iteratorValue))
            return DisposableResult<JSIterator, JSValue>.Success(
                new JSIterator(iteratorValue, this));

        throw new InvalidOperationException("Iterator call result is in invalid state");
    }
    
    /// <summary>
    /// Gets an async iterator for a JavaScript async iterable (async generators, etc.).
    /// </summary>
    /// <param name="iterableHandle">The async iterable value.</param>
    /// <returns>
    /// A result containing a <see cref="JSAsyncIterator"/> or an error if the object is not async iterable.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This calls the object's Symbol.asyncIterator method to obtain an async iterator.
    /// Most users should use <see cref="JSValueExtensions.IterateAsync"/> instead.
    /// </para>
    /// </remarks>
    public DisposableResult<JSAsyncIterator, JSValue> GetAsyncIterator(JSValue iterableHandle)
    {
        _symbolAsyncIterator ??= GetWellKnownSymbol("asyncIterator");

        using var methodHandle = iterableHandle.GetProperty(_symbolAsyncIterator);
    
        // If no async iterator method exists, throw an error
        if (methodHandle.IsNullOrUndefined())
        {
            return DisposableResult<JSAsyncIterator, JSValue>.Failure(NewError(new InvalidOperationException(
                "Object is not async iterable (no Symbol.asyncIterator method)")));
        }

        var iteratorCallResult = CallFunction(methodHandle, iterableHandle);

        if (iteratorCallResult.TryGetFailure(out var error))
            return DisposableResult<JSAsyncIterator, JSValue>.Failure(error);

        if (iteratorCallResult.TryGetSuccess(out var iteratorValue))
            return DisposableResult<JSAsyncIterator, JSValue>.Success(
                new JSAsyncIterator(iteratorValue, this));

        throw new InvalidOperationException("Async iterator call result is in invalid state");
    }

    #endregion

    #region Promise Operations

    /// <summary>
    /// Awaits a JavaScript Promise and returns its resolved value or rejection reason.
    /// </summary>
    /// <param name="promiseLikeHandle">The Promise to await.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// A task containing either the fulfilled value or the rejection reason.
    /// </returns>
    /// <exception cref="InvalidOperationException">The value is not a Promise.</exception>
    /// <remarks>
    /// <para>
    /// This method is used internally by <see cref="RealmExtensions.EvalAsync"/> to await
    /// promises returned from evaluated code.
    /// </para>
    /// </remarks>
    public async Task<DisposableResult<JSValue, JSValue>> ResolvePromise(
        JSValue promiseLikeHandle,
        CancellationToken cancellationToken = default)
    {
        // If not on event loop, marshal there first
        if (!Hako.Dispatcher.CheckAccess())
            return await Hako.Dispatcher.InvokeAsync(() => ResolvePromise(promiseLikeHandle, cancellationToken),
                cancellationToken).ConfigureAwait(false);

        if (!promiseLikeHandle.IsPromise())
            throw new InvalidOperationException($"Expected a Promise-like value, received {promiseLikeHandle.Type}");

        var state = promiseLikeHandle.GetPromiseState();

        if (state == PromiseState.Fulfilled)
        {
            var result = promiseLikeHandle.GetPromiseResult();
            return DisposableResult<JSValue, JSValue>.Success(result ?? NewValue(null));
        }

        if (state == PromiseState.Rejected)
        {
            var errorResult = promiseLikeHandle.GetPromiseResult();
            return DisposableResult<JSValue, JSValue>.Failure(errorResult ?? NewValue(null));
        }

        var tcs = new TaskCompletionSource<DisposableResult<JSValue, JSValue>>(TaskCreationOptions
            .RunContinuationsAsynchronously);

        // Link cancellation token
        await using var ctr = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        using (var resolveHandle = NewFunction("resolve", (realm, _, args) =>
               {
                   var value = args.Length > 0 ? args[0].Dup() : null;
                   tcs.TrySetResult(DisposableResult<JSValue, JSValue>.Success(value ?? NewValue(null)));
                   return null;
               }))
        using (var rejectHandle = NewFunction("reject", (realm, _, args) =>
               {
                   var errorVal = args.Length > 0 ? args[0].Dup() : null;
                   tcs.TrySetResult(DisposableResult<JSValue, JSValue>.Failure(errorVal ?? NewValue(null)));
                   return null;
               }))
        using (var promiseThenHandle = promiseLikeHandle.GetProperty("then"))
        {
            using var result = CallFunction(promiseThenHandle, promiseLikeHandle, resolveHandle, rejectHandle);
            if (result.TryGetFailure(out var thenError))
            {
                thenError.Dispose();
                return DisposableResult<JSValue, JSValue>.Failure(
                    NewValue(new Exception("Failed to attach promise handlers")));
            }

            if (result.TryGetSuccess(out var success)) success.Dispose();
        }

        await Hako.Dispatcher.Yield();

        if (tcs.Task.IsCompleted)
            return await tcs.Task.ConfigureAwait(false);

        while (!tcs.Task.IsCompleted && !cancellationToken.IsCancellationRequested)
            await Hako.Dispatcher.Yield();

        return await tcs.Task.ConfigureAwait(false);
    }

    #endregion

    #region Realm Configuration

    /// <summary>
    /// Stores custom string data associated with this realm.
    /// </summary>
    /// <param name="opaque">The string data to store.</param>
    /// <remarks>
    /// This can be used to associate metadata or identifiers with a realm instance.
    /// The data is automatically freed when the realm is disposed.
    /// </remarks>
    public void SetOpaqueData(string opaque)
    {
        if (_opaqueDataPointer != 0)
        {
            FreeMemory(_opaqueDataPointer);
            _opaqueDataPointer = 0;
            Runtime.Registry.SetContextData(Pointer, 0);
        }

        _opaqueDataPointer = AllocateString(opaque, out _);
        Runtime.Registry.SetContextData(Pointer, _opaqueDataPointer);
    }

    /// <summary>
    /// Retrieves the custom string data associated with this realm.
    /// </summary>
    /// <returns>The stored string data, or <c>null</c> if none was set.</returns>
    public string? GetOpaqueData()
    {
        if (_opaqueDataPointer == 0) return null;
        return ReadString(_opaqueDataPointer);
    }

    #endregion

    #region Memory Convenience Methods

    internal DisposableValue<int> AllocateMemory(int size)
    {
        return Runtime.Memory.AllocateMemory(Pointer, size);
    }

    internal void FreeMemory(int ptr)
    {
        Runtime.Memory.FreeMemory(Pointer, ptr);
    }

    internal DisposableValue<int> AllocateString(string str, out int length)
    {
        return Runtime.Memory.AllocateString(Pointer, str, out length);
    }

    private DisposableValue<(int Pointer, int Length)> WriteNullTerminatedString(string str)
    {
        return Runtime.Memory.WriteNullTerminatedString(Pointer, str);
    }

    internal string ReadString(int ptr)
    {
        return Runtime.Memory.ReadNullTerminatedString(ptr);
    }

    internal void FreeCString(int ptr)
    {
        Runtime.Memory.FreeCString(Pointer, ptr);
    }

    private int WriteBytes(ReadOnlySpan<byte> bytes)
    {
        return Runtime.Memory.WriteBytes(Pointer, bytes);
    }

    internal byte[] CopyMemory(int offset, int length)
    {
        return Runtime.Memory.Copy(offset, length);
    }

    internal Span<byte> SliceMemory(int offset, int length)
    {
        return Runtime.Memory.Slice(offset, length);
    }

    internal void FreeValuePointer(int ptr)
    {
        Runtime.Memory.FreeValuePointer(Pointer, ptr);
    }

    internal int DupValuePointer(int ptr)
    {
        return Runtime.Memory.DupValuePointer(Pointer, ptr);
    }

    internal int NewArrayBufferPtr(ReadOnlySpan<byte> data)
    {
        return Runtime.Memory.NewArrayBuffer(Pointer, data);
    }

    internal DisposableValue<int> AllocatePointerArray(int count)
    {
        return Runtime.Memory.AllocatePointerArray(Pointer, count);
    }

    internal int WritePointerToArray(int arrayPtr, int index, int value)
    {
        return Runtime.Memory.WritePointerToArray(arrayPtr, index, value);
    }

    internal int ReadPointerFromArray(int arrayPtr, int index)
    {
        return Runtime.Memory.ReadPointerFromArray(arrayPtr, index);
    }

    internal int ReadPointer(int address)
    {
        return Runtime.Memory.ReadPointer(address);
    }

    internal uint ReadUint32(int address)
    {
        return Runtime.Memory.ReadUint32(address);
    }

    internal void WriteUint32(int address, uint value)
    {
        Runtime.Memory.WriteUint32(address, value);
    }

    internal long ReadInt64(int address)
    {
        return Runtime.Memory.ReadInt64(address);
    }

    internal void WriteInt64(int address, long value)
    {
        Runtime.Memory.WriteInt64(address, value);
    }

    #endregion

    #region Code Evaluation

    /// <summary>
    /// Evaluates JavaScript code synchronously.
    /// </summary>
    /// <param name="code">The JavaScript code to evaluate.</param>
    /// <param name="options">Optional evaluation options.</param>
    /// <returns>
    /// A result containing either the evaluation result or an error.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Most users should use <see cref="RealmExtensions.EvalAsync"/> instead, which properly
    /// handles promises and async code.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var result = realm.EvalCode("2 + 2");
    /// if (result.TryGetSuccess(out var value))
    /// {
    ///     Console.WriteLine(value.AsNumber()); // 4
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public DisposableResult<JSValue, JSValue> EvalCode(string code, RealmEvalOptions? options = null)
    {
        options ??= new RealmEvalOptions();

        if (code.Length == 0) return DisposableResult<JSValue, JSValue>.Success(Undefined());

        using var codemem = WriteNullTerminatedString(code);

        var fileName = options.FileName;
        if (!fileName.StartsWith("file://")) fileName = $"file://{fileName}";

        using var filenamePtr = AllocateString(fileName, out _);
        var flags = options.ToFlags();
        var detectModule = options.DetectModule ? 1 : 0;

        var resultPtr = Runtime.Registry.Eval(
            Pointer,
            codemem.Value.Pointer,
            codemem.Value.Length,
            filenamePtr,
            detectModule,
            (int)flags);
        var exceptionPtr = Runtime.Errors.GetLastErrorPointer(Pointer, resultPtr);
        if (exceptionPtr != 0)
        {
            FreeValuePointer(resultPtr);
            return DisposableResult<JSValue, JSValue>.Failure(
                new JSValue(this, exceptionPtr));
        }

        return DisposableResult<JSValue, JSValue>.Success(
            new JSValue(this, resultPtr));
    }

    /// <summary>
    /// Compiles JavaScript code to QuickJS bytecode.
    /// </summary>
    /// <param name="code">The JavaScript code to compile.</param>
    /// <param name="options">Optional compilation options.</param>
    /// <returns>
    /// A result containing either the bytecode or an error.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Bytecode can be cached and executed later with <see cref="EvalByteCode"/> for faster startup.
    /// </para>
    /// </remarks>
    public DisposableResult<byte[], JSValue> CompileToByteCode(string code, RealmEvalOptions? options = null)
    {
        options ??= new RealmEvalOptions();

        if (code.Length == 0) return DisposableResult<byte[], JSValue>.Success([]);

        using var codemem = WriteNullTerminatedString(code);
        var fileName = options.FileName;
        if (!fileName.StartsWith("file://")) fileName = $"file://{fileName}";

        using var filemem = WriteNullTerminatedString(fileName);
        var flags = options.ToFlags();
        var detectModule = options.DetectModule ? 1 : 0;
        int bytecodeLength = AllocatePointerArray(1);

        var bytecodePtr = Runtime.Registry.CompileToByteCode(
            Pointer,
            codemem.Value.Pointer,
            codemem.Value.Length,
            filemem.Value.Pointer,
            detectModule,
            (int)flags,
            bytecodeLength);

        if (bytecodePtr == 0)
        {
            var exceptionPtr = Runtime.Errors.GetLastErrorPointer(Pointer);
            if (exceptionPtr != 0)
                return DisposableResult<byte[], JSValue>.Failure(
                    new JSValue(this, exceptionPtr));

            return DisposableResult<byte[], JSValue>.Failure(
                NewError(new Exception("Compilation failed")));
        }

        var length = ReadPointer(bytecodeLength);
        var bytecode = CopyMemory(bytecodePtr, length);
        FreeMemory(bytecodePtr);

        return DisposableResult<byte[], JSValue>.Success(bytecode);
    }

    /// <summary>
    /// Executes QuickJS bytecode.
    /// </summary>
    /// <param name="bytecode">The bytecode to execute.</param>
    /// <param name="loadOnly">If <c>true</c>, only loads the bytecode without executing it.</param>
    /// <returns>
    /// A result containing either the execution result or an error.
    /// </returns>
    /// <remarks>
    /// Bytecode must have been compiled with <see cref="CompileToByteCode"/> or equivalent.
    /// </remarks>
    public DisposableResult<JSValue, JSValue> EvalByteCode(byte[] bytecode, bool loadOnly = false)
    {
        if (bytecode.Length == 0) return DisposableResult<JSValue, JSValue>.Success(Undefined());

        var bytecodePtr = WriteBytes(bytecode);

        try
        {
            var resultPtr = Runtime.Registry.EvalByteCode(
                Pointer,
                bytecodePtr,
                bytecode.Length,
                loadOnly ? 1 : 0);

            var exceptionPtr = Runtime.Errors.GetLastErrorPointer(Pointer, resultPtr);
            if (exceptionPtr != 0)
            {
                FreeValuePointer(resultPtr);
                return DisposableResult<JSValue, JSValue>.Failure(
                    new JSValue(this, exceptionPtr));
            }

            return DisposableResult<JSValue, JSValue>.Success(
                new JSValue(this, resultPtr));
        }
        finally
        {
            FreeMemory(bytecodePtr);
        }
    }

    #endregion

    #region Error Handling

    /// <summary>
    /// Gets the last JavaScript exception that occurred, if any.
    /// </summary>
    /// <param name="maybeException">Optional pointer to check for an exception.</param>
    /// <returns>
    /// A <see cref="JavaScriptException"/> containing error details, or <c>null</c> if no error.
    /// </returns>
    /// <remarks>
    /// If <paramref name="maybeException"/> is an exception value, it will be freed after reading.
    /// </remarks>
    internal JavaScriptException? GetLastError(int? maybeException = null)
    {
        if (maybeException is 0) return null;

        var pointer = maybeException ?? Runtime.Errors.GetLastErrorPointer(Pointer);

        if (pointer == 0) return null;

        var isError = Runtime.Registry.IsError(Pointer, pointer);
        var lastError = isError != 0
            ? pointer
            : Runtime.Errors.GetLastErrorPointer(Pointer, pointer);

        if (lastError == 0) return null;

        try
        {
            return Runtime.Errors.GetExceptionDetails(Pointer, lastError);
        }
        finally
        {
            FreeValuePointer(lastError);
        }
    }

    /// <summary>
    /// Creates a JavaScript Error object from a .NET exception.
    /// </summary>
    /// <param name="error">The .NET exception to convert.</param>
    /// <returns>A <see cref="JSValue"/> representing the JavaScript Error.</returns>
    public JSValue NewError(Exception error)
    {
        return _valueFactory.FromNativeValue(error);
    }

    /// <summary>
    /// Throws a JavaScript exception from a <see cref="JSValue"/> error.
    /// </summary>
    /// <param name="error">The error value to throw.</param>
    /// <returns>An exception value that can be returned from native functions.</returns>
    public JSValue ThrowError(JSValue error)
    {
        var exceptionPtr = Runtime.Registry.Throw(Pointer, error.GetHandle());
        return new JSValue(this, exceptionPtr);
    }

    /// <summary>
    /// Throws a JavaScript exception from a .NET exception.
    /// </summary>
    /// <param name="exception">The .NET exception to throw.</param>
    /// <returns>An exception value that can be returned from native functions.</returns>
    public JSValue ThrowError(Exception exception)
    {
        using var errorObj = NewError(exception);
        return ThrowError(errorObj);
    }

    /// <summary>
    /// Throws a JavaScript error of a specific type with a message.
    /// </summary>
    /// <param name="errorType">The type of error (Error, TypeError, RangeError, etc.).</param>
    /// <param name="message">The error message.</param>
    /// <returns>An exception value that can be returned from native functions.</returns>
    public JSValue ThrowError(JSErrorType errorType, string message)
    {
        using var messagePtr = AllocateString(message, out _);
        var exceptionPtr = Runtime.Registry.ThrowError(Pointer, (int)errorType, messagePtr);
        return new JSValue(this, exceptionPtr);
    }

    #endregion

    #region Value Creation

    /// <summary>
    /// Gets the global object for this realm.
    /// </summary>
    /// <returns>A <see cref="JSValue"/> representing the global object.</returns>
    /// <remarks>
    /// The global object contains all global variables and built-in objects like Object, Array, Math, etc.
    /// </remarks>
    public JSValue GetGlobalObject()
    {
        return _valueFactory.GetGlobalObject();
    }

    /// <summary>
    /// Creates a new empty JavaScript object.
    /// </summary>
    /// <returns>A <see cref="JSValue"/> representing the new object.</returns>
    public JSValue NewObject()
    {
        var ptr = Runtime.Registry.NewObject(Pointer);
        return new JSValue(this, ptr);
    }

    /// <summary>
    /// Creates a new JavaScript object with a specified prototype.
    /// </summary>
    /// <param name="proto">The prototype object.</param>
    /// <returns>A <see cref="JSValue"/> representing the new object.</returns>
    public JSValue NewObjectWithPrototype(JSValue proto)
    {
        var ptr = Runtime.Registry.NewObjectProto(Pointer, proto.GetHandle());
        return new JSValue(this, ptr);
    }

    /// <summary>
    /// Creates a new empty JavaScript array.
    /// </summary>
    /// <returns>A <see cref="JSValue"/> representing the new array.</returns>
    public JSValue NewArray()
    {
        return _valueFactory.FromNativeValue(Array.Empty<object>());
    }
    
    /// <summary>
    /// Creates a new JavaScript array from a variable number of JSValue objects.
    /// </summary>
    /// <param name="items">The JSValue objects to populate the array with.</param>
    /// <returns>A <see cref="JSValue"/> representing the new array.</returns>
    public JSValue NewArray(params object[] items)
    {
        var array = NewArray();
        try
        {
            for (int i = 0; i < items.Length; i++)
            {
                array.SetProperty(i, items[i]);
            }
            return array;
        }
        catch
        {
            array.Dispose();
            throw;
        }
    }
    
    /// <summary>
    /// Creates a new JavaScript array from an enumerable collection of JSValue objects.
    /// </summary>
    /// <param name="items">The JSValue objects to populate the array with.</param>
    /// <returns>A <see cref="JSValue"/> representing the new array.</returns>
    public JSValue NewArray(IEnumerable<object> items)
    {
        return NewArray(items.ToArray());
    }

    /// <summary>
    /// Creates a new ArrayBuffer from byte data.
    /// </summary>
    /// <param name="data">The byte data to copy into the ArrayBuffer.</param>
    /// <returns>A <see cref="JSValue"/> representing the ArrayBuffer.</returns>
    public JSValue NewArrayBuffer(byte[] data)
    {
        return _valueFactory.FromNativeValue(data);
    }

    #region TypedArray Operations

    /// <summary>
    /// Creates a new TypedArray of the specified type and length.
    /// </summary>
    /// <param name="length">The number of elements in the array.</param>
    /// <param name="type">The TypedArray type (Uint8Array, Int32Array, Float64Array, etc.).</param>
    /// <returns>A <see cref="JSValue"/> representing the TypedArray.</returns>
    /// <exception cref="HakoException">Failed to create the typed array.</exception>
    public JSValue NewTypedArray(int length, TypedArrayType type)
    {
        var resultPtr = Runtime.Registry.NewTypedArray(Pointer, length, (int)type);

        var error = GetLastError(resultPtr);
        if (error != null)
        {
            FreeValuePointer(resultPtr);
            throw new HakoException("Failed to create typed array", error);
        }

        return new JSValue(this, resultPtr);
    }

    /// <summary>
    /// Creates a new TypedArray backed by an existing ArrayBuffer.
    /// </summary>
    /// <param name="arrayBuffer">The ArrayBuffer to use as the backing store.</param>
    /// <param name="byteOffset">The byte offset into the buffer where the array starts.</param>
    /// <param name="length">The number of elements in the array.</param>
    /// <param name="type">The TypedArray type.</param>
    /// <returns>A <see cref="JSValue"/> representing the TypedArray.</returns>
    /// <exception cref="InvalidOperationException">The provided value is not an ArrayBuffer.</exception>
    /// <exception cref="HakoException">Failed to create the typed array.</exception>
    public JSValue NewTypedArrayWithBuffer(JSValue arrayBuffer, int byteOffset, int length, TypedArrayType type)
    {
        if (!arrayBuffer.IsArrayBuffer()) throw new InvalidOperationException("Provided value is not an ArrayBuffer");

        var resultPtr = Runtime.Registry.NewTypedArrayWithBuffer(
            Pointer,
            arrayBuffer.GetHandle(),
            byteOffset,
            length,
            (int)type);

        var error = GetLastError(resultPtr);
        if (error != null)
        {
            FreeValuePointer(resultPtr);
            throw new HakoException("Failed to create typed array with buffer", error);
        }

        return new JSValue(this, resultPtr);
    }

    /// <summary>
    /// Creates a new Uint8Array from byte data.
    /// </summary>
    /// <param name="data">The byte data to copy into the array.</param>
    /// <returns>A <see cref="JSValue"/> representing the Uint8Array.</returns>
    public JSValue NewUint8Array(byte[] data)
    {
        using var buffer = NewArrayBuffer(data);
        return NewTypedArrayWithBuffer(buffer, 0, data.Length, TypedArrayType.Uint8Array);
    }

    /// <summary>
    /// Creates a new Float64Array from double data.
    /// </summary>
    /// <param name="data">The double data to copy into the array.</param>
    /// <returns>A <see cref="JSValue"/> representing the Float64Array.</returns>
    public JSValue NewFloat64Array(double[] data)
    {
        var byteArray = new byte[data.Length * sizeof(double)];
        Buffer.BlockCopy(data, 0, byteArray, 0, byteArray.Length);

        using var buffer = NewArrayBuffer(byteArray);
        return NewTypedArrayWithBuffer(buffer, 0, data.Length, TypedArrayType.Float64Array);
    }

    /// <summary>
    /// Creates a new Int32Array from integer data.
    /// </summary>
    /// <param name="data">The integer data to copy into the array.</param>
    /// <returns>A <see cref="JSValue"/> representing the Int32Array.</returns>
    public JSValue NewInt32Array(int[] data)
    {
        var byteArray = new byte[data.Length * sizeof(int)];
        Buffer.BlockCopy(data, 0, byteArray, 0, byteArray.Length);

        using var buffer = NewArrayBuffer(byteArray);
        return NewTypedArrayWithBuffer(buffer, 0, data.Length, TypedArrayType.Int32Array);
    }

    #endregion

    /// <summary>
    /// Creates a new JavaScript number.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    /// <returns>A <see cref="JSValue"/> representing the number.</returns>
    public JSValue NewNumber(double value)
    {
        return _valueFactory.FromNativeValue(value);
    }

    /// <summary>
    /// Creates a new JavaScript string.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>A <see cref="JSValue"/> representing the string.</returns>
    public JSValue NewString(string value)
    {
        return _valueFactory.FromNativeValue(value);
    }

    /// <summary>
    /// Creates a new JavaScript Date
    /// </summary>
    /// <param name="value">The source DateTime</param>
    /// <returns>A <see cref="JSValue"/> representing the Date object.</returns>
    public JSValue NewDate(DateTime value)
    {
        return _valueFactory.FromNativeValue(value);
    }

    /// <summary>
    /// Creates a new JavaScript function with a specified name.
    /// </summary>
    /// <param name="name">The function name (used for stack traces and debugging).</param>
    /// <param name="callback">The .NET function to call when invoked from JavaScript.</param>
    /// <returns>A <see cref="JSValue"/> representing the function.</returns>
    /// <remarks>
    /// <para>
    /// The callback receives the realm, 'this' value, and arguments. Return a <see cref="JSValue"/>
    /// for the result, or <c>null</c> to return <c>undefined</c>.
    /// </para>
    /// </remarks>
    public JSValue NewFunction(string name, JSFunction callback)
    {
        var options = new Dictionary<string, object> { { "name", name } };
        return _valueFactory.FromNativeValue(callback, options);
    }

    /// <summary>
    /// Creates a new JavaScript function that returns <c>undefined</c>.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="callback">The .NET action to call when invoked from JavaScript.</param>
    /// <returns>A <see cref="JSValue"/> representing the function.</returns>
    public JSValue NewFunction(string name, JSAction callback)
    {
        var options = new Dictionary<string, object> { { "name", name } };
        return _valueFactory.FromNativeValue((JSFunction)JavaScriptFunction, options);

        JSValue? JavaScriptFunction(Realm realm, JSValue thisArg, JSValue[] args)
        {
            callback(realm, thisArg, args);
            return realm.Undefined();
        }
    }

    /// <summary>
    /// Creates a new async JavaScript function that returns a Promise.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="callback">The async .NET function to call when invoked from JavaScript.</param>
    /// <returns>A <see cref="JSValue"/> representing the async function.</returns>
    /// <remarks>
    /// The returned Promise resolves with the task's result or rejects if the task fails or is canceled.
    /// </remarks>
    public JSValue NewFunctionAsync(string name, JSAsyncFunction callback)
    {
        var options = new Dictionary<string, object> { { "name", name } };

        return _valueFactory.FromNativeValue((JSFunction)JavaScriptFunction, options);

        JSValue? JavaScriptFunction(Realm realm, JSValue thisArg, JSValue[] args)
        {
            var deferred = NewPromise();
            var task = callback(realm, thisArg, args);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    using var error = NewError(t.Exception?.GetBaseException() ?? t.Exception!);
                    deferred.Reject(error);
                }
                else if (t.IsCanceled)
                {
                    using var error = NewError(new OperationCanceledException("Task was canceled"));
                    deferred.Reject(error);
                }
                else
                {
                    using var result = t.Result ?? realm.Undefined();

                    deferred.Resolve(result);
                }
            }, TaskContinuationOptions.RunContinuationsAsynchronously);
            return deferred.Handle;
        }
    }

    /// <summary>
    /// Creates a new async JavaScript function that returns a Promise resolving to <c>undefined</c>.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="callback">The async .NET action to call when invoked from JavaScript.</param>
    /// <returns>A <see cref="JSValue"/> representing the async function.</returns>
    public JSValue NewFunctionAsync(string name, JSAsyncAction callback)
    {
        var options = new Dictionary<string, object> { { "name", name } };

        return _valueFactory.FromNativeValue((JSFunction)JavaScriptFunction, options);

        JSValue? JavaScriptFunction(Realm realm, JSValue thisArg, JSValue[] args)
        {
            var deferred = NewPromise();
            var task = callback(realm, thisArg, args);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    using var error = NewError(t.Exception?.GetBaseException() ?? t.Exception!);
                    deferred.Reject(error);
                }
                else if (t.IsCanceled)
                {
                    using var error = NewError(new OperationCanceledException("Task was canceled"));
                    deferred.Reject(error);
                }
                else
                {
                    using var result = realm.Undefined();
                    deferred.Resolve(result);
                }
            }, TaskContinuationOptions.RunContinuationsAsynchronously);

            return deferred.Handle;
        }
    }

    /// <summary>
    /// Creates a new JavaScript Promise with resolve and reject functions.
    /// </summary>
    /// <returns>A <see cref="JSPromise"/> that can be resolved or rejected from .NET code.</returns>
    /// <remarks>
    /// <para>
    /// Use this to create promises that will be settled from .NET asynchronous operations.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var promise = realm.NewPromise();
    /// 
    /// Task.Run(async () =>
    /// {
    ///     await Task.Delay(1000);
    ///     using var result = realm.NewString("Done!");
    ///     promise.Resolve(result);
    /// });
    /// 
    /// return promise.Handle; // Return to JavaScript
    /// </code>
    /// </para>
    /// </remarks>
    public JSPromise NewPromise()
    {
        using var resolveFuncsPtr = AllocatePointerArray(2);

        var promisePtr = Runtime.Registry.NewPromiseCapability(Pointer, resolveFuncsPtr);
        var resolvePtr = ReadPointerFromArray(resolveFuncsPtr, 0);
        var rejectPtr = ReadPointerFromArray(resolveFuncsPtr, 1);

        var promise = new JSValue(this, promisePtr);
        var resolveFunc = new JSValue(this, resolvePtr);
        var rejectFunc = new JSValue(this, rejectPtr);

        return new JSPromise(this, promise, resolveFunc, rejectFunc);
    }

    /// <summary>
    /// Converts a .NET value to a JavaScript value.
    /// </summary>
    /// <param name="value">The .NET value to convert.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <returns>A <see cref="JSValue"/> representing the converted value.</returns>
    /// <remarks>
    /// <para>
    /// Supported conversions:
    /// <list type="bullet">
    /// <item><c>null</c> → <c>null</c></item>
    /// <item><c>bool</c> → <c>true</c>/<c>false</c></item>
    /// <item>Numbers → JavaScript number</item>
    /// <item><c>string</c> → JavaScript string</item>
    /// <item>Arrays → JavaScript array</item>
    /// <item>Dictionaries → JavaScript object</item>
    /// <item>Delegates → JavaScript function</item>
    /// <item><see cref="JSValue"/> → returned as-is</item>
    /// </list>
    /// </para>
    /// </remarks>
    public JSValue NewValue(object? value, Dictionary<string, object>? options = null)
    {
        if (value is JSValue jsValue) return jsValue;
        return _valueFactory.FromNativeValue(value, options);
    }

    /// <summary>
    /// Converts a marshalled .NET value to a JavaScript value
    /// </summary>
    /// <param name="value">The .NET value to convert.</param>
    /// <typeparam name="TValue">The type implementing <see cref="IJSMarshalable"/></typeparam>
    /// <returns>A <see cref="JSValue"/> representing the converted value.</returns>
    public JSValue NewValue<TValue>(TValue value) where TValue : IJSMarshalable<TValue>
    {
        return value.ToJSValue(this);
    }

    /// <summary>
    /// Returns the JavaScript <c>undefined</c> value.
    /// </summary>
    /// <returns>A <see cref="JSValue"/> representing <c>undefined</c>.</returns>
    public JSValue Undefined()
    {
        return _valueFactory.CreateUndefined();
    }

    /// <summary>
    /// Returns the JavaScript <c>null</c> value (borrowed reference).
    /// </summary>
    /// <returns>A borrowed <see cref="JSValue"/> representing <c>null</c>.</returns>
    public JSValue Null()
    {
        return new JSValue(this, Runtime.Registry.GetNull(), ValueLifecycle.Borrowed);
    }

    /// <summary>
    /// Returns the JavaScript <c>true</c> value (borrowed reference).
    /// </summary>
    /// <returns>A borrowed <see cref="JSValue"/> representing <c>true</c>.</returns>
    public JSValue True()
    {
        return new JSValue(this, Runtime.Registry.GetTrue(), ValueLifecycle.Borrowed);
    }

    /// <summary>
    /// Returns the JavaScript <c>false</c> value (borrowed reference).
    /// </summary>
    /// <returns>A borrowed <see cref="JSValue"/> representing <c>false</c>.</returns>
    public JSValue False()
    {
        return new JSValue(this, Runtime.Registry.GetFalse(), ValueLifecycle.Borrowed);
    }

    #endregion

    #region Value References

    internal JSValue BorrowValue(int ptr)
    {
        return new JSValue(this, ptr, ValueLifecycle.Borrowed);
    }

    /// <summary>
    /// Creates a duplicate of a value handle.
    /// </summary>
    /// <param name="ptr">The value handle to duplicate.</param>
    /// <returns>A new <see cref="JSValue"/> with independent lifecycle.</returns>
    public JSValue DupValue(int ptr)
    {
        var duped = DupValuePointer(ptr);
        return new JSValue(this, duped);
    }

    #endregion

    #region Utility Wrappers

    internal bool IsEqual(int handleA, int handleB, EqualityOp op = EqualityOp.Strict)
    {
        var result = Runtime.Registry.IsEqual(Pointer, handleA, handleB, (int)op);
        if (result == -1) throw new InvalidOperationException("Equality comparison failed");
        return result != 0;
    }

    internal int GetLength(int handle)
    {
        return Runtime.Utils.GetLength(Pointer, handle);
    }

    #endregion

    #region Module Operations

    /// <summary>
    /// Gets the namespace object for an ES6 module.
    /// </summary>
    /// <param name="moduleValue">The module value.</param>
    /// <returns>A <see cref="JSValue"/> representing the module's namespace object.</returns>
    /// <exception cref="HakoException">Failed to get the module namespace.</exception>
    public JSValue GetModuleNamespace(JSValue moduleValue)
    {
        var resultPtr = Runtime.Registry.GetModuleNamespace(Pointer, moduleValue.GetHandle());

        var exceptionPtr = Runtime.Errors.GetLastErrorPointer(Pointer, resultPtr);
        if (exceptionPtr != 0)
        {
            var error = Runtime.Errors.GetExceptionDetails(Pointer, exceptionPtr);
            FreeValuePointer(resultPtr);
            FreeValuePointer(exceptionPtr);
            throw new HakoException("Unable to find module namespace", error);
        }

        return new JSValue(this, resultPtr);
    }

    /// <summary>
    /// Gets the name of a module.
    /// </summary>
    /// <param name="moduleHandle">The module handle.</param>
    /// <returns>The module name, or <c>null</c> if unavailable.</returns>
    public string? GetModuleName(int moduleHandle)
    {
        var namePtr = Runtime.Registry.GetModuleName(Pointer, moduleHandle);
        if (namePtr == 0) return null;
        var result = ReadString(namePtr);
        FreeCString(namePtr);
        return result;
    }

    #endregion

    #region JSON Operations

    /// <summary>
    /// Encodes a JavaScript value to QuickJS's binary JSON format (BJSON).
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>The BJSON-encoded byte array.</returns>
    /// <exception cref="HakoException">Encoding failed.</exception>
    /// <remarks>
    /// BJSON is QuickJS's compact binary format for JavaScript values. It preserves types
    /// better than regular JSON and supports circular references.
    /// </remarks>
    public byte[] BJSONEncode(JSValue value)
    {
        using var lengthPtr = AllocatePointerArray(1);

        var bufferPtr = Runtime.Registry.BJSON_Encode(Pointer, value.GetHandle(), lengthPtr);
        if (bufferPtr == 0)
        {
            var lastError = GetLastError();
            if (lastError != null) throw new HakoException("BJSON encoding failed", lastError);
            throw new HakoException("BJSON encoding failed");
        }

        try
        {
            var length = ReadPointer(lengthPtr);
            return CopyMemory(bufferPtr, length);
        }
        finally
        {
            FreeMemory(bufferPtr);
        }
    }

    /// <summary>
    /// Decodes a BJSON byte array to a JavaScript value.
    /// </summary>
    /// <param name="data">The BJSON data to decode.</param>
    /// <returns>The decoded <see cref="JSValue"/>.</returns>
    /// <exception cref="HakoException">Decoding failed.</exception>
    public JSValue BJSONDecode(byte[] data)
    {
        var bufferPtr = WriteBytes(data);
        try
        {
            var resultPtr = Runtime.Registry.BJSON_Decode(Pointer, bufferPtr, data.Length);
            var error = GetLastError(resultPtr);
            if (error != null) throw new HakoException("BJSON decoding failed", error);

            return new JSValue(this, resultPtr);
        }
        finally
        {
            FreeMemory(bufferPtr);
        }
    }

    /// <summary>
    /// Dumps a JavaScript value to a .NET object representation (for debugging).
    /// </summary>
    /// <param name="value">The value to dump.</param>
    /// <returns>A .NET object representing the value's structure.</returns>
    public string Dump(JSValue value)
    {
        var cstring = Runtime.Registry.Dump(Pointer, value.GetHandle());
        var result = ReadString(cstring);
        FreeCString(cstring);
        return result;
    }

    /// <summary>
    /// Parses a JSON string into a JavaScript value.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="filename">Optional filename for error messages.</param>
    /// <returns>A <see cref="JSValue"/> representing the parsed JSON.</returns>
    /// <exception cref="HakoException">JSON parsing failed.</exception>
    /// <remarks>
    /// Returns <c>undefined</c> for empty or whitespace-only strings.
    /// </remarks>
    public JSValue ParseJson(string json, string? filename = null)
    {
        json = json.Trim();
        if (string.IsNullOrEmpty(json)) return Undefined();
        using var contentPointer = AllocateString(json, out var length);
        using var filenamePointer = AllocateString(filename ?? "<eval>", out _);
        var result = Runtime.Registry.ParseJson(Pointer, contentPointer, length, filenamePointer);
        if (result == 0) return Undefined();

        var error = GetLastError(result);
        if (error != null) throw new HakoException("JSON parsing failed", error);

        return JSValue.FromHandle(this, result, ValueLifecycle.Owned);
    }

    #endregion
}