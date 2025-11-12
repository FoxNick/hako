namespace HakoJS.SourceGeneration;

/// <summary>
/// Marks a class for JavaScript binding generation. The class must be declared as partial.
/// </summary>
/// <example>
/// <code>
/// [JSClass(Name = "MyClass")]
/// public partial class MyClass
/// {
///     [JSConstructor]
///     public MyClass(string name) { }
///     
///     [JSProperty]
///     public string Name { get; set; }
///     
///     [JSMethod]
///     public void DoSomething() { }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public sealed class JSClassAttribute : Attribute
{
    /// <summary>
    /// The JavaScript class name. Defaults to the C# class name if not specified.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Exposes a method to JavaScript. Supports async methods and optional parameters.
/// </summary>
/// <example>
/// <code>
/// [JSMethod(Name = "calculate")]
/// public int Add(int a, int b) => a + b;
/// 
/// [JSMethod(Static = true)]
/// public static async Task&lt;string&gt; FetchAsync(string url) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class JSMethodAttribute : Attribute
{
    /// <summary>
    /// The JavaScript method name. Defaults to camelCase of the C# method name.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Validation flag that must match the method's actual static modifier.
    /// </summary>
    public bool Static { get; set; }
}

/// <summary>
/// Exposes a property to JavaScript. Generates getter and optionally setter.
/// </summary>
/// <example>
/// <code>
/// [JSProperty(Name = "firstName")]
/// public string FirstName { get; set; }
/// 
/// [JSProperty(ReadOnly = true)]
/// public int Age { get; set; } // Exposed as read-only in JS
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JSPropertyAttribute : Attribute
{
    /// <summary>
    /// The JavaScript property name. Defaults to camelCase of the C# property name.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Validation flag that must match the property's actual static modifier.
    /// </summary>
    public bool Static { get; set; }
    
    /// <summary>
    /// Forces the property to be read-only in JavaScript even if it has a setter in C#.
    /// </summary>
    public bool ReadOnly { get; set; }
}

/// <summary>
/// Marks a constructor to be exposed to JavaScript. If not specified, a parameterless constructor is used by default.
/// </summary>
/// <example>
/// <code>
/// [JSConstructor]
/// public MyClass(string name, int value)
/// {
///     Name = name;
///     Value = value;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class JSConstructorAttribute : Attribute
{
}

/// <summary>
/// Prevents a method or property marked with [JSMethod] or [JSProperty] from being exposed to JavaScript.
/// </summary>
/// <example>
/// <code>
/// [JSMethod]
/// [JSIgnore] // Not exposed to JS
/// public void InternalMethod() { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public sealed class JSIgnoreAttribute : Attribute
{
}

/// <summary>
/// Marks a record for JavaScript object marshaling. The record must be declared as partial.
/// Generates bidirectional conversion between C# records and plain JavaScript objects.
/// </summary>
/// <example>
/// <code>
/// [JSObject]
/// public partial record EventConfig(
///     string EventName,
///     Action&lt;string&gt; OnEvent,
///     Func&lt;int, bool&gt;? Validator = null
/// );
/// 
/// // C# to JS
/// var config = new EventConfig("onClick", msg => Console.WriteLine(msg), num => num > 0);
/// using var jsValue = config.ToJSValue(realm);
/// 
/// // JS to C# (captures JS functions, must dispose)
/// using var jsConfig = realm.EvalCode("({ eventName: 'click', onEvent: (m) => console.log(m) })").Unwrap();
/// using var csharpConfig = EventConfig.FromJSValue(realm, jsConfig);
/// csharpConfig.OnEvent("test"); // Calls JS function
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class JSObjectAttribute : Attribute
{
    /// <summary>
    /// Indicates if the marshaled JSObject should be immutable. Defaults to true.
    /// </summary>
    public bool ReadOnly { get; set; }

    public JSObjectAttribute(bool readOnly = true)
    {
        ReadOnly = readOnly;
    }
}


/// <summary>
/// Customizes the JavaScript property name for a record parameter.
/// Only applies to records with [JSObject].
/// </summary>
/// <example>
/// <code>
/// [JSObject]
/// public partial record ApiRequest(
///     [JSPropertyName("api_key")] string ApiKey
/// );
/// // JavaScript: { api_key: "..." }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class JSPropertyNameAttribute : Attribute
{
    public string Name { get; }
    
    public JSPropertyNameAttribute(string name)
    {
        Name = name;
    }
}