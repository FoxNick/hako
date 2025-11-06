using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.VM;

namespace HakoJS.Lifetime;

public interface IAlive
{
    bool Alive { get; }
}

public sealed class NativeBox<TValue> : IDisposable, IAlive
{
    private readonly Action<TValue>? _disposeAction;
    private TValue _value;

    internal NativeBox(TValue value, Action<TValue>? disposeAction = null)
    {
        _value = value;
        _disposeAction = disposeAction;
    }


    public TValue Value
    {
        get
        {
            if (!Alive)
                throw new ObjectDisposedException(nameof(NativeBox<TValue>));
            return _value;
        }
    }


    public bool Alive { get; private set; } = true;

    public void Dispose()
    {
        if (!Alive)
            return;

        Alive = false;

        try
        {
            if (_value is IDisposable disposable) disposable.Dispose();

            _disposeAction?.Invoke(_value);
        }
        finally
        {
            _value = default!;
        }
    }
}

public abstract class Result<TSuccess, TFailure>
{
    public abstract bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public static Result<TSuccess, TFailure> Success(TSuccess value)
    {
        return new SuccessResult(value);
    }

    public static Result<TSuccess, TFailure> Failure(TFailure error)
    {
        return new FailureResult(error);
    }

    public abstract bool TryGetSuccess(out TSuccess value);
    public abstract bool TryGetFailure(out TFailure error);
    public abstract TSuccess Unwrap();
    public abstract TSuccess UnwrapOr(TSuccess fallback);

    public abstract TResult Match<TResult>(
        Func<TSuccess, TResult> onSuccess,
        Func<TFailure, TResult> onFailure);

    public abstract void Match(
        Action<TSuccess> onSuccess,
        Action<TFailure> onFailure);

    private sealed class SuccessResult(TSuccess value) : Result<TSuccess, TFailure>
    {
        public override bool IsSuccess => true;

        public override bool TryGetSuccess(out TSuccess value1)
        {
            value1 = value;
            return true;
        }

        public override bool TryGetFailure(out TFailure error)
        {
            error = default!;
            return false;
        }

        public override TSuccess Unwrap()
        {
            return value;
        }

        public override TSuccess UnwrapOr(TSuccess fallback)
        {
            return value;
        }

        public override TResult Match<TResult>(
            Func<TSuccess, TResult> onSuccess,
            Func<TFailure, TResult> onFailure)
        {
            return onSuccess(value);
        }

        public override void Match(
            Action<TSuccess> onSuccess,
            Action<TFailure> onFailure)
        {
            onSuccess(value);
        }
    }

    private sealed class FailureResult(TFailure error) : Result<TSuccess, TFailure>
    {
        public override bool IsSuccess => false;

        public override bool TryGetSuccess(out TSuccess value)
        {
            value = default!;
            return false;
        }

        public override bool TryGetFailure(out TFailure error1)
        {
            error1 = error;
            return true;
        }

        public override TSuccess Unwrap()
        {
            throw new InvalidOperationException(
                "Cannot unwrap a failure result. Check TryGetSuccess or TryGetFailure before calling Unwrap.");
        }

        public override TSuccess UnwrapOr(TSuccess fallback)
        {
            return fallback;
        }

        public override TResult Match<TResult>(
            Func<TSuccess, TResult> onSuccess,
            Func<TFailure, TResult> onFailure)
        {
            return onFailure(error);
        }

        public override void Match(
            Action<TSuccess> onSuccess,
            Action<TFailure> onFailure)
        {
            onFailure(error);
        }
    }
}

public abstract class DisposableResult<TSuccess, TFailure> : IDisposable, IAlive
{
    public abstract bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public abstract bool Alive { get; }

    public abstract void Dispose();

    public static DisposableResult<TSuccess, TFailure> Success(TSuccess value)
    {
        return new DisposableSuccess(value);
    }

    public static DisposableResult<TSuccess, TFailure> Failure(TFailure error)
    {
        return new DisposableFailure(error);
    }

    public abstract bool TryGetSuccess(out TSuccess value);
    public abstract bool TryGetFailure(out TFailure error);


    public abstract TSuccess Unwrap();


    public abstract TSuccess Peek();

    public abstract TSuccess UnwrapOr(TSuccess fallback);

    public abstract TResult Match<TResult>(
        Func<TSuccess, TResult> onSuccess,
        Func<TFailure, TResult> onFailure);

    public static bool Is(object? value, out DisposableResult<TSuccess, TFailure>? result)
    {
        result = value as DisposableResult<TSuccess, TFailure>;
        return result != null;
    }

    private sealed class DisposableSuccess(TSuccess value) : DisposableResult<TSuccess, TFailure>
    {
        private bool _disposed;
        private bool _ownershipTransferred;
        private TSuccess _value = value;

        public override bool IsSuccess => true;

        public override bool Alive
        {
            get
            {
                if (_disposed) return false;
                if (_value is IAlive alive) return alive.Alive;
                return true;
            }
        }

        public override bool TryGetSuccess(out TSuccess value)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DisposableSuccess));
            value = _value;
            return true;
        }

        public override bool TryGetFailure(out TFailure error)
        {
            error = default!;
            return false;
        }

        public override TSuccess Unwrap()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DisposableSuccess));

            _ownershipTransferred = true;
            return _value;
        }

        public override TSuccess Peek()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DisposableSuccess));
            return _value;
        }

        public override TSuccess UnwrapOr(TSuccess fallback)
        {
            return _disposed ? fallback : _value;
        }

        public override TResult Match<TResult>(
            Func<TSuccess, TResult> onSuccess,
            Func<TFailure, TResult> onFailure)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DisposableSuccess));
            return onSuccess(_value);
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Only dispose if ownership wasn't transferred
            if (!_ownershipTransferred && _value is IDisposable disposable)
                disposable.Dispose();

            _value = default!;
        }
    }

    private sealed class DisposableFailure(TFailure error) : DisposableResult<TSuccess, TFailure>
    {
        private bool _disposed;
        private TFailure _error = error;

        public override bool IsSuccess => false;

        public override bool Alive
        {
            get
            {
                if (_disposed) return false;
                if (_error is IAlive alive) return alive.Alive;
                return true;
            }
        }

        public override bool TryGetSuccess(out TSuccess value)
        {
            value = default!;
            return false;
        }

        public override bool TryGetFailure(out TFailure error)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DisposableFailure));
            error = _error;
            return true;
        }

        public override TSuccess Unwrap()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DisposableFailure));

            throw new InvalidOperationException(
                "Cannot unwrap a failure result. Check TryGetSuccess or TryGetFailure before calling Unwrap.");
        }

        public override TSuccess Peek()
        {
            throw new InvalidOperationException("Cannot peek a failure result.");
        }

        public override TSuccess UnwrapOr(TSuccess fallback)
        {
            return fallback;
        }

        public override TResult Match<TResult>(
            Func<TSuccess, TResult> onSuccess,
            Func<TFailure, TResult> onFailure)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DisposableFailure));
            return onFailure(_error);
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_error is IDisposable disposable)
                disposable.Dispose();

            _error = default!;
        }
    }
}