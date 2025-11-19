using System.Text;

namespace HakoJS.Exceptions;

/// <summary>
/// Represents an error that originated from JavaScript code.
/// This exception is never thrown by .NET - it's always constructed from
/// JavaScript error data. Formatting is handled by V8StackTraceFormatter.
/// </summary>
public class JavaScriptException : Exception
{
    public JavaScriptException(string message) : base(message)
    {
        JsMessage = message;
    }

    public JavaScriptException(
        string message,
        string? jsMessage,
        string? jsStackTrace = null,
        string? jsErrorName = null,
        object? jsCause = null)
        : base(message)
    {
        JsMessage = jsMessage ?? message;
        JsStackTrace = jsStackTrace;
        JsErrorName = jsErrorName;
        JsCause = jsCause;
    }

    /// <summary>
    /// The original JavaScript error message.
    /// </summary>
    public string? JsMessage { get; }
    
    /// <summary>
    /// The JavaScript error name (e.g., "TypeError", "ReferenceError").
    /// </summary>
    public string? JsErrorName { get; }
    
    /// <summary>
    /// The JavaScript error cause, if any.
    /// </summary>
    public object? JsCause { get; }

    /// <summary>
    /// The JavaScript stack trace as it appeared in JS.
    /// </summary>
    public string? JsStackTrace { get; }
}

