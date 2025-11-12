using HakoJS.Host;
using HakoJS.Lifetime;

namespace HakoJS.VM;

/// <summary>
/// Provides asynchronous iteration over JavaScript async iterable objects (async generators, async iterables, etc.).
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IAsyncEnumerable{T}"/> and <see cref="IAsyncDisposable"/>,
/// allowing JavaScript async iterables to be used with C# async foreach loops and async LINQ.
/// </para>
/// <para>
/// The iterator calls the object's Symbol.asyncIterator method and repeatedly invokes the <c>next()</c>
/// method, awaiting each returned Promise, until iteration is complete. Each yielded value is wrapped
/// in a <see cref="DisposableResult{TSuccess, TFailure}"/> that must be disposed by the caller.
/// </para>
/// <para>
/// Example:
/// <code>
/// using var generator = realm.EvalCode(@"
///     (async function*() {
///         yield 1;
///         yield 2;
///         yield 3;
///     })()
/// ").Unwrap();
/// 
/// var iteratorResult = realm.GetAsyncIterator(generator);
/// using var iterator = iteratorResult.Unwrap();
/// 
/// await foreach (var itemResult in iterator)
/// {
///     if (itemResult.TryGetSuccess(out var item))
///     {
///         using (item)
///         {
///             Console.WriteLine(item.AsNumber());
///         }
///     }
/// }
/// </code>
/// </para>
/// <para>
/// The iterator implements the full JavaScript async iteration protocol, including support for
/// <c>return()</c> and <c>throw()</c> methods for early termination and error handling.
/// </para>
/// </remarks>
public sealed class JSAsyncIterator : IAsyncDisposable, IAsyncEnumerable<DisposableResult<JSValue, JSValue>>
{
    private DisposableResult<JSValue, JSValue>? _current;
    private bool _disposed;
    private JSValue? _handle;
    private bool _isDone;
    private JSValue? _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="JSAsyncIterator"/> class.
    /// </summary>
    /// <param name="handle">The JavaScript async iterator object.</param>
    /// <param name="context">The realm in which the iterator exists.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="handle"/> or <paramref name="context"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This constructor is internal. Use <see cref="Realm.GetAsyncIterator"/> or extension methods
    /// like <see cref="JSValueExtensions.IterateAsync"/> to create async iterator instances.
    /// </remarks>
    internal JSAsyncIterator(JSValue handle, Realm context)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Owner = context.Runtime;
    }

    /// <summary>
    /// Gets the runtime that owns this iterator.
    /// </summary>
    public HakoRuntime Owner { get; }

    /// <summary>
    /// Gets the realm in which this iterator exists.
    /// </summary>
    public Realm Context { get; }

    /// <summary>
    /// Gets a value indicating whether the iterator handle is still valid.
    /// </summary>
    public bool Alive => _handle?.Alive ?? false;

    /// <summary>
    /// Asynchronously disposes the iterator and all associated resources.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This releases the iterator handle, the cached <c>next</c> method, and the current iteration result.
    /// The iterator is marked as done, preventing further iteration.
    /// </para>
    /// <para>
    /// Disposing the iterator does NOT call the JavaScript <c>return()</c> method.
    /// If you need proper cleanup semantics, call <see cref="ReturnAsync"/> before disposing.
    /// </para>
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _isDone = true;

        // Dispose current iteration result if it exists
        _current?.Dispose();
        _current = null;

        // Dispose the 'next' method reference
        if (_next?.Alive == true) _next.Dispose();
        _next = null;

        // Dispose the iterator handle
        if (_handle?.Alive == true) _handle.Dispose();
        _handle = null;

        _disposed = true;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Returns an async enumerator that iterates through the collection.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    /// <returns>An async enumerator for the iterator.</returns>
    /// <exception cref="ObjectDisposedException">The iterator has been disposed.</exception>
    /// <remarks>
    /// The returned enumerator checks for cancellation before each call to <see cref="MoveNextAsync"/>.
    /// </remarks>
    public IAsyncEnumerator<DisposableResult<JSValue, JSValue>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return new AsyncIteratorEnumerator(this, cancellationToken);
    }

    /// <summary>
    /// Gets the current iteration result.
    /// </summary>
    /// <value>
    /// A <see cref="DisposableResult{TSuccess, TFailure}"/> containing either the iteration value
    /// or an error if the iteration failed.
    /// </value>
    /// <exception cref="ObjectDisposedException">The iterator has been disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="MoveNextAsync"/> has not been called, or the iterator is past the end.
    /// </exception>
    public DisposableResult<JSValue, JSValue> Current
    {
        get
        {
            ThrowIfDisposed();
            if (_current == null) throw new InvalidOperationException("No current value");
            return _current;
        }
    }

    /// <summary>
    /// Asynchronously advances the iterator to the next element.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation and contains <c>true</c> if the iterator
    /// successfully advanced to the next element; <c>false</c> if the iterator has passed the end
    /// of the collection or an error occurred.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The iterator has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This method calls the JavaScript async iterator's <c>next()</c> method, awaits the returned Promise,
    /// and checks the result's <c>done</c> property. If <c>done</c> is true, the method returns false
    /// and disposes the iterator.
    /// </para>
    /// <para>
    /// The <c>next</c> method is lazily retrieved and cached on the first call to <see cref="MoveNextAsync"/>.
    /// </para>
    /// <para>
    /// If an error occurs during iteration (either from calling <c>next()</c> or from the Promise rejection),
    /// the iterator is disposed and false is returned. Check <see cref="Current"/> for error details.
    /// </para>
    /// </remarks>
    public async Task<bool> MoveNextAsync()
    {
        ThrowIfDisposed();

        if (!Alive || _isDone) return false;

        // Lazily retrieve and cache the 'next' method
        if (_next == null) _next = _handle!.GetProperty("next");

        var result = await CallAsyncIteratorMethodAsync(_next, null).ConfigureAwait(false);

        if (result.IsDone) return false;

        _current = result.Value;
        return true;
    }

    /// <summary>
    /// Asynchronously signals early termination of the iterator and optionally provides a return value.
    /// </summary>
    /// <param name="value">
    /// An optional value to pass to the iterator's <c>return()</c> method.
    /// If <c>null</c> and the iterator has no <c>return</c> method, the iterator is simply disposed.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">The iterator has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This method calls the JavaScript async iterator's <c>return()</c> method if it exists,
    /// allowing the iterator to perform async cleanup (e.g., closing resources, releasing locks).
    /// </para>
    /// <para>
    /// After calling this method, the iterator is disposed and cannot be used further.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// await using var iterator = realm.GetAsyncIterator(someAsyncIterable).Unwrap();
    /// 
    /// await foreach (var item in iterator)
    /// {
    ///     if (shouldStop)
    ///     {
    ///         using var returnValue = realm.NewString("Early exit");
    ///         await iterator.ReturnAsync(returnValue);
    ///         break;
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public async Task ReturnAsync(JSValue? value = null)
    {
        ThrowIfDisposed();

        if (!Alive) return;

        using var returnMethod = _handle!.GetProperty("return");
        if (returnMethod.IsUndefined() && value == null)
        {
            await DisposeAsync().ConfigureAwait(false);
            return;
        }

        await CallAsyncIteratorMethodAsync(returnMethod, value).ConfigureAwait(false);
        await DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously signals an error to the iterator, allowing it to handle or propagate the exception.
    /// </summary>
    /// <param name="error">
    /// A .NET exception to convert to a JavaScript error. Either this or <paramref name="errorValue"/> must be provided.
    /// </param>
    /// <param name="errorValue">
    /// A JavaScript error value. Either this or <paramref name="error"/> must be provided.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">The iterator has been disposed.</exception>
    /// <exception cref="ArgumentException">
    /// Both <paramref name="error"/> and <paramref name="errorValue"/> are <c>null</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method calls the JavaScript async iterator's <c>throw()</c> method if it exists,
    /// allowing the iterator to handle the error asynchronously (e.g., in an async generator's catch block).
    /// </para>
    /// <para>
    /// After calling this method, the iterator is disposed and cannot be used further.
    /// </para>
    /// <para>
    /// If the iterator doesn't have a <c>throw</c> method, the error is ignored and
    /// the iterator is simply disposed.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// await using var iterator = realm.GetAsyncIterator(someAsyncGenerator).Unwrap();
    /// 
    /// try
    /// {
    ///     await foreach (var item in iterator)
    ///     {
    ///         // Process item
    ///     }
    /// }
    /// catch (Exception ex)
    /// {
    ///     await iterator.ThrowAsync(ex); // Send error to async generator
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public async Task ThrowAsync(Exception? error = null, JSValue? errorValue = null)
    {
        ThrowIfDisposed();

        if (!Alive) return;

        if (error == null && errorValue == null)
            throw new ArgumentException("Either error or errorValue must be provided");

        JSValue? errorHandle = null;
        JSValue? throwMethod = null;

        try
        {
            if (errorValue != null)
                errorHandle = errorValue;
            else
                errorHandle = Context.NewError(error!);

            throwMethod = _handle!.GetProperty("throw");
            await CallAsyncIteratorMethodAsync(throwMethod, errorHandle).ConfigureAwait(false);
        }
        finally
        {
            // Only dispose errorHandle if we created it (not passed in)
            if (errorValue == null && errorHandle?.Alive == true) errorHandle.Dispose();

            throwMethod?.Dispose();
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<AsyncIteratorMethodResult> CallAsyncIteratorMethodAsync(JSValue method, JSValue? input)
    {
        // Call the method on the VM iterator
        var callResult = input != null
            ? Context.CallFunction(method, _handle!, input)
            : Context.CallFunction(method, _handle!);

        // If an error occurred, dispose the iterator and return the error
        if (callResult.TryGetFailure(out var error))
        {
            await DisposeAsync().ConfigureAwait(false);
            return new AsyncIteratorMethodResult(callResult, true);
        }

        if (!callResult.TryGetSuccess(out var resultPromise))
            throw new InvalidOperationException("Call result is in invalid state");

        // Await the promise result
        DisposableResult<JSValue, JSValue> promiseResult;
        try
        {
            promiseResult = await Context.ResolvePromise(resultPromise).ConfigureAwait(false);
        }
        finally
        {
            resultPromise.Dispose();
        }

        // If promise rejected, dispose the iterator and return the error
        if (promiseResult.TryGetFailure(out var promiseError))
        {
            await DisposeAsync().ConfigureAwait(false);
            return new AsyncIteratorMethodResult(promiseResult, true);
        }

        if (!promiseResult.TryGetSuccess(out var resultValue))
            throw new InvalidOperationException("Promise result is in invalid state");

        // Check the 'done' property
        using var doneProperty = resultValue.GetProperty("done");
        using var doneBox = doneProperty.ToNativeValue<bool>();

        if (doneBox.Value)
        {
            // If done, dispose resources
            resultValue.Dispose();
            await DisposeAsync().ConfigureAwait(false);
            return new AsyncIteratorMethodResult(null, true);
        }

        // Extract the 'value' property
        var value = resultValue.GetProperty("value");
        resultValue.Dispose();

        return new AsyncIteratorMethodResult(DisposableResult<JSValue, JSValue>.Success(value), false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JSAsyncIterator));
    }

    /// <summary>
    /// Represents the result of calling an async iterator method (next, return, or throw).
    /// </summary>
    private class AsyncIteratorMethodResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncIteratorMethodResult"/> class.
        /// </summary>
        /// <param name="value">The iteration value, or <c>null</c> if the iteration is done or failed.</param>
        /// <param name="isDone">A value indicating whether the iteration is complete.</param>
        public AsyncIteratorMethodResult(DisposableResult<JSValue, JSValue>? value, bool isDone)
        {
            Value = value;
            IsDone = isDone;
        }

        /// <summary>
        /// Gets the iteration value, or <c>null</c> if the iteration is done or failed.
        /// </summary>
        public DisposableResult<JSValue, JSValue>? Value { get; }

        /// <summary>
        /// Gets a value indicating whether the iteration is complete.
        /// </summary>
        public bool IsDone { get; }
    }

    /// <summary>
    /// Provides an async enumerator wrapper that supports cancellation.
    /// </summary>
    private class AsyncIteratorEnumerator : IAsyncEnumerator<DisposableResult<JSValue, JSValue>>
    {
        private readonly JSAsyncIterator _iterator;
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncIteratorEnumerator"/> class.
        /// </summary>
        /// <param name="iterator">The async iterator to enumerate.</param>
        /// <param name="cancellationToken">A token to cancel the enumeration.</param>
        public AsyncIteratorEnumerator(JSAsyncIterator iterator, CancellationToken cancellationToken)
        {
            _iterator = iterator;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the current iteration result.
        /// </summary>
        public DisposableResult<JSValue, JSValue> Current => _iterator.Current;

        /// <summary>
        /// Asynchronously advances the enumerator to the next element.
        /// </summary>
        /// <returns>
        /// A task containing <c>true</c> if the enumerator successfully advanced; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        public async ValueTask<bool> MoveNextAsync()
        {
            _cancellationToken.ThrowIfCancellationRequested();
            return await _iterator.MoveNextAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes the enumerator asynchronously.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        public ValueTask DisposeAsync()
        {
            return _iterator.DisposeAsync();
        }
    }
}