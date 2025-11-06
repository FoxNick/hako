using System.Text;

namespace HakoJS.Exceptions;

/// <summary>
/// Represents an error that originated from JavaScript code.
/// This exception is never thrown by .NET - it's always constructed from
/// JavaScript error data, so Message and StackTrace are overridden to show
/// the JavaScript-side information.
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

    public string? JsMessage { get; }
    public string? JsErrorName { get; }
    public object? JsCause { get; }

    private string? JsStackTrace { get; }

    /// <summary>
    /// Returns the JavaScript error message, formatted with the error name if available.
    /// </summary>
    public override string Message
    {
        get
        {
            if (!string.IsNullOrEmpty(JsErrorName) && !string.IsNullOrEmpty(JsMessage))
                return $"{JsErrorName}: {JsMessage}";
            
            if (!string.IsNullOrEmpty(JsMessage))
                return JsMessage;
            
            return base.Message;
        }
    }

    /// <summary>
    /// Returns the JavaScript stack trace, not the .NET stack trace.
    /// </summary>
    public override string? StackTrace => JsStackTrace;

    public override string ToString()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine(Message);

        if (!string.IsNullOrEmpty(JsStackTrace))
            sb.AppendLine(JsStackTrace);

        if (JsCause != null)
        {
            sb.AppendLine();
            AppendCause(sb, JsCause);
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendCause(StringBuilder sb, object cause)
    {
        sb.Append("Caused by: ");

        if (cause is Dictionary<string, object?> dict)
        {
            if (dict.TryGetValue("name", out var name) && dict.TryGetValue("message", out var message))
            {
                sb.Append(name).Append(": ").AppendLine(message?.ToString());

                if (dict.TryGetValue("stack", out var stack) && stack is string stackStr)
                    sb.AppendLine(stackStr);
            }
            else
            {
                sb.AppendLine(FormatObject(dict));
            }
        }
        else if (cause is List<object?> list)
        {
            sb.AppendLine(FormatArray(list));
        }
        else
        {
            sb.AppendLine(cause.ToString());
        }
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
}