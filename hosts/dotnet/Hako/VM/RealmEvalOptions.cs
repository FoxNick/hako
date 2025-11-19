using HakoJS.Extensions;

namespace HakoJS.VM;

/// <summary>
/// Specifies the evaluation type for JavaScript code execution.
/// </summary>
/// <remarks>
/// <para>
/// The evaluation type determines how the code is interpreted and what scope it has access to.
/// Different types affect variable scoping, module imports, and the 'this' binding.
/// </para>
/// </remarks>
public enum EvalType
{
    /// <summary>
    /// Evaluates code in the global scope with access to global variables.
    /// </summary>
    /// <remarks>
    /// This is the default and most common evaluation type. Code executes as if typed
    /// at the top level of a script file.
    /// </remarks>
    Global,

    /// <summary>
    /// Evaluates code as an ES6 module with its own scope and import/export support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Module evaluation creates a separate module scope. Variables declared with
    /// <c>let</c>, <c>const</c>, or <c>class</c> don't pollute the global scope.
    /// </para>
    /// <para>
    /// Modules can use <c>import</c> and <c>export</c> statements.
    /// </para>
    /// </remarks>
    Module,

    /// <summary>
    /// Evaluates code as a direct eval call, inheriting the caller's scope.
    /// </summary>
    /// <remarks>
    /// Direct eval can access and modify variables in the calling scope.
    /// This is similar to calling <c>eval()</c> directly in JavaScript.
    /// </remarks>
    Direct,

    /// <summary>
    /// Evaluates code as an indirect eval call with its own scope.
    /// </summary>
    /// <remarks>
    /// Indirect eval executes in its own scope and cannot access the caller's local variables.
    /// This is similar to <c>(0, eval)(code)</c> in JavaScript.
    /// </remarks>
    Indirect
}

/// <summary>
/// Flags for controlling JavaScript code evaluation behavior.
/// </summary>
/// <remarks>
/// These flags correspond to QuickJS internal eval flags and can be combined using bitwise OR.
/// Most users should use <see cref="RealmEvalOptions"/> instead of working with these flags directly.
/// </remarks>
[Flags]
public enum EvalFlags
{
    /// <summary>
    /// Evaluate as global code (default).
    /// </summary>
    Global = 0 << 0,

    /// <summary>
    /// Evaluate as an ES6 module.
    /// </summary>
    Module = 1 << 0,

    /// <summary>
    /// Evaluate as a direct eval call.
    /// </summary>
    Direct = 2 << 0,

    /// <summary>
    /// Evaluate as an indirect eval call.
    /// </summary>
    Indirect = 3 << 0,

    /// <summary>
    /// Mask for extracting the evaluation type bits.
    /// </summary>
    TypeMask = 3 << 0,

    /// <summary>
    /// Enable strict mode for the evaluation.
    /// </summary>
    /// <remarks>
    /// Strict mode applies stricter parsing and error handling rules,
    /// such as disallowing undeclared variables and deprecated features.
    /// </remarks>
    Strict = 1 << 3,

    /// <summary>
    /// Compile the code without executing it.
    /// </summary>
    /// <remarks>
    /// This is useful for syntax checking or pre-compiling code for later execution.
    /// </remarks>
    CompileOnly = 1 << 5,

    /// <summary>
    /// Add a barrier in the backtrace for error stack traces.
    /// </summary>
    /// <remarks>
    /// This prevents the eval call from appearing in JavaScript error stack traces,
    /// which can be useful for cleaner error messages.
    /// </remarks>
    BacktraceBarrier = 1 << 6,

    /// <summary>
    /// Enable top-level await support in global code.
    /// </summary>
    /// <remarks>
    /// When set with <see cref="Global"/>, allows using <c>await</c> at the top level
    /// and automatically wraps the result in a Promise.
    /// </remarks>
    Async = 1 << 7,

    /// <summary>
    /// Strip TypeScript type annotations before evaluation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, TypeScript type annotations (types, interfaces, enums, etc.) are
    /// automatically removed from the source code before evaluation.
    /// </para>
    /// <para>
    /// This flag is also automatically enabled when the filename has a TypeScript
    /// extension (.ts, .mts, .tsx, .mtsx).
    /// </para>
    /// <para>
    /// Note: Some advanced TypeScript features (enums, namespaces, parameter properties)
    /// are not supported and will cause errors.
    /// </para>
    /// </remarks>
    StripTypes = 1 << 8
}

/// <summary>
/// Provides options for controlling how JavaScript code is evaluated in a realm.
/// </summary>
/// <remarks>
/// <para>
/// Use this class to configure evaluation behavior such as strict mode, async execution,
/// module imports, and compilation settings.
/// </para>
/// <para>
/// Example:
/// <code>
/// var options = new RealmEvalOptions
/// {
///     Type = EvalType.Module,
///     Strict = true,
///     FileName = "mymodule.js"
/// };
/// 
/// using var result = realm.EvalCode("export const x = 42;", options);
/// </code>
/// </para>
/// </remarks>
public class RealmEvalOptions
{
    /// <summary>
    /// Gets or sets the evaluation type that determines scope and execution context.
    /// </summary>
    /// <value>
    /// The evaluation type. Default is <see cref="EvalType.Global"/>.
    /// </value>
    public EvalType Type { get; set; } = EvalType.Global;

    /// <summary>
    /// Gets or sets whether to enable strict mode for the evaluation.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable strict mode; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// Strict mode enforces stricter parsing and error handling rules in JavaScript.
    /// </remarks>
    public bool Strict { get; set; }

    /// <summary>
    /// Gets or sets whether to compile the code without executing it.
    /// </summary>
    /// <value>
    /// <c>true</c> to only compile the code; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// Use this for syntax checking or pre-compilation. The result will be a compiled
    /// function or module that can be executed later.
    /// </remarks>
    public bool CompileOnly { get; set; }

    /// <summary>
    /// Gets or sets whether to add a backtrace barrier for cleaner error stack traces.
    /// </summary>
    /// <value>
    /// <c>true</c> to hide the eval call from stack traces; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    public bool BacktraceBarrier { get; set; }

    /// <summary>
    /// Gets or sets whether to enable top-level await support.
    /// </summary>
    /// <value>
    /// <c>true</c> to allow top-level await; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// This can only be used with <see cref="EvalType.Global"/>. When enabled, the code
    /// can use <c>await</c> at the top level, and the evaluation result is automatically
    /// wrapped in a Promise.
    /// </para>
    /// <para>
    /// QuickJS wraps async global results in <c>{ value: result }</c>, which is automatically
    /// unwrapped by <see cref="RealmExtensions.EvalAsync"/>.
    /// </para>
    /// </remarks>
    public bool Async { get; set; }

    /// <summary>
    /// Gets or sets whether to strip TypeScript type annotations before evaluation.
    /// </summary>
    /// <value>
    /// <c>true</c> to strip TypeScript types; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// When enabled, TypeScript type annotations are automatically removed from the source
    /// code before evaluation, allowing you to run TypeScript code directly.
    /// </para>
    /// <para>
    /// This is also automatically enabled when the <see cref="FileName"/> has a TypeScript
    /// extension (.ts, .mts, .tsx, .mtsx).
    /// </para>
    /// <para>
    /// Note: Some TypeScript features require runtime support and cannot be stripped:
    /// <list type="bullet">
    /// <item><description>Enums (use const objects instead)</description></item>
    /// <item><description>Namespaces with runtime values (use ES modules instead)</description></item>
    /// <item><description>Parameter properties (use explicit assignments instead)</description></item>
    /// <item><description>Legacy module syntax (use ES modules instead)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var options = new RealmEvalOptions
    /// {
    ///     StripTypes = true,
    ///     FileName = "example.ts"
    /// };
    /// 
    /// using var result = realm.EvalCode("let x: number = 42; x + 1", options);
    /// Console.WriteLine(result.GetInt32()); // 43
    /// </code>
    /// </para>
    /// </remarks>
    public bool StripTypes { get; set; }

    /// <summary>
    /// Gets or sets the filename to use in error messages and stack traces.
    /// </summary>
    /// <value>
    /// The filename. Default is "eval".
    /// </value>
    /// <remarks>
    /// <para>
    /// This appears in JavaScript error stack traces and helps identify where code came from.
    /// If the filename doesn't start with "file://", it will be automatically prefixed.
    /// </para>
    /// <para>
    /// When the filename ends with a TypeScript extension (.ts, .mts, .tsx, .mtsx),
    /// type stripping is automatically enabled unless explicitly disabled.
    /// </para>
    /// </remarks>
    public string FileName { get; set; } = "eval";

    /// <summary>
    /// Gets or sets whether to automatically detect if code should be evaluated as a module.
    /// </summary>
    /// <value>
    /// <c>true</c> to auto-detect modules; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// When enabled, the evaluator checks if the code contains <c>import</c> or <c>export</c>
    /// statements and automatically treats it as a module if so. Module file extensions
    /// (.mjs, .mts, .mtsx) also trigger module evaluation.
    /// </para>
    /// <para>
    /// This overrides the <see cref="Type"/> setting if module syntax is detected.
    /// </para>
    /// </remarks>
    public bool DetectModule { get; set; }

    /// <summary>
    /// Converts the options to QuickJS internal evaluation flags.
    /// </summary>
    /// <returns>The combined <see cref="EvalFlags"/> for the configured options.</returns>
    /// <exception cref="InvalidOperationException">
    /// The <see cref="Async"/> flag is set with a <see cref="Type"/> other than <see cref="EvalType.Global"/>.
    /// </exception>
    /// <remarks>
    /// This method is used internally by the realm evaluation methods.
    /// </remarks>
    public EvalFlags ToFlags()
    {
        if (Async && Type != EvalType.Global)
            throw new InvalidOperationException(
                "Async flag is only allowed with EvalType.Global");

        var flags = Type switch
        {
            EvalType.Global => EvalFlags.Global,
            EvalType.Module => EvalFlags.Module,
            EvalType.Direct => EvalFlags.Direct,
            EvalType.Indirect => EvalFlags.Indirect,
            _ => EvalFlags.Global
        };

        if (Strict) flags |= EvalFlags.Strict;
        if (CompileOnly) flags |= EvalFlags.CompileOnly;
        if (BacktraceBarrier) flags |= EvalFlags.BacktraceBarrier;
        if (Async) flags |= EvalFlags.Async;
        if (StripTypes) flags |= EvalFlags.StripTypes;

        return flags;
    }
}