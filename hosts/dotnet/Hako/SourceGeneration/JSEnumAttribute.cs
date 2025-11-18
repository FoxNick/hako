namespace HakoJS.SourceGeneration;

/// <summary>
/// Defines the casing style for enum property names in JavaScript/TypeScript.
/// </summary>
public enum NameCasing
{
    /// <summary>
    /// Keep the original C# name unchanged.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// camelCase - first letter lowercase, subsequent words capitalized.
    /// Example: myEnumValue
    /// </summary>
    Camel,
    
    /// <summary>
    /// PascalCase - first letter of each word capitalized.
    /// Example: MyEnumValue
    /// </summary>
    Pascal,
    
    /// <summary>
    /// snake_case - all lowercase with underscores between words.
    /// Example: my_enum_value
    /// </summary>
    Snake,
    
    /// <summary>
    /// SCREAMING_SNAKE_CASE - all uppercase with underscores between words.
    /// Example: MY_ENUM_VALUE
    /// </summary>
    ScreamingSnake,
    
    /// <summary>
    /// lowercase - all lowercase, no separators.
    /// Example: myenumvalue
    /// </summary>
    Lower
}

/// <summary>
/// Defines simple casing transformations for enum values.
/// </summary>
public enum ValueCasing
{
    /// <summary>
    /// Keep the original C# value name unchanged.
    /// Example: MyValue stays MyValue
    /// </summary>
    Original = 0,
    
    /// <summary>
    /// Convert to lowercase.
    /// Example: MyValue becomes myvalue
    /// </summary>
    Lower,
    
    /// <summary>
    /// Convert to uppercase.
    /// Example: MyValue becomes MYVALUE
    /// </summary>
    Upper
}

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
    
    /// <summary>
    /// Controls the casing of enum property names in the generated TypeScript.
    /// Default is None (keeps original C# naming).
    /// </summary>
    public NameCasing Casing { get; set; } = NameCasing.None;
    
    /// <summary>
    /// Controls the casing style of enum values when marshaling to JavaScript.
    /// Default is Original (keeps original C# value names).
    /// </summary>
    public ValueCasing ValueCasing { get; set; } = ValueCasing.Original;
}