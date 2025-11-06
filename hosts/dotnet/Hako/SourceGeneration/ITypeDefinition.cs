namespace HakoJS.SourceGeneration;


public interface IDefinitelyTyped<out TSelf>
{
    static abstract string TypeDefinition { get; }
}