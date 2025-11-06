using System.Text.Json;
using HakoJS.Exceptions;
using HakoJS.Host;
using HakoJS.Memory;

namespace HakoJS.Utils;

public class JavaScriptErrorDetails
{
    public string Message { get; init; } = string.Empty;
    public string? Stack { get; init; }
    public string? Name { get; init; }
    public object? Cause { get; init; }
}

internal class ErrorManager(HakoRegistry registry, MemoryManager memory)
{
    private readonly MemoryManager _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    private readonly HakoRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));


    public int GetLastErrorPointer(int ctx, int ptr = 0)
    {
        return _registry.GetLastError(ctx, ptr);
    }


    public int NewError(int ctx)
    {
        return _registry.NewError(ctx);
    }


    public int ThrowError(int ctx, int errorPtr)
    {
        return _registry.Throw(ctx, errorPtr);
    }


    public int ThrowErrorMessage(int ctx, string message)
    {
        using var msgPtr = _memory.AllocateString(ctx, message, out _);
        return _registry.Throw(ctx, (int)msgPtr);
    }


    private JavaScriptErrorDetails DumpException(int ctx, int ptr)
    {
        var errorStrPtr = HakoRegistry.NullPointer;
        try
        {
            errorStrPtr = _registry.Dump(ctx, ptr);
            var errorStr = _memory.ReadNullTerminatedString(errorStrPtr);

            try
            {
                using var jsonDoc = JsonDocument.Parse(errorStr);
                var errorObj = jsonDoc.RootElement;

                return new JavaScriptErrorDetails
                {
                    Message = errorObj.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString() ?? errorStr
                        : errorStr,
                    Stack = errorObj.TryGetProperty("stack", out var stackProp)
                        ? stackProp.GetString()
                        : null,
                    Name = errorObj.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString()
                        : null,
                    Cause = errorObj.TryGetProperty("cause", out var causeProp)
                        ? DeserializeCause(causeProp)
                        : null
                };
            }
            catch (JsonException)
            {
                // Not valid JSON, just return the string as the message
                return new JavaScriptErrorDetails { Message = errorStr };
            }
        }
        finally
        {
            if (errorStrPtr != HakoRegistry.NullPointer) _memory.FreeCString(ctx, errorStrPtr);
        }
    }


    private static object? DeserializeCause(JsonElement causeElement)
    {
        return causeElement.ValueKind switch
        {
            JsonValueKind.Object => ParseObject(causeElement),
            JsonValueKind.Array => ParseArray(causeElement),
            JsonValueKind.String => causeElement.GetString(),
            JsonValueKind.Number => causeElement.TryGetInt64(out var l) ? l : causeElement.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => causeElement.GetRawText()
        };
    }


    private static Dictionary<string, object?> ParseObject(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject()) dict[property.Name] = DeserializeCause(property.Value);
        return dict;
    }


    private static List<object?> ParseArray(JsonElement element)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray()) list.Add(DeserializeCause(item));
        return list;
    }


    public JavaScriptException GetExceptionDetails(int ctx, int ptr)
    {
        var details = DumpException(ctx, ptr);

        var exception = new JavaScriptException(
            details.Message,
            details.Message,
            details.Stack,
            details.Name,
            details.Cause
        );

        return exception;
    }


    public bool IsException(int ptr)
    {
        return _registry.IsException(ptr) != 0;
    }


    public bool IsError(int ctx, int ptr)
    {
        return _registry.IsError(ctx, ptr) != 0;
    }
}