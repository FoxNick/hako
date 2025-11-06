using System.Runtime.CompilerServices;

namespace HakoJS.Host;

/// <summary>
/// Awaitable type that yields control back to the event loop.
/// </summary>
public readonly struct EventLoopYieldAwaitable
{
    private readonly HakoEventLoop _eventLoop;

    internal EventLoopYieldAwaitable(HakoEventLoop eventLoop)
    {
        _eventLoop = eventLoop;
    }

    public EventLoopYieldAwaiter GetAwaiter() => new(_eventLoop);
}

/// <summary>
/// Awaiter that posts the continuation back to the event loop.
/// </summary>
public readonly struct EventLoopYieldAwaiter : ICriticalNotifyCompletion
{
    private readonly HakoEventLoop _eventLoop;

    internal EventLoopYieldAwaiter(HakoEventLoop eventLoop)
    {
        _eventLoop = eventLoop;
    }

    /// <summary>
    /// Always returns false to force yielding.
    /// </summary>
    public bool IsCompleted => false;

    public void GetResult() { }

    public void OnCompleted(Action continuation)
    {
        _eventLoop.PostYield(continuation);
    }

    public void UnsafeOnCompleted(Action continuation)
    {
        _eventLoop.PostYield(continuation);
    }
}