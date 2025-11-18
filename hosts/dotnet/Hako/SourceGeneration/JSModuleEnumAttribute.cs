namespace HakoJS.SourceGeneration;

/// <summary>
/// Exports a [JSEnum] type from a module. Can be used multiple times to export multiple enums.
/// Regular enums are exported with string values, [Flags] enums are exported with numeric values.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute to include enum definitions in your module's exports.
/// </para>
/// <para>
/// <b>Regular enums</b> marshal as strings for better debuggability:
/// - C# to JS: enum value → "ValueName" string
/// - JS to C#: "ValueName" string → enum value (case-insensitive)
/// </para>
/// <para>
/// <b>[Flags] enums</b> marshal as numbers to support bitwise operations:
/// - C# to JS: enum value → numeric value
/// - JS to C#: numeric value → enum value
/// - JavaScript bitwise operators (|, &amp;, ^, ~) work natively
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [JSEnum]
/// public enum LogLevel
/// {
///     Debug,
///     Info,
///     Warning,
///     Error
/// }
/// 
/// [Flags]
/// [JSEnum]
/// public enum FileAccess
/// {
///     None = 0,
///     Read = 1 &lt;&lt; 0,
///     Write = 1 &lt;&lt; 1,
///     Execute = 1 &lt;&lt; 2,
///     All = Read | Write | Execute
/// }
/// 
/// [JSModule(Name = "io")]
/// [JSModuleEnum(EnumType = typeof(LogLevel), ExportName = "LogLevel")]
/// [JSModuleEnum(EnumType = typeof(FileAccess), ExportName = "FileAccess")]
/// public partial class IOModule
/// {
///     [JSModuleMethod]
///     public static void Log(string message, LogLevel level)
///     {
///         Console.WriteLine($"[{level}] {message}");
///     }
///     
///     [JSModuleMethod]
///     public static void SetPermissions(string path, FileAccess access)
///     {
///         // Bitwise operations work in both C# and JavaScript
///         if ((access &amp; FileAccess.Write) != 0)
///             Console.WriteLine($"Write access granted to {path}");
///     }
/// }
/// 
/// // Register module
/// runtime.ConfigureModules()
///     .WithModule&lt;IOModule&gt;()
///     .Apply();
/// 
/// // In JavaScript:
/// // import { LogLevel, FileAccess, log, setPermissions } from 'io';
/// // 
/// // // Regular enum - uses strings
/// // log("Application started", LogLevel.Info);
/// // 
/// // // Flags enum - uses numbers with bitwise operations
/// // const perms = FileAccess.Read | FileAccess.Write;  // 3
/// // setPermissions("/file.txt", perms);
/// // 
/// // // Check flags
/// // if (perms &amp; FileAccess.Write) {
/// //   console.log("Has write access");
/// // }
/// 
/// // In TypeScript (.d.ts generated):
/// // export const LogLevel = {
/// //   Debug: "Debug",
/// //   Info: "Info",
/// //   Warning: "Warning",
/// //   Error: "Error",
/// // } as const;
/// // export type LogLevel = (typeof LogLevel)[keyof typeof LogLevel];
/// // 
/// // export const FileAccess = {
/// //   None: 0,
/// //   Read: 1,
/// //   Write: 2,
/// //   Execute: 4,
/// //   All: 7,
/// // } as const;
/// // export type FileAccess = (typeof FileAccess)[keyof typeof FileAccess];
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class JSModuleEnumAttribute : Attribute
{
    /// <summary>
    /// The enum type to export. Must have [JSEnum] attribute.
    /// </summary>
    public Type? EnumType { get; set; }
    
    /// <summary>
    /// The export name in JavaScript/TypeScript. Defaults to the enum name if not specified.
    /// </summary>
    public string? ExportName { get; set; }
}