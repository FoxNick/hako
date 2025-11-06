using System.Collections.Concurrent;
using HakoJS.VM;

namespace HakoJS.Host;

public sealed class TimerManager : IDisposable
{
    private readonly ConcurrentDictionary<int, TimerEntry> _activeTimers = new();
    private readonly Realm _context;
    private readonly ConcurrentQueue<TimerEntry> _pendingDisposals = new();
    private bool _disposed;
    private int _nextTimerId;

    public TimerManager(Realm context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }


    public int ActiveTimerCount => _activeTimers.Count;


    public bool HasActiveTimers => !_activeTimers.IsEmpty;

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        // Dispose all active timers
        var exceptions = new List<Exception>();

        foreach (var kvp in _activeTimers)
            try
            {
                kvp.Value.Dispose();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

        _activeTimers.Clear();

        // Process any pending disposals
        ProcessPendingDisposals();

        if (exceptions.Count > 0)
            throw new AggregateException("One or more errors occurred while disposing timers", exceptions);
    }


    public int SetTimeout(JSValue callback, int delay)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentOutOfRangeException.ThrowIfNegative(delay);

        var callbackHandle = callback.Dup();
        var timerId = Interlocked.Increment(ref _nextTimerId);
        var executeAt = DateTime.UtcNow.AddMilliseconds(delay);

        var entry = new TimerEntry(
            callbackHandle,
            executeAt,
            null,
            false);

        if (!_activeTimers.TryAdd(timerId, entry))
        {
            callbackHandle.Dispose();
            throw new InvalidOperationException($"Timer ID {timerId} already exists.");
        }

        return timerId;
    }


    public int SetInterval(JSValue callback, int interval)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(interval);

        var callbackHandle = callback.Dup();
        var timerId = Interlocked.Increment(ref _nextTimerId);
        var executeAt = DateTime.UtcNow.AddMilliseconds(interval);

        var entry = new TimerEntry(
            callbackHandle,
            executeAt,
            interval,
            true);

        if (!_activeTimers.TryAdd(timerId, entry))
        {
            callbackHandle.Dispose();
            throw new InvalidOperationException($"Timer ID {timerId} already exists.");
        }

        return timerId;
    }


    public void ClearTimer(int timerId)
    {
        if (_activeTimers.TryRemove(timerId, out var entry))
            // Don't dispose immediately - the callback might still be executing
            // Queue it for disposal after all callbacks have finished
            _pendingDisposals.Enqueue(entry);
    }


    internal int ProcessTimers()
    {
        try
        {
            if (_disposed) return -1;

            // Process pending disposals from previous iteration first
            ProcessPendingDisposals();

            if (_activeTimers.IsEmpty) return -1;

            var now = DateTime.UtcNow;
            var nextTimerDelay = int.MaxValue;
            var timersToExecute = new List<(int TimerId, TimerEntry Entry)>();

            // Find all timers ready to execute
            foreach (var kvp in _activeTimers)
            {
                var entry = kvp.Value;
                var timeUntilExecution = (entry.ExecuteAt - now).TotalMilliseconds;

                if (timeUntilExecution <= 0)
                    timersToExecute.Add((kvp.Key, entry));
                else if (timeUntilExecution < nextTimerDelay) nextTimerDelay = (int)Math.Ceiling(timeUntilExecution);
            }

            // Execute ready timers
            foreach (var (timerId, entry) in timersToExecute)
                try
                {
                    InvokeCallback(entry.Callback);

                    // Check if timer still exists (might have been cleared during callback)
                    if (!_activeTimers.ContainsKey(timerId))
                        // Timer was cleared during execution - already in disposal queue
                        continue;

                    if (entry.IsRepeating && entry.Interval.HasValue)
                    {
                        // Schedule next execution for interval timers
                        entry.ExecuteAt = DateTime.UtcNow.AddMilliseconds(entry.Interval.Value);

                        var nextExecution = (entry.ExecuteAt - DateTime.UtcNow).TotalMilliseconds;
                        if (nextExecution < nextTimerDelay) nextTimerDelay = (int)Math.Ceiling(nextExecution);
                    }
                    else
                    {
                        // Remove one-time timers
                        if (_activeTimers.TryRemove(timerId, out var removedEntry))
                            _pendingDisposals.Enqueue(removedEntry);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other timers
                    Console.Error.WriteLine($"Timer {timerId} callback failed: {ex.Message}");

                    // Check if timer still exists before trying to remove
                    if (_activeTimers.ContainsKey(timerId))
                        // Remove failed timers (both one-time and intervals)
                        if (_activeTimers.TryRemove(timerId, out var removedEntry))
                            _pendingDisposals.Enqueue(removedEntry);
                }

            return _activeTimers.IsEmpty ? -1 : Math.Max(0, nextTimerDelay);
        }
        finally
        {
            // Always process disposals at the end, even if an exception occurred
            ProcessPendingDisposals();
        }
    }


    private void ProcessPendingDisposals()
    {
        while (_pendingDisposals.TryDequeue(out var entry))
            try
            {
                entry.Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error disposing timer entry: {ex.Message}");
            }
    }

    private void InvokeCallback(JSValue callback)
    {
        using var result = _context.CallFunction(callback);

        if (result.TryGetFailure(out var error))
        {
            var errorMessage = error.AsString();
            throw new InvalidOperationException($"Timer callback failed: {errorMessage}");
        }
    }


    private sealed class TimerEntry : IDisposable
    {
        private bool _disposed;

        public TimerEntry(JSValue callback, DateTime executeAt, int? interval, bool isRepeating)
        {
            Callback = callback;
            ExecuteAt = executeAt;
            Interval = interval;
            IsRepeating = isRepeating;
        }

        public JSValue Callback { get; }
        public DateTime ExecuteAt { get; set; }
        public int? Interval { get; }
        public bool IsRepeating { get; }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            Callback.Dispose();
        }
    }
}