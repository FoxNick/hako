using System.Text;
using System.Text.RegularExpressions;
using HakoJS.Exceptions;

namespace HakoJS.Utils;

internal static partial class V8StackTraceFormatter
{
    [GeneratedRegex(@"^\s*at\s+(?<content>.+?)(?:\s+in\s+(?<location>.+?)|\s+\((?<location>.+?)\))?$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex StackFrameRegex();

    [GeneratedRegex(@"^(?<returnType>[\w.`<>,\[\]]+\s+)?(?<type>[\w.`<>,\[\]]+)\.(?<method>[^(]+)(?:\((?<params>[^)]*)\))?(?<lambdaBody>\+.*)?$", RegexOptions.Compiled)]
    private static partial Regex MethodRegex();

    [GeneratedRegex(@"^(.+?)!<BaseAddress>(?:\+0x[0-9a-fA-F]+)?$", RegexOptions.Compiled)]
    private static partial Regex AotNoSymbolsRegex();

    [GeneratedRegex(@"^(.+?)`(\d+)(?:\[\[(.+?)\]\])?$", RegexOptions.Compiled)]
    private static partial Regex GenericTypeRegex();

    [GeneratedRegex(@"\s*\+\s*0x[0-9a-fA-F]+\s*$", RegexOptions.Compiled)]
    private static partial Regex OffsetRegex();

    [GeneratedRegex(@"<>c__DisplayClass\d+_\d+", RegexOptions.Compiled)]
    private static partial Regex DisplayClassRegex();

    [GeneratedRegex(@"<>c", RegexOptions.Compiled)]
    private static partial Regex ClosureClassRegex();

    [GeneratedRegex(@"<(.+?)>d__\d+(?:\.MoveNext)?", RegexOptions.Compiled)]
    private static partial Regex AsyncStateMachineRegex();

    [GeneratedRegex(@"<(.+?)>b__\d+(?:_\d+)?", RegexOptions.Compiled)]
    private static partial Regex LambdaRegex();

    [GeneratedRegex(@"<(.+?)>g__(.+?)\|\d+_\d+", RegexOptions.Compiled)]
    private static partial Regex LocalFunctionRegex();

    [GeneratedRegex(@"\.+", RegexOptions.Compiled)]
    private static partial Regex MultipleDotRegex();

    [GeneratedRegex(@":line\s+(\d+)$", RegexOptions.Compiled)]
    private static partial Regex LineNumberRegex();

    public static string Format(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var sb = new StringBuilder();
        var seenExceptions = new HashSet<string>();
        FormatExceptionWithDedup(exception, sb, seenExceptions, isNested: false);
        return sb.ToString().TrimEnd();
    }

    private static void FormatExceptionWithDedup(Exception exception, StringBuilder sb, HashSet<string> seenExceptions, bool isNested)
    {
        var header = GetExceptionHeader(exception);
        
        if (seenExceptions.Contains(header))
        {
            if (exception.InnerException != null)
            {
                FormatExceptionWithDedup(exception.InnerException, sb, seenExceptions, isNested: true);
            }
            
            if (exception is AggregateException { InnerExceptions.Count: > 1 } aggregateException)
            {
                foreach (var inner in aggregateException.InnerExceptions.Skip(1))
                {
                    FormatExceptionWithDedup(inner, sb, seenExceptions, isNested: true);
                }
            }
            return;
        }

        if (exception is JavaScriptException jsEx && !string.IsNullOrWhiteSpace(jsEx.JsStackTrace))
        {
            seenExceptions.Add(header);
            var dedupedStack = DeduplicateStackTrace(jsEx.JsStackTrace, seenExceptions).TrimEnd();
            
            if (isNested)
            {
                if (!dedupedStack.TrimStart().StartsWith("caused by:"))
                {
                    sb.Append("caused by: ");
                }
            }
            
            sb.AppendLine(dedupedStack);
            return;
        }

        seenExceptions.Add(header);
        
        if (isNested)
        {
            sb.Append("caused by: ");
            sb.AppendLine(!string.IsNullOrWhiteSpace(exception.Message)
                ? exception.Message
                : GetSimpleTypeName(exception.GetType()));
        }
        else
        {
            sb.AppendLine(header);
        }

        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            FormatStackTrace(exception.StackTrace, sb);
        }

        if (exception.InnerException != null)
        {
            FormatExceptionWithDedup(exception.InnerException, sb, seenExceptions, isNested: true);
        }

        if (exception is AggregateException { InnerExceptions.Count: > 1 } aggEx)
        {
            foreach (var inner in aggEx.InnerExceptions.Skip(1))
            {
                FormatExceptionWithDedup(inner, sb, seenExceptions, isNested: true);
            }
        }
    }

    private static string DeduplicateStackTrace(string stackTrace, HashSet<string> seenExceptions)
    {
        var lines = stackTrace.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var result = new StringBuilder();
        
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                result.AppendLine(line);
                continue;
            }

            var cleanedLine = RemoveMultipleCausedBy(trimmed);
            
            if (cleanedLine.StartsWith("caused by:"))
            {
                var exceptionHeader = cleanedLine["caused by:".Length..].Trim();
                
                if (string.IsNullOrWhiteSpace(exceptionHeader) && i + 1 < lines.Length)
                {
                    var nextLine = lines[i + 1].TrimStart();
                    if (IsExceptionHeader(nextLine))
                    {
                        if (!seenExceptions.Add(nextLine))
                        {
                            i = SkipExceptionBlock(lines, i + 1);
                            continue;
                        }
                    }
                }
                else if (IsExceptionHeader(exceptionHeader))
                {
                    if (!seenExceptions.Add(exceptionHeader))
                    {
                        i = SkipExceptionBlock(lines, i);
                        continue;
                    }

                    exceptionHeader = StripExceptionTypeFromHeader(exceptionHeader);
                }
                
                result.Append(line[..^trimmed.Length]);
                result.Append("caused by: ").AppendLine(exceptionHeader);
            }
            else if (IsExceptionHeader(trimmed))
            {
                if (!seenExceptions.Add(trimmed))
                {
                    i = SkipExceptionBlock(lines, i);
                    continue;
                }

                result.AppendLine(line);
            }
            else if (IsStackFrame(trimmed))
            {
                var formatted = FormatStackFrame(trimmed);
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    result.Append("    at ").AppendLine(formatted);
                }
            }
            else
            {
                result.AppendLine(line);
            }
        }
        
        return result.ToString();
    }

    private static bool IsStackFrame(string line)
    {
        return line.Contains("(") && line.Contains(")") && 
               (line.Contains(".") || line.Contains(" in "));
    }

    private static string StripExceptionTypeFromHeader(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex > 0 && !line[..colonIndex].Contains(' '))
        {
            return line[(colonIndex + 1)..].Trim();
        }
        return line;
    }

    private static string RemoveMultipleCausedBy(string line)
    {
        const string causedBy = "caused by: ";
        var count = 0;
        var pos = 0;
        
        while (line[pos..].StartsWith(causedBy))
        {
            count++;
            pos += causedBy.Length;
        }
        
        return count > 1 ? causedBy + line[pos..] : line;
    }

    private static bool IsExceptionHeader(string line)
    {
        return !line.StartsWith("at ") && 
               line.Contains("Exception") && 
               line.Contains(":");
    }

    private static int SkipExceptionBlock(string[] lines, int startIndex)
    {
        var i = startIndex;
        while (i + 1 < lines.Length)
        {
            i++;
            var nextLine = lines[i].TrimStart();
            if (nextLine.StartsWith("caused by:") || IsExceptionHeader(nextLine))
            {
                return i - 1;
            }
        }
        return i;
    }

    private static string GetExceptionHeader(Exception exception)
    {
        var typeName = GetSimpleTypeName(exception.GetType());
        return !string.IsNullOrWhiteSpace(exception.Message) 
            ? $"{typeName}: {exception.Message}" 
            : typeName;
    }

    private static string GetSimpleTypeName(Type type)
    {
        var name = type.Name;
        var backtickIndex = name.IndexOf('`');
        return backtickIndex > 0 ? name[..backtickIndex] : name;
    }

    private static void FormatStackTrace(string stackTrace, StringBuilder sb)
    {
        var lines = stackTrace.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (IsNoiseFrame(line))
                continue;

            var formatted = FormatStackFrame(line);
            if (!string.IsNullOrWhiteSpace(formatted))
            {
                sb.Append("    at ").AppendLine(formatted);
            }
        }
    }

    private static bool IsNoiseFrame(string line)
    {
        var trimmed = line.Trim();
        
        return trimmed.StartsWith("---") && trimmed.EndsWith("---") ||
               line.Contains("End of stack trace from previous location") ||
               line.Contains("End of inner exception stack trace") ||
               line.Contains("System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw") ||
               line.Contains("System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification") ||
               line.Contains("System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess") ||
               line.Contains("System.Runtime.CompilerServices.TaskAwaiter.ValidateEnd") ||
               (line.Contains("System.Runtime.CompilerServices.TaskAwaiter.GetResult") &&
                   !line.Contains("Program.") && !line.Contains("ConsoleApp"));
    }

    private static string FormatStackFrame(string frame)
    {
        if (frame.Contains(" ---> "))
            return string.Empty;

        var match = StackFrameRegex().Match(frame);
        if (!match.Success)
            return frame.TrimStart().TrimStart("at ".ToCharArray()).Trim();

        var content = match.Groups["content"].Value;
        var location = match.Groups["location"].Success ? match.Groups["location"].Value : null;

        var aotMatch = AotNoSymbolsRegex().Match(content);
        if (aotMatch.Success)
        {
            var assembly = aotMatch.Groups[1].Value;
            var result = $"{assembly}.<anonymous>";
            if (!string.IsNullOrWhiteSpace(location))
            {
                result += $" ({FormatLocation(location)})";
            }
            return result;
        }

        var demystified = DemystifyFrame(content);

        if (!string.IsNullOrWhiteSpace(location))
        {
            demystified += $" ({FormatLocation(location)})";
        }

        return demystified;
    }

    private static string FormatLocation(string location)
    {
        var lineMatch = LineNumberRegex().Match(location);
        if (lineMatch.Success)
        {
            var filePath = location[..lineMatch.Index];
            var lineNumber = lineMatch.Groups[1].Value;
            location = $"{filePath}:{lineNumber}";
        }

        if (location.StartsWith("file://"))
        {
            return AddColumnIfMissing(location);
        }

        var colonIndex = location.LastIndexOf(':');
        if (colonIndex > 0)
        {
            var pathPart = location[..colonIndex];
            var lineAndColumnPart = location[(colonIndex + 1)..];
            
            if (IsAbsolutePath(pathPart))
            {
                string formattedPath;
                if (pathPart.Length > 1 && pathPart[1] == ':')
                {
                    formattedPath = $"file:///{pathPart.Replace('\\', '/')}";
                }
                else if (pathPart.StartsWith("/"))
                {
                    formattedPath = $"file://{pathPart}";
                }
                else
                {
                    return AddColumnIfMissing(location);
                }
                
                if (lineAndColumnPart.Contains(':'))
                {
                    return $"{formattedPath}:{lineAndColumnPart}";
                }
                else
                {
                    return $"{formattedPath}:{lineAndColumnPart}:0";
                }
            }
            
            if (lineAndColumnPart.Contains(':'))
            {
                return location;
            }
            else
            {
                return $"{location}:0";
            }
        }

        if (IsAbsolutePath(location))
        {
            if (location.Length > 1 && location[1] == ':')
            {
                return $"file:///{location.Replace('\\', '/')}:0:0";
            }
            else if (location.StartsWith("/"))
            {
                return $"file://{location}:0:0";
            }
        }

        return location + ":0:0";
    }

    private static bool IsAbsolutePath(string path)
    {
        if (path.Length > 1 && path[1] == ':')
            return true;
        
        if (path.StartsWith("/"))
            return true;
        
        return false;
    }

    private static string AddColumnIfMissing(string location)
    {
        var pathStart = location.StartsWith("file:///") ? 8 : (location.StartsWith("file://") ? 7 : 0);
        var pathPart = location[pathStart..];
        
        var lastColon = pathPart.LastIndexOf(':');
        if (lastColon < 0)
        {
            return location + ":0:0";
        }
        
        var beforeLastColon = pathPart[..lastColon];
        var afterLastColon = pathPart[(lastColon + 1)..];
        
        if (int.TryParse(afterLastColon, out _))
        {
            var secondLastColon = beforeLastColon.LastIndexOf(':');
            if (secondLastColon >= 0)
            {
                var afterSecondLastColon = beforeLastColon[(secondLastColon + 1)..];
                if (int.TryParse(afterSecondLastColon, out _))
                {
                    return location;
                }
            }
            return location + ":0";
        }
        
        return location + ":0:0";
    }

    private static string DemystifyFrame(string content)
    {
        content = OffsetRegex().Replace(content, "").Trim();

        var match = MethodRegex().Match(content);
        if (!match.Success)
        {
            return CleanupSimple(content);
        }

        var type = match.Groups["type"].Value;
        var method = match.Groups["method"].Value;

        var isDisplayClass = DisplayClassRegex().IsMatch(type) || DisplayClassRegex().IsMatch(method);
        var isClosureClass = ClosureClassRegex().IsMatch(type) || ClosureClassRegex().IsMatch(method);
        var isLambda = LambdaRegex().IsMatch(method);
        var isAsync = AsyncStateMachineRegex().IsMatch(method);

        type = CleanTypeName(type);

        string displayMethod;
        var showAsync = false;

        if (isAsync)
        {
            var asyncMatch = AsyncStateMachineRegex().Match(method);
            if (asyncMatch.Success)
            {
                displayMethod = asyncMatch.Groups[1].Value;
                showAsync = true;
            }
            else
            {
                displayMethod = "<anonymous>";
            }
        }
        else if (isLambda)
        {
            var lambdaMatch = LambdaRegex().Match(method);
            if (lambdaMatch.Success)
            {
                var parentMethod = lambdaMatch.Groups[1].Value;
                displayMethod = !string.IsNullOrWhiteSpace(parentMethod) ? parentMethod : "<anonymous>";
            }
            else
            {
                displayMethod = "<anonymous>";
            }
        }
        else if (isDisplayClass || isClosureClass)
        {
            displayMethod = "<anonymous>";
        }
        else
        {
            var localFuncMatch = LocalFunctionRegex().Match(method);
            if (localFuncMatch.Success)
            {
                var parentMethod = localFuncMatch.Groups[1].Value;
                var localFunc = localFuncMatch.Groups[2].Value;
                displayMethod = $"{parentMethod}.{localFunc}";
            }
            else
            {
                displayMethod = CleanMethodName(method);
            }
        }

        var result = new StringBuilder();

        if (showAsync)
        {
            result.Append("async ");
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            result.Append(type).Append('.');
        }

        result.Append(displayMethod);

        return result.ToString();
    }

    private static string CleanupSimple(string content)
    {
        content = DisplayClassRegex().Replace(content, "");
        content = ClosureClassRegex().Replace(content, "");
        content = MultipleDotRegex().Replace(content, ".");
        content = content.Trim('.');
        
        return string.IsNullOrWhiteSpace(content) ? "<anonymous>" : content;
    }

    private static string CleanTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return "";

        var assemblyIndex = typeName.IndexOf(',');
        if (assemblyIndex > 0)
        {
            typeName = typeName[..assemblyIndex];
        }

        typeName = DisplayClassRegex().Replace(typeName, "");
        typeName = ClosureClassRegex().Replace(typeName, "");
        typeName = DemystifyGenerics(typeName);
        typeName = ReplaceSystemTypes(typeName);
        typeName = MultipleDotRegex().Replace(typeName, ".");
        typeName = typeName.Trim('.');

        return typeName;
    }

    private static string CleanMethodName(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return "<anonymous>";

        method = method.Trim();

        method = method switch
        {
            ".ctor" => "constructor",
            ".cctor" => "static constructor",
            _ => method
        };

        if (method.EndsWith("$"))
        {
            method = method[..^1];
        }

        method = method.Replace("..", ".");

        return string.IsNullOrWhiteSpace(method) ? "<anonymous>" : method;
    }

    private static string ReplaceSystemTypes(string typeName)
    {
        return typeName
            .Replace("System.String", "string")
            .Replace("System.Int32", "int")
            .Replace("System.Int64", "long")
            .Replace("System.Boolean", "bool")
            .Replace("System.Double", "double")
            .Replace("System.Single", "float")
            .Replace("System.Decimal", "decimal")
            .Replace("System.Object", "object")
            .Replace("System.Void", "void")
            .Replace("System.Byte", "byte")
            .Replace("System.SByte", "sbyte")
            .Replace("System.Int16", "short")
            .Replace("System.UInt16", "ushort")
            .Replace("System.UInt32", "uint")
            .Replace("System.UInt64", "ulong")
            .Replace("System.Char", "char");
    }

    private static string DemystifyGenerics(string typeName)
    {
        var match = GenericTypeRegex().Match(typeName);
        if (!match.Success)
            return typeName;

        var baseName = match.Groups[1].Value;
        var genericCount = int.Parse(match.Groups[2].Value);
        var genericArgs = match.Groups[3].Value;

        if (string.IsNullOrWhiteSpace(genericArgs))
        {
            var placeholders = Enumerable.Range(0, genericCount)
                .Select(i => $"T{(genericCount > 1 ? (i + 1).ToString() : "")}")
                .ToArray();
            return $"{baseName}<{string.Join(", ", placeholders)}>";
        }

        var args = ParseGenericArguments(genericArgs);
        var demystifiedArgs = args.Select(CleanTypeName).ToArray();

        return $"{baseName}<{string.Join(", ", demystifiedArgs)}>";
    }

    private static List<string> ParseGenericArguments(string args)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var ch in args)
        {
            switch (ch)
            {
                case '[':
                    depth++;
                    if (depth > 1) current.Append(ch);
                    break;
                case ']':
                    depth--;
                    if (depth > 0)
                    {
                        current.Append(ch);
                    }
                    else if (current.Length > 0)
                    {
                        result.Add(current.ToString().Trim());
                        current.Clear();
                    }
                    break;
                case ',' when depth == 0:
                    continue;
                default:
                    if (depth > 0)
                    {
                        current.Append(ch);
                    }
                    break;
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }

        return result;
    }
}

public static class ExceptionFormattingExtensions
{
    public static string ToV8String(this Exception exception)
    {
        return V8StackTraceFormatter.Format(exception);
    }
}