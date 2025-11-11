namespace HakoJS.SourceGeneration;

/// <summary>
/// Marks an enum for JavaScript marshaling.
/// Regular enums marshal as strings, [Flags] enums marshal as numbers.
/// </summary>
[AttributeUsage(AttributeTargets.Enum)]
public class JSEnumAttribute : Attribute
{
    /// <summary>
    /// Optional JavaScript name for the enum. If not specified, uses the enum name.
    /// </summary>
    public string? Name { get; set; }
}