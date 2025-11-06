using HakoJS.Host;

namespace HakoJS.VM;

/// <summary>
/// Represents a JavaScript Promise with methods to resolve or reject it from C# code.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps a JavaScript Promise along with its resolve and reject functions,
/// providing a convenient API for controlling promise settlement from the .NET side.
/// </para>
/// <para>
/// Use this class when you need to create a Promise in JavaScript that will be resolved
/// or rejected from C# code, such as when wrapping asynchronous .NET operations.
/// </para>
/// <para>
/// Example:
/// <code>
/// var promise = realm.NewPromise();
/// 
/// // Return the promise to JavaScript
/// global.SetProperty("myPromise", promise.Handle);
/// 
/// // Later, resolve it from C#
/// Task.Run(async () =>
/// {
///     await Task.Delay(1000);
///     using var result = realm.NewString("Success!");
///     promise.Resolve(result);
/// });
/// 
/// // JavaScript can now await the promise
/// // await myPromise; // resolves to "Success!" after 1 second
/// </code>
/// </para>
/// </remarks>
public sealed class JSPromise : IDisposable
{
    private readonly TaskCompletionSource<bool> _settledTcs;
    private bool _disposed;
    private JSValue? _handle;
    private JSValue? _rejectHandle;
    private JSValue? _resolveHandle;

    /// <summary>
    /// Initializes a new instance of the <see cref="JSPromise"/> class.
    /// </summary>
    /// <param name="context">The realm in which the promise exists.</param>
    /// <param name="promiseHandle">The JavaScript Promise object.</param>
    /// <param name="resolveHandle">The resolve function for the promise.</param>
    /// <param name="rejectHandle">The reject function for the promise.</param>
    /// <exception cref="ArgumentNullException">
    /// Any parameter is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This constructor is internal. Use <see cref="Realm.NewPromise"/> to create promise instances.
    /// </remarks>
    internal JSPromise(
        Realm context,
        JSValue promiseHandle,
        JSValue resolveHandle,
        JSValue rejectHandle)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _handle = promiseHandle ?? throw new ArgumentNullException(nameof(promiseHandle));
        _resolveHandle = resolveHandle ?? throw new ArgumentNullException(nameof(resolveHandle));
        _rejectHandle = rejectHandle ?? throw new ArgumentNullException(nameof(rejectHandle));
        Owner = context.Runtime;
        _settledTcs = new TaskCompletionSource<bool>();
    }

    /// <summary>
    /// Gets the runtime that owns this promise.
    /// </summary>
    public HakoRuntime Owner { get; }

    /// <summary>
    /// Gets the realm in which this promise exists.
    /// </summary>
    private Realm Context { get; }

    /// <summary>
    /// Gets the JavaScript Promise value that can be returned to JavaScript code.
    /// </summary>
    /// <value>
    /// The underlying <see cref="JSValue"/> representing the JavaScript Promise object.
    /// </value>
    /// <exception cref="ObjectDisposedException">The promise has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Use this property to pass the promise to JavaScript code, where it can be awaited
    /// or used with Promise methods like <c>.then()</c> and <c>.catch()</c>.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var promise = realm.NewPromise();
    /// global.SetProperty("asyncOperation", promise.Handle);
    /// 
    /// // JavaScript can now:
    /// // asyncOperation.then(result => console.log(result));
    /// </code>
    /// </para>
    /// </remarks>
    public JSValue Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle!;
        }
    }

    /// <summary>
    /// Gets a task that completes when the promise is settled (resolved or rejected).
    /// </summary>
    /// <value>
    /// A task that completes with <c>true</c> if the promise was successfully settled,
    /// or <c>false</c> if an error occurred during settlement.
    /// </value>
    /// <remarks>
    /// <para>
    /// Use this to await promise settlement from C# code. The task completes regardless
    /// of whether the promise was resolved or rejected.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var promise = realm.NewPromise();
    /// 
    /// Task.Run(async () =>
    /// {
    ///     await Task.Delay(1000);
    ///     promise.Resolve();
    /// });
    /// 
    /// await promise.Settled; // Waits until promise is resolved or rejected
    /// </code>
    /// </para>
    /// </remarks>
    public Task Settled => _settledTcs.Task;

    /// <summary>
    /// Gets a value indicating whether the promise and its resolver functions are still valid.
    /// </summary>
    /// <value>
    /// <c>true</c> if any of the internal handles are still alive; otherwise, <c>false</c>.
    /// </value>
    public bool Alive =>
        (_handle?.Alive ?? false) ||
        (_resolveHandle?.Alive ?? false) ||
        (_rejectHandle?.Alive ?? false);

    /// <summary>
    /// Disposes the promise and all associated resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This releases the promise handle and resolver functions. After disposal,
    /// attempting to resolve or reject the promise will have no effect.
    /// </para>
    /// <para>
    /// Note: Disposing the promise does not reject it in JavaScript. If you need to ensure
    /// the promise is rejected, call <see cref="Reject"/> before disposing.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        // Dispose the promise handle
        if (_handle?.Alive == true) _handle.Dispose();
        _handle = null;

        // Dispose the resolver handles
        DisposeResolvers();

        _disposed = true;
    }

    /// <summary>
    /// Resolves the promise with the specified value or <c>undefined</c> if no value is provided.
    /// </summary>
    /// <param name="value">
    /// The value to resolve the promise with, or <c>null</c> to resolve with <c>undefined</c>.
    /// </param>
    /// <exception cref="ObjectDisposedException">The promise has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Once resolved, the promise's state cannot be changed. Subsequent calls to <see cref="Resolve"/>
    /// or <see cref="Reject"/> will have no effect.
    /// </para>
    /// <para>
    /// The resolver functions are automatically disposed after the promise is settled,
    /// and the <see cref="Settled"/> task completes.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var promise = realm.NewPromise();
    /// 
    /// // Return promise to JavaScript
    /// someJsFunction.Invoke(promise.Handle);
    /// 
    /// // Resolve it later
    /// using var result = realm.NewString("Operation completed");
    /// promise.Resolve(result);
    /// </code>
    /// </para>
    /// </remarks>
    public void Resolve(JSValue? value = null)
    {
        ThrowIfDisposed();

        if (_resolveHandle?.Alive != true) return;

        var valueToUse = value ?? Context.Undefined();
        using var result = Context.CallFunction(_resolveHandle, Context.Undefined(), valueToUse);

        if (result.TryGetFailure(out var error))
        {
            // If calling resolve fails, dispose the error and resolvers
            error.Dispose();
            DisposeResolvers();
            _settledTcs.TrySetResult(false);
            return;
        }

        if (result.TryGetSuccess(out var success)) success.Dispose();

        DisposeResolvers();
        _settledTcs.TrySetResult(true);
    }

    /// <summary>
    /// Rejects the promise with the specified reason or <c>undefined</c> if no reason is provided.
    /// </summary>
    /// <param name="value">
    /// The rejection reason (typically an Error object), or <c>null</c> to reject with <c>undefined</c>.
    /// </param>
    /// <exception cref="ObjectDisposedException">The promise has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Once rejected, the promise's state cannot be changed. Subsequent calls to <see cref="Resolve"/>
    /// or <see cref="Reject"/> will have no effect.
    /// </para>
    /// <para>
    /// The resolver functions are automatically disposed after the promise is settled,
    /// and the <see cref="Settled"/> task completes.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var promise = realm.NewPromise();
    /// 
    /// try
    /// {
    ///     // Perform some operation
    ///     throw new InvalidOperationException("Something went wrong");
    /// }
    /// catch (Exception ex)
    /// {
    ///     using var error = realm.NewError(ex);
    ///     promise.Reject(error);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public void Reject(JSValue? value = null)
    {
        ThrowIfDisposed();

        if (_rejectHandle?.Alive != true) return;

        var valueToUse = value ?? Context.Undefined();
        using var result = Context.CallFunction(_rejectHandle, Context.Undefined(), valueToUse);

        if (result.TryGetFailure(out var error))
        {
            // If calling reject fails, dispose the error and resolvers
            error.Dispose();
            DisposeResolvers();
            _settledTcs.TrySetResult(false);
            return;
        }

        if (result.TryGetSuccess(out var success)) success.Dispose();

        DisposeResolvers();
        _settledTcs.TrySetResult(true);
    }

    private void DisposeResolvers()
    {
        if (_resolveHandle?.Alive == true) _resolveHandle.Dispose();
        _resolveHandle = null;

        if (_rejectHandle?.Alive == true) _rejectHandle.Dispose();
        _rejectHandle = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JSPromise));
    }
}