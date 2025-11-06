namespace HakoJS.Lifetime;

/// <summary>
/// Manages a scope of disposable resources using a LIFO (Last-In-First-Out) stack.
/// Resources are disposed in reverse order of registration when the scope exits.
/// </summary>
/// <remarks>
/// This implementation uses <see cref="Stack{T}"/> to ensure that the most recently
/// deferred disposable is disposed first, similar to nested using statements.
/// </remarks>
public sealed class DisposableScope : IDisposable
{
    private readonly Stack<IDisposable> _disposables = new();
    private bool _disposed;

    /// <summary>
    /// Gets the number of disposables currently tracked by this scope.
    /// </summary>
    public int Count => _disposables.Count;

    /// <summary>
    /// Registers a disposable to be disposed when the scope exits.
    /// The disposable is pushed onto a stack and will be disposed in LIFO order.
    /// </summary>
    /// <typeparam name="T">The type of disposable.</typeparam>
    /// <param name="disposable">The disposable to register.</param>
    /// <returns>The same disposable for convenience (fluent API).</returns>
    /// <exception cref="ArgumentNullException">Thrown when disposable is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the scope has already been disposed.</exception>
    public T Defer<T>(T disposable) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(disposable);
        
        if (_disposed)
            throw new ObjectDisposedException(nameof(DisposableScope), "Cannot defer disposables to an already disposed scope");
            
        _disposables.Push(disposable);
        return disposable;
    }

    /// <summary>
    /// Disposes all registered disposables in LIFO (stack) order.
    /// Each disposable is popped from the stack and disposed sequentially.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Use TryPop for safer, more idiomatic stack operations
        while (_disposables.TryPop(out var disposable))
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // Continue disposing remaining items even if one fails
                // Consider logging here in production code
            }
        }
    }

    /// <summary>
    /// Clears all tracked disposables without disposing them.
    /// Use with caution - this can lead to resource leaks.
    /// </summary>
    public void Clear()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DisposableScope));
            
        _disposables.Clear();
    }
}