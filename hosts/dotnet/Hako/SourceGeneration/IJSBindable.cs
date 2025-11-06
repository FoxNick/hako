using HakoJS.VM;

namespace HakoJS.SourceGeneration;

/// <summary>
/// Interface for types that can be bound to JavaScript.
/// Automatically implemented by the source generator for classes with [JSClass].
/// </summary>
/// <typeparam name="TSelf">The implementing class type.</typeparam>
/// <remarks>
/// You don't implement this interface manually - the source generator creates the implementation.
/// </remarks>
/// <example>
/// <code>
/// [JSClass]
/// public partial class Vector2 
/// { 
///     public float X { get; set; }
///     public float Y { get; set; }
/// }
/// 
/// // Register with runtime
/// realm.RegisterClass&lt;Vector2&gt;();
/// 
/// // Marshal C# to JS
/// var vector = new Vector2 { X = 1, Y = 2 };
/// JSValue jsValue = vector.ToJSValue(realm);
/// 
/// // Marshal JS to C#
/// Vector2? instance = jsValue.ToInstance&lt;Vector2&gt;();
/// </code>
/// </example>
public interface IJSBindable<out TSelf> where TSelf : class, IJSBindable<TSelf>
{
    /// <summary>
    /// Gets the fully qualified type key used for runtime registration (e.g., "MyApp.Vector2").
    /// </summary>
    static abstract string TypeKey { get; }
    
    /// <summary>
    /// Creates the JSClass prototype for this type. Called internally by <c>realm.RegisterClass&lt;T&gt;()</c>.
    /// </summary>
    /// <param name="realm">The JavaScript realm to create the prototype in.</param>
    /// <returns>The created JSClass with all bindings configured.</returns>
    static abstract JSClass CreatePrototype(Realm realm);

    /// <summary>
    /// Retrieves the C# instance associated with a JSValue, or null if invalid.
    /// Use <c>jsValue.ToInstance&lt;T&gt;()</c> extension method instead of calling this directly.
    /// </summary>
    static abstract TSelf? GetInstanceFromJS(JSValue jsValue);

    /// <summary>
    /// Removes the C# instance associated with a JSValue from internal tracking.
    /// </summary>
    static abstract bool RemoveInstance(JSValue jsValue);
}