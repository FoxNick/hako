using System.Collections;
using HakoJS.Host;
using HakoJS.Lifetime;

namespace HakoJS.VM;

/// <summary>
/// Provides synchronous iteration over JavaScript iterable objects (arrays, sets, maps, generators, etc.).
/// </summary>
/// <remarks>
/// <para>
/// This class implements both <see cref="IEnumerable{T}"/> and <see cref="IEnumerator{T}"/>,
/// allowing JavaScript iterables to be used with C# foreach loops and LINQ.
/// </para>
/// <para>
/// The iterator calls the object's Symbol.iterator method and repeatedly invokes the <c>next()</c>
/// method until iteration is complete. Each yielded value is wrapped in a <see cref="DisposableResult{TSuccess, TFailure}"/>
/// that must be disposed by the caller.
/// </para>
/// <para>
/// Example:
/// <code>
/// using var array = realm.EvalCode("[1, 2, 3]").Unwrap();
/// using var iteratorResult = realm.GetIterator(array);
/// using var iterator = iteratorResult.Unwrap();
/// 
/// foreach (var itemResult in iterator)
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
/// The iterator implements the full JavaScript iteration protocol, including support for
/// <c>return()</c> and <c>throw()</c> methods for early termination and error handling.
/// </para>
/// </remarks>
public sealed class JSIterator : IDisposable, IEnumerable<DisposableResult<JSValue, JSValue>>,
    IEnumerator<DisposableResult<JSValue, JSValue>>
{
    private DisposableResult<JSValue, JSValue>? _current;
    private bool _disposed;
    private JSValue? _handle;
    private bool _isDone;
    private JSValue? _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="JSIterator"/> class.
    /// </summary>
    /// <param name="handle">The JavaScript iterator object.</param>
    /// <param name="context">The realm in which the iterator exists.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="handle"/> or <paramref name="context"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This constructor is internal. Use <see cref="Realm.GetIterator"/> or extension methods
    /// like <see cref="JSValueExtensions.Iterate"/> to create iterator instances.
    /// </remarks>
    internal JSIterator(JSValue handle, Realm context)
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
    private Realm Context { get; }

    /// <summary>
    /// Gets a value indicating whether the iterator handle is still valid.
    /// </summary>
    private bool Alive => _handle?.Alive ?? false;

    /// <summary>
    /// Disposes the iterator and all associated resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This releases the iterator handle, the cached <c>next</c> method, and the current iteration result.
    /// The iterator is marked as done, preventing further iteration.
    /// </para>
    /// <para>
    /// Disposing the iterator does NOT call the JavaScript <c>return()</c> method.
    /// If you need proper cleanup semantics, call <see cref="Return"/> before disposing.
    /// </para>
    /// </remarks>
    public void Dispose()
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
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>The iterator itself, as it implements <see cref="IEnumerator{T}"/>.</returns>
    /// <exception cref="ObjectDisposedException">The iterator has been disposed.</exception>
    public IEnumerator<DisposableResult<JSValue, JSValue>> GetEnumerator()
    {
        ThrowIfDisposed();
        return this;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
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
    /// <see cref="MoveNext"/> has not been called, or the iterator is past the end.
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

    object IEnumerator.Current => Current;

    /// <summary>
    /// Advances the iterator to the next element.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the iterator successfully advanced to the next element;
    /// <c>false</c> if the iterator has passed the end of the collection or an error occurred.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The iterator has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This method calls the JavaScript iterator's <c>next()</c> method and checks the result's
    /// <c>done</c> property. If <c>done</c> is true, the method returns false and disposes the iterator.
    /// </para>
    /// <para>
    /// The <c>next</c> method is lazily retrieved and cached on the first call to <see cref="MoveNext"/>.
    /// </para>
    /// <para>
    /// If an error occurs during iteration, the iterator is disposed and false is returned.
    /// Check <see cref="Current"/> for error details.
    /// </para>
    /// </remarks>
    public bool MoveNext()
    {
        ThrowIfDisposed();

        if (!Alive || _isDone) return false;

        // Lazily retrieve and cache the 'next' method
        _next ??= _handle!.GetProperty("next");

        var result = CallIteratorMethod(_next, null);

        if (result.IsDone) return false;

        _current = result.Value;
        return true;
    }

    /// <summary>
    /// Resets the iterator to its initial position.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown; JavaScript iterators cannot be reset.</exception>
    /// <remarks>
    /// JavaScript iterators are forward-only and cannot be reset. To iterate again,
    /// create a new iterator from the original iterable.
    /// </remarks>
    public void Reset()
    {
        throw new NotSupportedException("VMIterator does not support Reset");
    }

    /// <summary>
    /// Signals early termination of the iterator and optionally provides a return value.
    /// </summary>
    /// <param name="value">
    /// An optional value to pass to the iterator's <c>return()</c> method.
    /// If <c>null</c> and the iterator has no <c>return</c> method, the iterator is simply disposed.
    /// </param>
    /// <exception cref="ObjectDisposedException">The iterator has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This method calls the JavaScript iterator's <c>return()</c> method if it exists,
    /// allowing the iterator to perform cleanup (e.g., closing resources, releasing locks).
    /// </para>
    /// <para>
    /// After calling this method, the iterator is disposed and cannot be used further.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// using var iterator = realm.GetIterator(someIterable).Unwrap();
    /// 
    /// foreach (var item in iterator)
    /// {
    ///     if (shouldStop)
    ///     {
    ///         using var returnValue = realm.NewString("Early exit");
    ///         iterator.Return(returnValue);
    ///         break;
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public void Return(JSValue? value = null)
    {
        ThrowIfDisposed();

        if (!Alive) return;

        using var returnMethod = _handle!.GetProperty("return");
        if (returnMethod.IsUndefined() && value == null)
        {
            Dispose();
            return;
        }

        CallIteratorMethod(returnMethod, value);
        Dispose();
    }

    /// <summary>
    /// Signals an error to the iterator, allowing it to handle or propagate the exception.
    /// </summary>
    /// <param name="error">
    /// A .NET exception to convert to a JavaScript error. Either this or <paramref name="errorValue"/> must be provided.
    /// </param>
    /// <param name="errorValue">
    /// A JavaScript error value. Either this or <paramref name="error"/> must be provided.
    /// </param>
    /// <exception cref="ObjectDisposedException">The iterator has been disposed.</exception>
    /// <exception cref="ArgumentException">
    /// Both <paramref name="error"/> and <paramref name="errorValue"/> are <c>null</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method calls the JavaScript iterator's <c>throw()</c> method if it exists,
    /// allowing the iterator to handle the error (e.g., in a generator's catch block).
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
    /// using var iterator = realm.GetIterator(someGenerator).Unwrap();
    /// 
    /// try
    /// {
    ///     foreach (var item in iterator)
    ///     {
    ///         // Process item
    ///     }
    /// }
    /// catch (Exception ex)
    /// {
    ///     iterator.Throw(ex); // Send error to generator
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public void Throw(Exception? error = null, JSValue? errorValue = null)
    {
        ThrowIfDisposed();

        if (!Alive) return;

        if (error == null && errorValue == null)
            throw new ArgumentException("Either error or errorValue must be provided");

        JSValue? errorHandle = null;
        JSValue? throwMethod = null;

        try
        {
            errorHandle = errorValue ?? Context.NewError(error!);

            throwMethod = _handle!.GetProperty("throw");
            CallIteratorMethod(throwMethod, errorHandle);
        }
        finally
        {
            // Only dispose errorHandle if we created it (not passed in)
            if (errorValue == null && errorHandle?.Alive == true) errorHandle.Dispose();

            throwMethod?.Dispose();
            Dispose();
        }
    }

    private IteratorMethodResult CallIteratorMethod(JSValue method, JSValue? input)
    {
        // Call the method on the VM iterator
        var callResult = input != null
            ? Context.CallFunction(method, _handle!, input)
            : Context.CallFunction(method, _handle!);

        // If an error occurred, dispose the iterator and return the error
        if (callResult.TryGetFailure(out var error))
        {
            Dispose();
            return new IteratorMethodResult(callResult, true);
        }

        if (!callResult.TryGetSuccess(out var resultValue))
            throw new InvalidOperationException("Call result is in invalid state");

        // Check the 'done' property
        using var doneProperty = resultValue.GetProperty("done");
        using var doneBox = doneProperty.ToNativeValue<bool>();

        if (doneBox.Value)
        {
            // If done, dispose resources
            resultValue.Dispose();
            Dispose();
            return new IteratorMethodResult(null, true);
        }

        // Extract the 'value' property
        var value = resultValue.GetProperty("value");
        resultValue.Dispose();

        return new IteratorMethodResult(DisposableResult<JSValue, JSValue>.Success(value), false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JSIterator));
    }

    /// <summary>
    /// Represents the result of calling an iterator method (next, return, or throw).
    /// </summary>
    private class IteratorMethodResult(DisposableResult<JSValue, JSValue>? value, bool isDone)
    {
        /// <summary>
        /// Gets the iteration value, or <c>null</c> if the iteration is done or failed.
        /// </summary>
        public DisposableResult<JSValue, JSValue>? Value { get; } = value;

        /// <summary>
        /// Gets a value indicating whether the iteration is complete.
        /// </summary>
        public bool IsDone { get; } = isDone;
    }
}