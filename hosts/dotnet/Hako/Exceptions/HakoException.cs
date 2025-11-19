namespace HakoJS.Exceptions;

/// <summary>
/// Base exception for HakoJS runtime errors.
/// Formatting is handled by V8StackTraceFormatter.
/// </summary>
public class HakoException : Exception
{
    public HakoException(string message) : base(message)
    {
    }

    public HakoException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

