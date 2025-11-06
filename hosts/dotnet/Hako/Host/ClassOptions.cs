using HakoJS.VM;

namespace HakoJS.Host;

/// <summary>
/// Defines configuration options for creating JavaScript classes, including methods, properties, and lifecycle hooks.
/// </summary>
/// <remarks>
/// <para>
/// This class is used when creating JavaScript classes via <see cref="JSClass"/> to specify
/// instance and static members, property descriptors, and garbage collection callbacks.
/// </para>
/// <para>
/// Example:
/// <code>
/// var options = new ClassOptions
/// {
///     Methods = new Dictionary&lt;string, Func&lt;Realm, JSValue, JSValue[], JSValue?&gt;&gt;
///     {
///         ["greet"] = (ctx, thisArg, args) => ctx.NewString("Hello!")
///     },
///     Properties = new Dictionary&lt;string, ClassOptions.PropertyDefinition&gt;
///     {
///         ["name"] = new ClassOptions.PropertyDefinition
///         {
///             Name = "name",
///             Getter = (ctx, thisArg, args) => ctx.NewString("MyClass")
///         }
///     }
/// };
/// </code>
/// </para>
/// </remarks>
public class ClassOptions
{
    /// <summary>
    /// Gets or sets the dictionary of instance methods for the class.
    /// </summary>
    /// <value>
    /// A dictionary mapping method names to their implementation functions, or <c>null</c> if no instance methods.
    /// </value>
    /// <remarks>
    /// Instance methods are added to the class prototype and are available on all instances.
    /// </remarks>
    public Dictionary<string, JSFunction>? Methods { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of static methods for the class.
    /// </summary>
    /// <value>
    /// A dictionary mapping method names to their implementation functions, or <c>null</c> if no static methods.
    /// </value>
    /// <remarks>
    /// Static methods are added to the constructor function itself and are not available on instances.
    /// In JavaScript, these are accessed as <c>ClassName.methodName()</c>.
    /// </remarks>
    public Dictionary<string, JSFunction>? StaticMethods { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of instance property descriptors for the class.
    /// </summary>
    /// <value>
    /// A dictionary mapping property names to their descriptors, or <c>null</c> if no instance properties.
    /// </value>
    /// <remarks>
    /// Instance properties are added to the class prototype with the specified getter/setter functions
    /// and enumeration/configuration flags.
    /// </remarks>
    public Dictionary<string, PropertyDefinition>? Properties { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of static property descriptors for the class.
    /// </summary>
    /// <value>
    /// A dictionary mapping property names to their descriptors, or <c>null</c> if no static properties.
    /// </value>
    /// <remarks>
    /// Static properties are added to the constructor function itself.
    /// In JavaScript, these are accessed as <c>ClassName.propertyName</c>.
    /// </remarks>
    public Dictionary<string, PropertyDefinition>? StaticProperties { get; set; }

    /// <summary>
    /// Gets or sets the finalizer callback invoked when a class instance is garbage collected.
    /// </summary>
    /// <value>
    /// A callback function that receives the runtime, opaque data, and class ID, or <c>null</c> if no finalizer.
    /// </value>
    /// <remarks>
    /// <para>
    /// Finalizers are called on the garbage collection thread when JavaScript objects are collected.
    /// Use this to clean up native resources or remove instances from tracking dictionaries.
    /// </para>
    /// <para>
    /// Warning: Finalizers run on the GC thread, not the main event loop thread.
    /// </para>
    /// </remarks>
    public ClassFinalizerHandler? Finalizer { get; set; }

    /// <summary>
    /// Gets or sets the GC mark callback for tracing reachable JavaScript values.
    /// </summary>
    /// <value>
    /// A callback function that receives the runtime and opaque data, or <c>null</c> if no GC mark handler.
    /// </value>
    /// <remarks>
    /// <para>
    /// The GC mark handler is called during garbage collection to mark JavaScript values
    /// that are reachable from native objects. This prevents premature collection of values
    /// held by native code.
    /// </para>
    /// <para>
    /// This is an advanced feature typically only needed when native objects hold strong
    /// references to JavaScript values that aren't otherwise visible to the garbage collector.
    /// </para>
    /// </remarks>
    public ClassGcMarkHandler? GCMark { get; set; }

    /// <summary>
    /// Defines a property descriptor for JavaScript object properties.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Property descriptors control the behavior of properties on JavaScript objects,
    /// including their getter/setter functions and enumeration/configuration flags.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// new PropertyDefinition
    /// {
    ///     Name = "fullName",
    ///     Getter = (ctx, thisArg, args) =>
    ///     {
    ///         var firstName = thisArg.GetProperty("firstName").AsString();
    ///         var lastName = thisArg.GetProperty("lastName").AsString();
    ///         return ctx.NewString($"{firstName} {lastName}");
    ///     },
    ///     Setter = (ctx, thisArg, args) =>
    ///     {
    ///         var parts = args[0].AsString().Split(' ');
    ///         thisArg.SetProperty("firstName", parts[0]);
    ///         thisArg.SetProperty("lastName", parts[1]);
    ///         return null;
    ///     },
    ///     Enumerable = true,
    ///     Configurable = false
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public class PropertyDefinition
    {
        /// <summary>
        /// Gets or sets the name of the property.
        /// </summary>
        /// <value>The property name as it appears in JavaScript.</value>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the getter function for the property.
        /// </summary>
        /// <value>
        /// A function that returns the property value when accessed, or <c>null</c> for write-only properties.
        /// </value>
        /// <remarks>
        /// The getter receives the realm, the 'this' value, and an empty arguments array.
        /// Return <c>null</c> to indicate an error (which throws in JavaScript).
        /// </remarks>
        public JSFunction? Getter { get; init; }

        /// <summary>
        /// Gets or sets the setter function for the property.
        /// </summary>
        /// <value>
        /// A function called when the property is assigned a value, or <c>null</c> for read-only properties.
        /// </value>
        /// <remarks>
        /// <para>
        /// The setter receives the realm, the 'this' value, and an arguments array containing
        /// the new value at index 0.
        /// </para>
        /// <para>
        /// Return <c>null</c> for success, or return an error value to throw an exception.
        /// </para>
        /// </remarks>
        public JSFunction? Setter { get; init; }

        /// <summary>
        /// Gets or sets whether the property appears during enumeration.
        /// </summary>
        /// <value>
        /// <c>true</c> if the property shows up in for-in loops and <c>Object.keys()</c>;
        /// otherwise, <c>false</c>. Default is <c>true</c>.
        /// </value>
        public bool Enumerable { get; init; } = true;

        /// <summary>
        /// Gets or sets whether the property can be deleted or its descriptor modified.
        /// </summary>
        /// <value>
        /// <c>true</c> if the property descriptor may be changed and the property may be deleted;
        /// otherwise, <c>false</c>. Default is <c>true</c>.
        /// </value>
        public bool Configurable { get; init; } = true;
    }
}