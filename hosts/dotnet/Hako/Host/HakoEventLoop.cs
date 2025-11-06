using System.Diagnostics;
using System.Threading.Channels;

namespace HakoJS.Host;

/// <summary>
/// Provides a dedicated event loop thread for executing JavaScript runtime operations.
/// </summary>
internal sealed class HakoEventLoop : IDisposable
{
    private readonly Thread _eventLoopThread;
    private readonly Lock _runtimeLock = new();
    private readonly TaskCompletionSource _shutdownComplete;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly CancellationTokenSource _linkedCts;
    private readonly Channel<IWorkItem> _workQueue;
    private volatile bool _disposed;
    private HakoRuntime? _runtime;

    /// <summary>
    /// Initializes a new instance of the <see cref="HakoEventLoop"/> class.
    /// </summary>
    /// <param name="runtime">The optional runtime to associate with this event loop.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the event loop externally.</param>
    internal HakoEventLoop(HakoRuntime? runtime = null, CancellationToken cancellationToken = default)
    {
        _runtime = runtime;
        _workQueue = Channel.CreateUnbounded<IWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _shutdownCts = new CancellationTokenSource();
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
        _shutdownComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _eventLoopThread = new Thread(RunEventLoop)
        {
            Name = "HakoJS-EventLoop",
            IsBackground = false,
            Priority = ThreadPriority.Normal
        };
        _eventLoopThread.Start();
    }

    /// <summary>
    /// Gets a value indicating whether the event loop is currently running.
    /// </summary>
    public bool IsRunning => !_linkedCts.Token.IsCancellationRequested && !_disposed;

    /// <summary>
    /// Gets the managed thread ID of the event loop thread.
    /// </summary>
    public int ThreadId => _eventLoopThread.ManagedThreadId;

    /// <summary>
    /// Disposes the event loop and releases all associated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownCts.Cancel();

        try
        {
            _workQueue.Writer.Complete();
        }
        catch (ChannelClosedException)
        {
        }

        if (!_eventLoopThread.Join(TimeSpan.FromSeconds(5)))
            Console.Error.WriteLine("[HakoEventLoop] Event loop thread did not terminate within timeout");

        _linkedCts.Dispose();
        _shutdownCts.Dispose();
        Hako.NotifyEventLoopDisposed(this);
    }

    /// <summary>
    /// Occurs when an unhandled exception is thrown on the event loop thread.
    /// </summary>
    public event EventHandler<UnhandledExceptionEventArgs>? UnhandledException;

    /// <summary>
    /// Determines whether the current thread is the event loop thread.
    /// </summary>
    /// <returns><c>true</c> if the current thread is the event loop thread; otherwise, <c>false</c>.</returns>
    public bool CheckAccess() => Thread.CurrentThread.ManagedThreadId == ThreadId;

    /// <summary>
    /// Verifies that the current thread is the event loop thread and throws an exception if it is not.
    /// </summary>
    /// <exception cref="InvalidOperationException">The current thread is not the event loop thread.</exception>
    public void VerifyAccess()
    {
        if (!CheckAccess())
            throw new InvalidOperationException(
                "This operation must be called on the HakoJS event loop thread.");
    }

    internal void SetRuntime(HakoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        using (_runtimeLock.EnterScope())
        {
            if (_runtime != null)
                throw new InvalidOperationException("Runtime has already been set for this event loop.");

            _runtime = runtime;
        }
    }

    /// <summary>
    /// Synchronously invokes an action on the event loop thread.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <exception cref="ObjectDisposedException">The event loop has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The event loop is shutting down.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public void Invoke(Action action, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(action);

        if (CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
            return;
        }
        
        var workItem = new SyncWorkItem(action);
        if (!_workQueue.Writer.TryWrite(workItem))
            throw new InvalidOperationException("Event loop is shutting down");

        workItem.Wait(cancellationToken);
    }

    /// <summary>
    /// Synchronously invokes a function on the event loop thread and returns its result.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The function to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The result of the function invocation.</returns>
    /// <exception cref="ObjectDisposedException">The event loop has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The event loop is shutting down.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public T Invoke<T>(Func<T> func, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(func);

        if (CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            return func();
        }

        var workItem = new SyncWorkItem<T>(func);
        if (!_workQueue.Writer.TryWrite(workItem))
            throw new InvalidOperationException("Event loop is shutting down");

        return workItem.Wait(cancellationToken);
    }

    /// <summary>
    /// Asynchronously invokes an action on the event loop thread.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">The event loop has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(action);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workItem = new AsyncWorkItem(action, tcs);

        if (!_workQueue.Writer.TryWrite(workItem))
        {
            tcs.SetException(new InvalidOperationException("Event loop is shutting down"));
            return tcs.Task;
        }

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        return tcs.Task;
    }

    /// <summary>
    /// Asynchronously invokes a function on the event loop thread and returns its result.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The function to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and contains the result.</returns>
    /// <exception cref="ObjectDisposedException">The event loop has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <c>null</c>.</exception>
    public Task<T> InvokeAsync<T>(Func<T> func, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(func);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workItem = new AsyncWorkItem<T>(func, tcs);

        if (!_workQueue.Writer.TryWrite(workItem))
        {
            tcs.SetException(new InvalidOperationException("Event loop is shutting down"));
            return tcs.Task;
        }

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        return tcs.Task;
    }

    /// <summary>
    /// Asynchronously invokes an asynchronous function on the event loop thread.
    /// </summary>
    /// <param name="asyncFunc">The asynchronous function to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">The event loop has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="asyncFunc"/> is <c>null</c>.</exception>
    public Task InvokeAsync(Func<Task> asyncFunc, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(asyncFunc);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workItem = new AsyncTaskWorkItem(asyncFunc, tcs);

        if (!_workQueue.Writer.TryWrite(workItem))
        {
            tcs.SetException(new InvalidOperationException("Event loop is shutting down"));
            return tcs.Task;
        }

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        return tcs.Task;
    }

    /// <summary>
    /// Asynchronously invokes an asynchronous function on the event loop thread and returns its result.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="asyncFunc">The asynchronous function to invoke.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and contains the result.</returns>
    /// <exception cref="ObjectDisposedException">The event loop has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="asyncFunc"/> is <c>null</c>.</exception>
    public Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunc, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(asyncFunc);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workItem = new AsyncTaskWorkItem<T>(asyncFunc, tcs);

        if (!_workQueue.Writer.TryWrite(workItem))
        {
            tcs.SetException(new InvalidOperationException("Event loop is shutting down"));
            return tcs.Task;
        }

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        return tcs.Task;
    }

    /// <summary>
    /// Posts an action to be executed on the event loop thread without waiting for completion.
    /// </summary>
    /// <param name="action">The action to post.</param>
    public void Post(Action action) => _ = InvokeAsync(action);

    /// <summary>
    /// Posts an asynchronous action to be executed on the event loop thread without waiting for completion.
    /// </summary>
    /// <param name="asyncAction">The asynchronous action to post.</param>
    /// <exception cref="ObjectDisposedException">The event loop has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="asyncAction"/> is <c>null</c>.</exception>
    public void Post(Func<Task> asyncAction)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(asyncAction);

        var workItem = new AsyncTaskWorkItem(asyncAction, 
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        
        _workQueue.Writer.TryWrite(workItem);
    }

    internal void PostYield(Action continuation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(continuation);

        _workQueue.Writer.TryWrite(new YieldWorkItem(continuation));
    }

    /// <summary>
    /// Requests that the event loop stop processing work items.
    /// </summary>
    /// <returns>A task that completes when the event loop has stopped.</returns>
    public Task StopAsync()
    {
        if (!_disposed)
        {
            _shutdownCts.Cancel();
            _workQueue.Writer.Complete();
        }

        return _shutdownComplete.Task;
    }

    /// <summary>
    /// Waits for the event loop to exit.
    /// </summary>
    /// <returns>A task that completes when the event loop has exited.</returns>
    public Task WaitForExitAsync() => _shutdownComplete.Task;

    private void RunEventLoop()
    {
        SynchronizationContext.SetSynchronizationContext(new HakoSynchronizationContext(this));

        try
        {
            while (!_linkedCts.Token.IsCancellationRequested)
            {
                // Process all work items from the queue
                var (hasWork, nextTimerMs) = ProcessWorkQueue();

                // Always flush microtasks after processing work items
                // If we didn't get a timer update from work items, check timers now
                if (!nextTimerMs.HasValue)
                {
                    FlushMicrotasks();
                    nextTimerMs = ProcessMacrotasks();
                }

                // If we did work or have timers ready to fire, continue immediately
                if (hasWork || nextTimerMs == 0)
                    continue;

                // Wait for either new work or the next timer to be due
                WaitForWork(nextTimerMs.Value);
            }

            _shutdownComplete.SetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HakoEventLoop] Event loop terminated with exception: {ex}");
            OnUnhandledException(ex);
            _shutdownComplete.SetException(ex);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(null);
        }
    }

    private (bool hasWork, int? nextTimerMs) ProcessWorkQueue()
    {
        var hasWork = false;
        int? nextTimerMs = null;

        while (_workQueue.Reader.TryRead(out var workItem))
        {
            if (workItem.RequiresTasks)
            {
                FlushMicrotasks();
                nextTimerMs = ProcessMacrotasks();
            }

            workItem.Execute();
            hasWork = true;
        }

        return (hasWork, nextTimerMs);
    }

    /// <summary>
    /// Flushes all pending microtasks (promise callbacks, queueMicrotask, etc.).
    /// </summary>
    /// <remarks>
    /// Microtasks are executed completely before any macrotasks. This includes:
    /// - Promise then/catch/finally callbacks
    /// - queueMicrotask() callbacks
    /// - MutationObserver callbacks (if supported)
    /// </remarks>
    private void FlushMicrotasks()
    {
        var runtime = GetRuntime();
        if (runtime == null)
            return;

        if (!runtime.IsMicrotaskPending())
            return;

        try
        {
            var result = runtime.ExecuteMicrotasks();
            result.EnsureSuccess();
        }
        catch (Exception ex)
        {
            OnUnhandledException(ex);
        }
    }

    /// <summary>
    /// Processes due macrotasks (timers: setTimeout/setInterval).
    /// </summary>
    /// <returns>The time in milliseconds until the next timer is due, or -1 if no timers are pending.</returns>
    /// <remarks>
    /// Macrotasks are processed after all microtasks have been flushed. This includes:
    /// - setTimeout callbacks
    /// - setInterval callbacks
    /// </remarks>
    private int ProcessMacrotasks()
    {
        var runtime = GetRuntime();
        if (runtime == null)
            return -1;

        try
        {
            return runtime.ExecuteTimers();
        }
        catch (Exception ex)
        {
            OnUnhandledException(ex);
            return -1;
        }
    }

    private void WaitForWork(int nextTimerMs)
    {
        var waitTime = nextTimerMs <= -1 
            ? Timeout.InfiniteTimeSpan 
            : TimeSpan.FromMilliseconds(nextTimerMs);

        try
        {
            _workQueue.Reader.WaitToReadAsync(_linkedCts.Token)
                .AsTask()
                .Wait(waitTime, _linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (TimeoutException)
        {
        }
    }

    private HakoRuntime? GetRuntime()
    {
        using (_runtimeLock.EnterScope())
        {
            return _runtime;
        }
    }

    private void OnUnhandledException(Exception exception)
    {
        try
        {
            UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(exception, false));
        }
        catch
        {
        }
    }

    #region Work Item Implementations

    private interface IWorkItem
    {
        void Execute();
        bool RequiresTasks { get; }
    }

    private sealed class SyncWorkItem(Action action) : IWorkItem
    {
        private readonly ManualResetEventSlim _completionEvent = new(false);
        private Exception? _exception;

        public bool RequiresTasks => false;

        public void Execute()
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
            finally
            {
                _completionEvent.Set();
            }
        }

        public void Wait(CancellationToken cancellationToken = default)
        {
            try
            {
                _completionEvent.Wait(cancellationToken);

                if (_exception != null)
                    throw new AggregateException("Work item execution failed", _exception);
            }
            finally
            {
                _completionEvent.Dispose();
            }
        }
    }

    private sealed class SyncWorkItem<T>(Func<T> func) : IWorkItem
    {
        private readonly ManualResetEventSlim _completionEvent = new(false);
        private Exception? _exception;
        private T? _result;

        public bool RequiresTasks => false;

        public void Execute()
        {
            try
            {
                _result = func();
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
            finally
            {
                _completionEvent.Set();
            }
        }

        public T Wait(CancellationToken cancellationToken = default)
        {
            try
            {
                _completionEvent.Wait(cancellationToken);

                if (_exception != null)
                    throw new AggregateException("Work item execution failed", _exception);

                return _result!;
            }
            finally
            {
                _completionEvent.Dispose();
            }
        }
    }

    private sealed class AsyncWorkItem(Action action, TaskCompletionSource tcs) : IWorkItem
    {
        public bool RequiresTasks => false;

        public void Execute()
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }
    }

    private sealed class AsyncWorkItem<T>(Func<T> func, TaskCompletionSource<T> tcs) : IWorkItem
    {
        public bool RequiresTasks => false;

        public void Execute()
        {
            try
            {
                var result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }
    }

    private sealed class AsyncTaskWorkItem(Func<Task> asyncFunc, TaskCompletionSource tcs) : IWorkItem
    {
        public bool RequiresTasks => false;

        public void Execute()
        {
            try
            {
                var task = asyncFunc();

                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        tcs.SetException(t.Exception!.InnerExceptions);
                    else if (t.IsCanceled)
                        tcs.SetCanceled();
                    else
                        tcs.SetResult();
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }
    }

    private sealed class AsyncTaskWorkItem<T>(Func<Task<T>> asyncFunc, TaskCompletionSource<T> tcs) : IWorkItem
    {
        public bool RequiresTasks => false;

        public void Execute()
        {
            try
            {
                var task = asyncFunc();

                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        tcs.SetException(t.Exception!.InnerExceptions);
                    else if (t.IsCanceled)
                        tcs.SetCanceled();
                    else
                        tcs.SetResult(t.Result);
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }
    }

    private sealed class YieldWorkItem(Action continuation) : IWorkItem
    {
        public bool RequiresTasks => true;

        public void Execute() => continuation();
    }

    #endregion
}