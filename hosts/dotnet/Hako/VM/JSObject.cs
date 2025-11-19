using HakoJS.Exceptions;

namespace HakoJS.VM;

/// <summary>
/// Provides a simplified wrapper around <see cref="JSValue"/> for working with JavaScript objects.
/// </summary>
/// <remarks>
/// <para>
/// This class offers a more convenient API for common object operations like getting and setting properties,
/// while managing the lifecycle of the underlying <see cref="JSValue"/>.
/// </para>
/// <para>
/// Note: In most cases, working directly with <see cref="JSValue"/> is more flexible and idiomatic.
/// This class is provided for scenarios where a dedicated object wrapper is preferred.
/// </para>
/// <para>
/// Example:
/// <code>
/// using var obj = new JSObject(realm, realm.NewObject());
/// obj.SetProperty("name", "Alice");
/// obj.SetProperty("age", 30);
/// 
/// using var nameValue = obj.GetProperty("name");
/// var name = nameValue.AsString(); // "Alice"
/// </code>
/// </para>
/// </remarks>
public sealed class JSObject : IDisposable
{
    private readonly JSValue _value;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JSObject"/> class.
    /// </summary>
    /// <param name="context">The realm in which the object exists.</param>
    /// <param name="value">The JavaScript value representing the object.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="value"/> is <c>null</c>.
    /// </exception>
    internal JSObject(Realm context, JSValue value)
    {
        Realm = context ?? throw new ArgumentNullException(nameof(context));
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the realm in which this object exists.
    /// </summary>
    private Realm Realm { get; }

    /// <summary>
    /// Releases the underlying <see cref="JSValue"/> and marks this object as disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _value.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Returns the underlying <see cref="JSValue"/> without duplicating it.
    /// </summary>
    /// <returns>The underlying <see cref="JSValue"/>.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <remarks>
    /// Warning: The returned value shares the same lifecycle as this <see cref="JSObject"/>.
    /// When this object is disposed, the returned value becomes invalid.
    /// </remarks>
    public JSValue Value()
    {
        ThrowIfDisposed();
        return _value;
    }

    /// <summary>
    /// Creates a duplicate of the underlying <see cref="JSValue"/> with independent lifecycle.
    /// </summary>
    /// <returns>A new <see cref="JSValue"/> that must be disposed independently.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <remarks>
    /// Use this when you need a <see cref="JSValue"/> that outlives this <see cref="JSObject"/>.
    /// The caller is responsible for disposing the returned value.
    /// </remarks>
    public JSValue Dup()
    {
        ThrowIfDisposed();
        return _value.Dup();
    }

    /// <summary>
    /// Sets a named property on the JavaScript object.
    /// </summary>
    /// <typeparam name="T">The .NET type of the value to set.</typeparam>
    /// <param name="key">The property name.</param>
    /// <param name="value">The value to set. Can be a .NET primitive, string, or <see cref="JSValue"/>.</param>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="HakoException">An error occurred setting the property.</exception>
    /// <remarks>
    /// <para>
    /// .NET values are automatically converted to JavaScript values. If <paramref name="value"/> is already
    /// a <see cref="JSValue"/>, it is consumed.
    /// </para>
    /// </remarks>
    public void SetProperty<T>(string key, T value)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        using var vmValue = Realm.NewValue(value);
        _value.SetProperty(key, vmValue);
    }

    /// <summary>
    /// Sets an indexed property (array element) on the JavaScript object.
    /// </summary>
    /// <typeparam name="T">The .NET type of the value to set.</typeparam>
    /// <param name="index">The numeric index of the property.</param>
    /// <param name="value">The value to set. Can be a .NET primitive, string, or <see cref="JSValue"/>.</param>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    /// <exception cref="HakoException">An error occurred setting the property.</exception>
    /// <remarks>
    /// This is typically used for setting array elements: <c>obj.SetProperty(0, "first")</c>.
    /// </remarks>
    public void SetProperty<T>(int index, T value)
    {
        ThrowIfDisposed();

        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be negative");

        using var vmValue = Realm.NewValue(value);
        _value.SetProperty(index, vmValue);
    }

    /// <summary>
    /// Gets a named property from the JavaScript object.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <returns>A <see cref="JSValue"/> representing the property value that must be disposed.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="HakoException">An error occurred getting the property.</exception>
    /// <remarks>
    /// The caller is responsible for disposing the returned <see cref="JSValue"/>.
    /// If the property doesn't exist, returns a JavaScript <c>undefined</c> value.
    /// </remarks>
    public JSValue GetProperty(string key)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or whitespace", nameof(key));

        return _value.GetProperty(key);
    }

    /// <summary>
    /// Gets an indexed property (array element) from the JavaScript object.
    /// </summary>
    /// <param name="index">The numeric index of the property.</param>
    /// <returns>A <see cref="JSValue"/> representing the property value that must be disposed.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    /// <exception cref="HakoException">An error occurred getting the property.</exception>
    /// <remarks>
    /// This is typically used for accessing array elements: <c>var first = obj.GetProperty(0)</c>.
    /// The caller is responsible for disposing the returned <see cref="JSValue"/>.
    /// </remarks>
    public JSValue GetProperty(int index)
    {
        ThrowIfDisposed();

        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be negative");

        return _value.GetProperty(index);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(JSObject));
    }

    /// <summary>
    /// Implicitly converts a <see cref="JSObject"/> to its underlying <see cref="JSValue"/>.
    /// </summary>
    /// <param name="obj">The object to convert.</param>
    /// <returns>The underlying <see cref="JSValue"/>.</returns>
    /// <remarks>
    /// This allows <see cref="JSObject"/> to be used in places where <see cref="JSValue"/> is expected.
    /// The returned value shares the same lifecycle as the <see cref="JSObject"/>.
    /// </remarks>
    public static implicit operator JSValue(JSObject obj)
    {
        return obj.Value();
    }
}