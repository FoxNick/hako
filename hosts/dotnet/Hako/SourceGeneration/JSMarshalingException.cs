namespace HakoJS.SourceGeneration;

public sealed class JSMarshalingException : Exception
{
    public JSMarshalingException(string message) : base(message)
    {
    }

    public JSMarshalingException(string message, Exception inner) : base(message, inner)
    {
    }
}