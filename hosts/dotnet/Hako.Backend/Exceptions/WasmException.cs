using System;

namespace HakoJS.Backend.Exceptions;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
/// <summary>
/// Base exception for all WebAssembly runtime errors.
/// </summary>
public abstract class WasmException : Exception
{
    /// <summary>
    /// Gets the name of the backend that raised this exception.
    /// </summary>
    public string? BackendName { get; init; }

    protected WasmException(string message) : base(message)
    {
    }

    protected WasmException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when module compilation fails.
/// </summary>
public sealed class WasmCompilationException : WasmException
{
    /// <inheritdoc />
    public WasmCompilationException(string message) : base(message)
    {
    }

    /// <inheritdoc />
    public WasmCompilationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when module instantiation fails.
/// </summary>
public sealed class WasmInstantiationException : WasmException
{
    /// <inheritdoc />
    public WasmInstantiationException(string message) : base(message)
    {
    }

    /// <inheritdoc />
    public WasmInstantiationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a runtime error occurs during execution.
/// </summary>
public sealed class WasmRuntimeException : WasmException
{
    /// <inheritdoc />
    public WasmRuntimeException(string message) : base(message)
    {
    }

    /// <inheritdoc />
    public WasmRuntimeException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a WebAssembly trap occurs.
/// </summary>
public sealed class WasmTrapException : WasmException
{
    /// <summary>
    /// Gets the trap code that caused this exception.
    /// </summary>
    public string? TrapCode { get; init; }

    public WasmTrapException(string message, string? trapCode = null) : base(message)
    {
        TrapCode = trapCode;
    }

    public WasmTrapException(string message, Exception innerException, string? trapCode = null) 
        : base(message, innerException)
    {
        TrapCode = trapCode;
    }
}