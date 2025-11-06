using HakoJS.VM;

namespace HakoJS.SourceGeneration;

/// <summary>
/// Base interface for types that can be converted to JavaScript values.
/// </summary>
public interface IJSMarshalable
{
    /// <summary>
    /// Converts this C# instance to a JSValue.
    /// </summary>
    /// <param name="realm">The JavaScript realm to create the value in.</param>
    /// <returns>A JSValue wrapping this instance.</returns>
    JSValue ToJSValue(Realm realm);
}

/// <summary>
/// Interface for types that can be marshaled between C# and JavaScript.
/// Automatically implemented by the source generator for classes with [JSClass].
/// </summary>
/// <typeparam name="TSelf">The implementing class type.</typeparam>
/// <example>
/// <code>
/// [JSClass]
/// public partial class Vector2 { }
/// 
/// // C# to JS
/// var vec = new Vector2();
/// JSValue jsValue = vec.ToJSValue(realm);
/// 
/// // JS to C# (use ToInstance extension method)
/// Vector2? instance = jsValue.ToInstance&lt;Vector2&gt;();
/// </code>
/// </example>
public interface IJSMarshalable<out TSelf> : IJSMarshalable where TSelf : IJSMarshalable<TSelf>
{
    /// <summary>
    /// Converts a JSValue to a C# instance. Throws if the JSValue is invalid.
    /// Use <c>jsValue.ToInstance&lt;T&gt;()</c> extension method instead of calling this directly.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when JSValue doesn't contain a valid instance.</exception>
    static abstract TSelf FromJSValue(Realm realm, JSValue jsValue);
}