namespace HakoJS.SourceGeneration;

/// <summary>
/// Exports a static property or field as a module value. Only primitive types and marshalable types are supported.
/// </summary>
/// <example>
/// <code>
/// [JSModule(Name = "constants")]
/// public partial class ConstantsModule
/// {
///     [JSModuleValue]
///     public static readonly double PI = Math.PI;
///     
///     [JSModuleValue(Name = "appVersion")]
///     public static readonly string Version = "1.0.0";
///     
///     [JSModuleValue]
///     public static int MaxConnections { get; } = 100;
///     
///     [JSModuleValue]
///     public static readonly byte[] SecretKey = new byte[] { 0x01, 0x02, 0x03 };
/// }
/// 
/// // In JavaScript:
/// // import { PI, appVersion, maxConnections, secretKey } from 'constants';
/// // console.log(PI);              // 3.14159...
/// // console.log(appVersion);      // "1.0.0"
/// // console.log(maxConnections);  // 100
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class JSModuleValueAttribute : Attribute
{
    /// <summary>
    /// The exported value name in JavaScript. Defaults to camelCase of the member name.
    /// </summary>
    public string? Name { get; set; }
}