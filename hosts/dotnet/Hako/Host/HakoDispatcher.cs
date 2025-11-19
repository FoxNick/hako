namespace HakoJS.Host;

using System.Runtime.ExceptionServices;

/// <summary>
/// Provides a thread-safe dispatcher for executing operations on the HakoJS event loop thread.
/// </summary>
public sealed class HakoDispatcher
{
    private volatile HakoEventLoop? _eventLoop;
    private volatile bool _isOrphaned;

    internal HakoDispatcher()
    {
    }

    private HakoEventLoop EventLoop => _eventLoop
                                       ?? throw new InvalidOperationException(
                                           "Hako has not been initialized. Call Hako.Initialize() first.");

    internal void Initialize(HakoEventLoop eventLoop)
    {
        _eventLoop = eventLoop ?? throw new ArgumentNullException(nameof(eventLoop));
    }

    internal void Reset()
    {
        _eventLoop = null;
        _isOrphaned = false;
    }

    /// <summary>
    /// Sets the dispatcher into orphaned mode where operations execute directly on the calling thread
    /// instead of being marshalled to the event loop.
    /// </summary>
    internal void SetOrphaned()
    {
        _isOrphaned = true;
    }

    /// <summary>
    /// Determines whether the current thread is the HakoJS event loop thread.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the current thread is the event loop thread; otherwise, <c>false</c>.
    /// Returns <c>false</c> if Hako has not been initialized.
    /// In orphaned mode, always returns <c>true</c>.
    /// </returns>
    public bool CheckAccess()
    {
        if (_isOrphaned)
            return true;

        var loop = _eventLoop;
        return loop?.CheckAccess() ?? false;
    }

    /// <summary>
    /// Verifies that the current thread is the HakoJS event loop thread and throws an exception if it is not.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The current thread is not the event loop thread or Hako has not been initialized.
    /// </exception>
    public void VerifyAccess()
    {
        if (_isOrphaned)
            return;

        if (!CheckAccess())
            throw new InvalidOperationException(
                "This operation must be called on the HakoJS event loop thread.");
    }

    /// <summary>
    /// Synchronously invokes an action on the event loop thread.
    /// If called from the event loop thread, executes immediately; otherwise, blocks until execution completes.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Hako has not been initialized or the event loop is shutting down.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public void Invoke(Action action, CancellationToken cancellationToken = default)
    {
        if (_isOrphaned)
        {
            ArgumentNullException.ThrowIfNull(action);

            cancellationToken.ThrowIfCancellationRequested();
            action();
            return;
        }

        try
        {
            EventLoop.Invoke(action, cancellationToken);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }
    }

    /// <summary>
    /// Synchronously invokes a function on the event loop thread and returns its result.
    /// If called from the event loop thread, executes immediately; otherwise, blocks until execution completes.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The function to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The result of the function invocation.</returns>
    /// <exception cref="InvalidOperationException">Hako has not been initialized or the event loop is shutting down.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public T Invoke<T>(Func<T> func, CancellationToken cancellationToken = default)
    {
        if (_isOrphaned)
        {
            ArgumentNullException.ThrowIfNull(func);

            cancellationToken.ThrowIfCancellationRequested();
            return func();
        }

        try
        {
            return EventLoop.Invoke(func, cancellationToken);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
            throw; // This line is unreachable but required for compilation
        }
    }

    /// <summary>
    /// Asynchronously invokes an action on the event loop thread.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Hako has not been initialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The returned task completes when the action finishes execution on the event loop thread.
    /// If the action throws an exception, the task will be faulted with that exception.
    /// </remarks>
    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        if (_isOrphaned)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        return EventLoop.InvokeAsync(action, cancellationToken);
    }

    /// <summary>
    /// Asynchronously invokes a function on the event loop thread and returns its result.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The function to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and contains the result.</returns>
    /// <exception cref="InvalidOperationException">Hako has not been initialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The returned task completes when the function finishes execution on the event loop thread.
    /// If the function throws an exception, the task will be faulted with that exception.
    /// </remarks>
    public Task<T> InvokeAsync<T>(Func<T> func, CancellationToken cancellationToken = default)
    {
        if (_isOrphaned)
        {
            ArgumentNullException.ThrowIfNull(func);

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<T>(cancellationToken);

            try
            {
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        return EventLoop.InvokeAsync(func, cancellationToken);
    }

    /// <summary>
    /// Asynchronously invokes an asynchronous action on the event loop thread.
    /// </summary>
    /// <param name="asyncAction">The asynchronous action to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Hako has not been initialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="asyncAction"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The asynchronous action is invoked on the event loop thread, and the returned task completes
    /// when the action's task completes. This allows async/await operations to be marshalled to the event loop thread.
    /// </remarks>
    public Task InvokeAsync(Func<Task> asyncAction, CancellationToken cancellationToken = default)
    {
        if (_isOrphaned)
        {
            ArgumentNullException.ThrowIfNull(asyncAction);

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            try
            {
                return asyncAction();
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        return EventLoop.InvokeAsync(asyncAction, cancellationToken);
    }

    /// <summary>
    /// Asynchronously invokes an asynchronous function on the event loop thread and returns its result.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="asyncFunc">The asynchronous function to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and contains the result.</returns>
    /// <exception cref="InvalidOperationException">Hako has not been initialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="asyncFunc"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The asynchronous function is invoked on the event loop thread, and the returned task completes
    /// when the function's task completes. This allows async/await operations to be marshalled to the event loop thread.
    /// </remarks>
    public Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunc, CancellationToken cancellationToken = default)
    {
        if (_isOrphaned)
        {
            ArgumentNullException.ThrowIfNull(asyncFunc);

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<T>(cancellationToken);

            try
            {
                return asyncFunc();
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        return EventLoop.InvokeAsync(asyncFunc, cancellationToken);
    }

    /// <summary>
    /// Posts an action to be executed on the event loop thread without waiting for completion.
    /// This is a fire-and-forget operation.
    /// </summary>
    /// <param name="action">The action to post.</param>
    /// <exception cref="InvalidOperationException">Hako has not been initialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Unlike <see cref="InvokeAsync(Action, CancellationToken)"/>, this method does not return a task,
    /// and exceptions thrown by the action will be handled by the event loop's unhandled exception handler.
    /// </remarks>
    public void Post(Action action)
    {
        if (_isOrphaned)
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                action();
            }
            catch
            {
                // In orphaned mode, swallow exceptions for fire-and-forget operations
                // to match the behavior of Post on the event loop
            }
            return;
        }

        EventLoop.Post(action);
    }
    
    /// <summary>
    /// Posts an asynchronous action to be executed on the event loop thread without waiting for completion.
    /// This is a fire-and-forget operation.
    /// </summary>
    /// <param name="asyncAction">The asynchronous action to post.</param>
    /// <exception cref="InvalidOperationException">Hako has not been initialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="asyncAction"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Unlike <see cref="InvokeAsync(Func{Task}, CancellationToken)"/>, this method does not return a task,
    /// and exceptions thrown by the action will be handled by the event loop's unhandled exception handler.
    /// </remarks>
    public void Post(Func<Task> asyncAction)
    {
        if (_isOrphaned)
        {
            ArgumentNullException.ThrowIfNull(asyncAction);

            try
            {
                _ = asyncAction();
            }
            catch
            {
                // In orphaned mode, swallow exceptions for fire-and-forget operations
                // to match the behavior of Post on the event loop
            }
            return;
        }

        EventLoop.Post(asyncAction);
    }

    /// <summary>
    /// Yields control back to the event loop, allowing it to process
    /// pending JavaScript jobs (microtasks) and timers before resuming execution.
    /// Must be called from the event loop thread.
    /// </summary>
    /// <returns>An awaitable that resumes execution on the event loop thread after processing pending work.</returns>
    /// <exception cref="InvalidOperationException">
    /// Hako has not been initialized or the method was not called from the event loop thread.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is useful when performing long-running operations on the event loop thread
    /// that need to periodically allow JavaScript promises and timers to execute.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// await Hako.Dispatcher.InvokeAsync(async () =>
    /// {
    ///     for (int i = 0; i &lt; 1000; i++)
    ///     {
    ///         // Do some work
    ///         ProcessItem(i);
    ///         
    ///         // Every 100 items, yield to allow JS to run
    ///         if (i % 100 == 0)
    ///             await Hako.Dispatcher.Yield();
    ///     }
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public EventLoopYieldAwaitable Yield()
    {
        if (_isOrphaned)
            throw new InvalidOperationException(
                "Cannot yield when the dispatcher is in orphaned mode.");

        VerifyAccess();
        return new EventLoopYieldAwaitable(EventLoop);
    }
}