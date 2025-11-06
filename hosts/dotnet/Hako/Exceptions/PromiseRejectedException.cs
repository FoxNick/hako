using System;
using System.Collections.Generic;
using System.Text;

namespace HakoJS.Exceptions;

/// <summary>
/// Represents a JavaScript Promise rejection surfaced to .NET.
/// Unlike typical exceptions, the rejection <c>reason</c> may be any JS value
/// (string, object, array, Error-like, null). When the reason is a JavaScript error,
/// it will be wrapped as a <see cref="JavaScriptException"/> and set as the InnerException.
/// </summary>
public sealed class PromiseRejectedException : Exception
{
    /// <summary>
    /// The raw JS rejection reason as marshaled into .NET.
    /// This may be a string, number, bool, dictionary (object),
    /// list (array), an <see cref="Exception"/>, or <c>null</c>.
    /// </summary>
    public object? Reason { get; }

    /// <summary>
    /// True if the reason looked like a JS Error object
    /// (i.e., had 'name' and 'message', optionally 'stack').
    /// </summary>
    public bool IsErrorLike { get; }

    /// <summary>
    /// Creates an exception for an unhandled Promise rejection.
    /// If the reason is a JavaScript error, it will be wrapped as a JavaScriptException
    /// and set as the InnerException.
    /// </summary>
    /// <param name="reason">Arbitrary JS value used to reject the Promise.</param>
    public PromiseRejectedException(object? reason)
        : base(CreateMessage(reason), CreateInnerException(reason))
    {
        Reason = reason;
        IsErrorLike = DetermineIfErrorLike(reason);
    }

    /// <summary>
    /// Creates an exception with a custom high-level message while still
    /// preserving/normalizing the JS rejection details.
    /// </summary>
    public PromiseRejectedException(string message, object? reason)
        : base(message ?? "Unhandled promise rejection", CreateInnerException(reason))
    {
        Reason = reason;
        IsErrorLike = DetermineIfErrorLike(reason);
    }

    /// <summary>
    /// Creates a promise rejection with a JavaScriptException as the cause.
    /// </summary>
    public PromiseRejectedException(JavaScriptException innerException)
        : base("Promise was rejected", innerException)
    {
        Reason = innerException;
        IsErrorLike = true;
    }

    /// <summary>
    /// Creates a promise rejection with a custom message and a JavaScriptException as the cause.
    /// </summary>
    public PromiseRejectedException(string message, JavaScriptException innerException)
        : base(message ?? "Unhandled promise rejection", innerException)
    {
        Reason = innerException;
        IsErrorLike = true;
    }

    private static string CreateMessage(object? reason)
    {
        if (reason is null)
            return "Promise rejected with: (null)";

        if (reason is JavaScriptException jsEx)
            return $"Promise rejected with: {jsEx.JsErrorName ?? "Error"}: {jsEx.JsMessage ?? jsEx.Message}";

        if (reason is Exception ex)
            return $"Promise rejected with: {ex.Message}";

        if (reason is Dictionary<string, object?> dict)
        {
            var hasName = dict.TryGetValue("name", out var nm) && nm is not null;
            var hasMsg = dict.TryGetValue("message", out var msg) && msg is not null;

            if (hasName || hasMsg)
            {
                var name = nm?.ToString();
                var message = msg?.ToString();
                return $"Promise rejected with: {name ?? "Error"}: {message ?? "(no message)"}";
            }

            return "Promise rejected with: [object Object]";
        }

        if (reason is List<object?> list)
            return $"Promise rejected with: [array of {list.Count} items]";

        return $"Promise rejected with: {reason}";
    }

    private static Exception? CreateInnerException(object? reason)
    {
        if (reason is null)
            return null;

        // If it's already a .NET exception, use it as-is
        if (reason is JavaScriptException jsEx)
            return jsEx;

        if (reason is Exception ex)
            return ex;

        // If it's a JS Error object, wrap it in JavaScriptException
        if (reason is Dictionary<string, object?> dict)
        {
            var hasName = dict.TryGetValue("name", out var nm) && nm is not null;
            var hasMsg = dict.TryGetValue("message", out var msg) && msg is not null;

            if (hasName || hasMsg)
            {
                var name = nm?.ToString();
                var message = msg?.ToString() ?? "(no message)";
                var stack = dict.TryGetValue("stack", out var st) && st is string stackStr ? stackStr : null;
                var cause = dict.TryGetValue("cause", out var c) ? c : null;

                return new JavaScriptException(
                    message: message,
                    jsMessage: message,
                    jsStackTrace: stack,
                    jsErrorName: name,
                    jsCause: cause);
            }
        }

        // Non-error values don't get wrapped
        return null;
    }

    private static bool DetermineIfErrorLike(object? reason)
    {
        if (reason is Exception)
            return true;

        if (reason is Dictionary<string, object?> dict)
        {
            var hasName = dict.TryGetValue("name", out var nm) && nm is not null;
            var hasMsg = dict.TryGetValue("message", out var msg) && msg is not null;
            return hasName || hasMsg;
        }

        return false;
    }

    /// <summary>
    /// A short, one-line summary that's handy for logs.
    /// </summary>
    public string Summary
    {
        get
        {
            if (InnerException is JavaScriptException jsEx)
            {
                return string.IsNullOrEmpty(jsEx.JsErrorName)
                    ? (jsEx.JsMessage ?? Message)
                    : $"{jsEx.JsErrorName}: {jsEx.JsMessage}";
            }

            if (InnerException != null)
                return InnerException.Message;

            return Message;
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Message);

        if (InnerException != null)
        {
            sb.AppendLine();
            sb.AppendLine("Caused by:");
            sb.Append(InnerException.ToString());
        }
        else if (Reason != null && !IsErrorLike)
        {
            // For non-error rejections, show the raw value
            sb.AppendLine();
            sb.Append("Rejection value: ");
            sb.Append(FormatValue(Reason));
        }

        if (!string.IsNullOrEmpty(StackTrace))
        {
            sb.AppendLine();
            sb.Append(StackTrace);
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            Dictionary<string, object?> dict => FormatObject(dict),
            List<object?> list => FormatArray(list),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? "null"
        };
    }

    private static string FormatObject(Dictionary<string, object?> dict)
    {
        var sb = new StringBuilder("{ ");
        var first = true;

        foreach (var kvp in dict)
        {
            if (!first) sb.Append(", ");
            first = false;

            sb.Append(kvp.Key).Append(": ");
            sb.Append(FormatValue(kvp.Value));
        }

        sb.Append(" }");
        return sb.ToString();
    }

    private static string FormatArray(List<object?> list)
    {
        var sb = new StringBuilder("[ ");

        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(FormatValue(list[i]));
        }

        sb.Append(" ]");
        return sb.ToString();
    }
}