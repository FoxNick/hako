namespace HakoJS.VM;

/// <summary>
/// Flags for controlling which properties are enumerated when retrieving object property names.
/// </summary>
/// <remarks>
/// <para>
/// These flags can be combined using bitwise OR to specify multiple criteria for property enumeration.
/// Use this with methods like <see cref="JSValue.GetOwnPropertyNames"/> to filter which properties are returned.
/// </para>
/// <para>
/// Example:
/// <code>
/// // Get only enumerable string properties
/// var flags = PropertyEnumFlags.String | PropertyEnumFlags.Enumerable;
/// var properties = obj.GetOwnPropertyNames(flags);
/// </code>
/// </para>
/// </remarks>
[Flags]
public enum PropertyEnumFlags
{
    /// <summary>
    /// Include string-keyed properties.
    /// </summary>
    String = 1 << 0,

    /// <summary>
    /// Include symbol-keyed properties.
    /// </summary>
    Symbol = 1 << 1,

    /// <summary>
    /// Include private fields (typically class private fields in JavaScript).
    /// </summary>
    Private = 1 << 2,

    /// <summary>
    /// Include only enumerable properties (those that appear in for-in loops).
    /// </summary>
    Enumerable = 1 << 4,

    /// <summary>
    /// Include only non-enumerable properties.
    /// </summary>
    NonEnumerable = 1 << 5,

    /// <summary>
    /// Include only configurable properties (those that can be deleted or modified).
    /// </summary>
    Configurable = 1 << 6,

    /// <summary>
    /// Include only non-configurable properties.
    /// </summary>
    NonConfigurable = 1 << 7,

    /// <summary>
    /// Include numeric properties (array indices).
    /// </summary>
    Number = 1 << 14,

    /// <summary>
    /// Use ECMAScript-compliant enumeration order.
    /// </summary>
    Compliant = 1 << 15
}