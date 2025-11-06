namespace HakoJS.Lifetime;

public sealed class DisposableValue<T> : IDisposable
{
    private readonly Action<T> _disposeAction;
    private T _value;


    internal DisposableValue(T value, Action<T> disposeAction)
    {
        _value = value;
        _disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
        IsDisposed = false;
    }


    public T Value
    {
        get
        {
            ThrowIfDisposed();
            return _value;
        }
    }


    private bool IsDisposed { get; set; }


    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;

        try
        {
            _disposeAction(_value);
        }
        finally
        {
            _value = default!;
        }
    }


    public static implicit operator T(DisposableValue<T> disposable)
    {
        return disposable.Value;
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name, "Cannot access value after disposal");
    }
}