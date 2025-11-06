namespace HakoJS.Host;

/// <summary>
/// Custom SynchronizationContext that ensures async continuations
/// execute on the HakoJS event loop thread.
/// </summary>
internal sealed class HakoSynchronizationContext : SynchronizationContext
{
    private readonly HakoEventLoop _eventLoop;

    internal HakoSynchronizationContext(HakoEventLoop eventLoop)
    {
        _eventLoop = eventLoop ?? throw new ArgumentNullException(nameof(eventLoop));
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        _eventLoop.Post(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        _eventLoop.Invoke(() => d(state));
    }

    public override SynchronizationContext CreateCopy()
    {
        return new HakoSynchronizationContext(_eventLoop);
    }
}