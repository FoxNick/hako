namespace HakoJS.Exceptions;

public class HakoUseAfterFreeException : HakoException
{
    public HakoUseAfterFreeException(string message) : base(message)
    {
    }

    public HakoUseAfterFreeException(string resourceType, int pointer)
        : base($"Attempted to use {resourceType} at pointer {pointer} after it has been freed")
    {
    }
}