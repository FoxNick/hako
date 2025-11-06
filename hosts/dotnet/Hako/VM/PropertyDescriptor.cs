namespace HakoJS.VM;

/// <summary>
/// Represents a JavaScript property descriptor used for defining object properties with specific characteristics.
/// </summary>
/// <remarks>
/// <para>
/// Property descriptors control how properties behave on JavaScript objects. A descriptor can be
/// a data descriptor (with a value) or an accessor descriptor (with getter/setter), but not both.
/// </para>
/// <para>
/// This class is typically used with <c>Object.defineProperty</c> equivalent operations to define
/// properties with precise control over their enumeration, configuration, and access patterns.
/// </para>
/// </remarks>
public sealed class PropertyDescriptor
{
    /// <summary>
    /// Gets or sets the value associated with the property (data descriptor).
    /// </summary>
    /// <remarks>
    /// Cannot be used together with <see cref="Get"/> or <see cref="Set"/>.
    /// </remarks>
    public JSValue? Value { get; set; }

    /// <summary>
    /// Gets or sets whether the property can be deleted or its attributes changed.
    /// </summary>
    /// <value>
    /// <c>true</c> if the property descriptor may be changed and the property may be deleted;
    /// otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    public bool? Configurable { get; set; }

    /// <summary>
    /// Gets or sets whether the property shows up during enumeration of properties.
    /// </summary>
    /// <value>
    /// <c>true</c> if the property shows up in for-in loops and <c>Object.keys()</c>;
    /// otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    public bool? Enumerable { get; set; }

    /// <summary>
    /// Gets or sets the getter function for the property (accessor descriptor).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cannot be used together with <see cref="Value"/> or <see cref="Writable"/>.
    /// </para>
    /// <para>
    /// When a property is accessed, this function is called to retrieve the value.
    /// </para>
    /// </remarks>
    public JSValue? Get { get; set; }

    /// <summary>
    /// Gets or sets the setter function for the property (accessor descriptor).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cannot be used together with <see cref="Value"/> or <see cref="Writable"/>.
    /// </para>
    /// <para>
    /// When a property is assigned a value, this function is called with the new value.
    /// </para>
    /// </remarks>
    public JSValue? Set { get; set; }

    /// <summary>
    /// Gets or sets whether the property value can be changed (data descriptor).
    /// </summary>
    /// <value>
    /// <c>true</c> if the value can be changed with an assignment operator;
    /// otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// Cannot be used together with <see cref="Get"/> or <see cref="Set"/>.
    /// </remarks>
    public bool? Writable { get; set; }
}