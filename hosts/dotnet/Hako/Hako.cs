using HakoJS.Backend.Core;
using HakoJS.Host;
using System.Threading;

namespace HakoJS;

/// <summary>
/// Provides the main entry point for initializing and managing the HakoJS runtime and event loop.
/// </summary>
public static class Hako
{
    private static readonly Lock Lock = new();
    private static HakoRuntime? _runtime;
    private static HakoEventLoop? _eventLoop;

    /// <summary>
    /// Gets the dispatcher for executing work on the event loop thread.
    /// </summary>
    public static HakoDispatcher Dispatcher { get; } = new();

    /// <summary>
    /// Gets the current HakoJS runtime instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">No runtime has been initialized.</exception>
    public static HakoRuntime Runtime
    {
        get
        {
            using (Lock.EnterScope())
            {
                return _runtime ?? throw new InvalidOperationException(
                    "No HakoRuntime has been initialized. Call Hako.Initialize() first.");
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the HakoJS runtime has been initialized.
    /// </summary>
    internal static bool IsInitialized
    {
        get
        {
            using (Lock.EnterScope())
            {
                return _runtime != null && _eventLoop != null;
            }
        }
    }

    /// <summary>
    /// Occurs when an unhandled exception is thrown on the event loop thread.
    /// </summary>
    public static event EventHandler<UnhandledExceptionEventArgs>? EventLoopException;

    /// <summary>
    /// Initializes the HakoJS runtime and event loop with the specified configuration.
    /// </summary>
    /// <param name="configure">An optional action to configure the runtime options.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the event loop externally.</param>
    /// <returns>The initialized runtime instance.</returns>
    /// <exception cref="InvalidOperationException">The runtime has already been initialized.</exception>
    public static HakoRuntime Initialize<TEngine>(Action<HakoOptions<TEngine>>? configure = null, CancellationToken cancellationToken = default) where TEngine : WasmEngine, IWasmEngineFactory<TEngine>
    {
        using (Lock.EnterScope())
        {
            if (_runtime != null || _eventLoop != null)
                throw new InvalidOperationException(
                    "HakoRuntime has already been initialized. Call ShutdownAsync() first.");

            _eventLoop = new HakoEventLoop(cancellationToken: cancellationToken);
            _eventLoop.UnhandledException += OnEventLoopException;

            Dispatcher.Initialize(_eventLoop);

            var options = new HakoOptions<TEngine>();
            configure?.Invoke(options);

            _runtime = HakoRuntime.Create(options);
            _eventLoop.SetRuntime(_runtime);

            return _runtime;
        }
    }

    /// <summary>
    /// Shuts down the HakoJS runtime and event loop asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for shutdown to complete.</param>
    /// <returns>A task that completes when the shutdown is complete.</returns>
    public static async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        HakoEventLoop? eventLoopToStop;
        HakoRuntime? runtimeToDispose;

        using (Lock.EnterScope())
        {
            eventLoopToStop = _eventLoop;
            runtimeToDispose = _runtime;
        }

        if (runtimeToDispose != null && eventLoopToStop != null)
        {
            await eventLoopToStop.InvokeAsync(() => runtimeToDispose.Dispose(), cancellationToken);
            
            eventLoopToStop.UnhandledException -= OnEventLoopException;
            await eventLoopToStop.StopAsync(cancellationToken).ConfigureAwait(false);
            eventLoopToStop.Dispose();
        }
        Dispatcher.Reset();
        using (Lock.EnterScope())
        {
            _eventLoop = null;
            _runtime = null;
        }
    }

    /// <summary>
    /// Waits for the event loop to exit.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the event loop to exit.</param>
    /// <returns>A task that completes when the event loop has exited.</returns>
    /// <exception cref="InvalidOperationException">No event loop has been initialized.</exception>
    public static Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        using (Lock.EnterScope())
        {
            if (_eventLoop == null)
                throw new InvalidOperationException(
                    "No event loop has been initialized. Call Hako.Initialize() first.");

            return _eventLoop.WaitForExitAsync(cancellationToken);
        }
    }

    private static void OnEventLoopException(object? sender, UnhandledExceptionEventArgs e)
    {
        EventLoopException?.Invoke(null, e);
    }

    internal static void NotifyEventLoopDisposed(HakoEventLoop eventLoop)
    {
        using (Lock.EnterScope())
        {
            if (_eventLoop == eventLoop)
            {
                _eventLoop = null;
                Dispatcher.Reset();
            }
        }
    }
}