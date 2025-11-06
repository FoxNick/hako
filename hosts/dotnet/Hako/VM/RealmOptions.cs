namespace HakoJS.VM;

/// <summary>
/// Provides configuration options for creating JavaScript realms (execution contexts).
/// </summary>
/// <remarks>
/// <para>
/// A realm represents an isolated JavaScript execution environment with its own global object
/// and set of built-in objects. Use this class to control which JavaScript features are available
/// and to set resource limits.
/// </para>
/// <para>
/// Example:
/// <code>
/// var options = new RealmOptions
/// {
///     Intrinsics = RealmOptions.RealmIntrinsics.Standard,
///     MaxStackSizeBytes = 1024 * 1024 // 1MB stack
/// };
/// 
/// var realm = runtime.CreateRealm(options);
/// </code>
/// </para>
/// </remarks>
public class RealmOptions
{
    /// <summary>
    /// Defines the set of built-in JavaScript objects and features available in a realm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Intrinsics control which JavaScript APIs are available. You can use predefined sets
    /// like <see cref="Standard"/> for common features, or combine individual flags to create
    /// a custom set.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// // Standard JavaScript features
    /// var standard = RealmIntrinsics.Standard;
    /// 
    /// // Custom set with BigInt support
    /// var custom = RealmIntrinsics.Standard | RealmIntrinsics.BigInt;
    /// 
    /// // Minimal set
    /// var minimal = RealmIntrinsics.BaseObjects | RealmIntrinsics.Json;
    /// </code>
    /// </para>
    /// </remarks>
    [Flags]
    public enum RealmIntrinsics
    {
        /// <summary>
        /// No intrinsics - creates a nearly empty realm.
        /// </summary>
        /// <remarks>
        /// Use this for highly restricted environments. Most JavaScript code will not work
        /// without at least <see cref="BaseObjects"/>.
        /// </remarks>
        None = 0,

        /// <summary>
        /// Core JavaScript objects (Object, Array, Function, String, Number, Boolean, etc.).
        /// </summary>
        /// <remarks>
        /// Essential for any JavaScript code. Includes fundamental constructors and prototype methods.
        /// </remarks>
        BaseObjects = 1 << 0,

        /// <summary>
        /// Date object for working with dates and times.
        /// </summary>
        Date = 1 << 1,

        /// <summary>
        /// The eval() function for evaluating JavaScript code dynamically.
        /// </summary>
        /// <remarks>
        /// Warning: eval() can execute arbitrary code. Disable this in security-sensitive environments.
        /// </remarks>
        Eval = 1 << 2,

        /// <summary>
        /// String.prototype.normalize() for Unicode normalization.
        /// </summary>
        StringNormalize = 1 << 3,

        /// <summary>
        /// RegExp object for regular expressions.
        /// </summary>
        RegExp = 1 << 4,

        /// <summary>
        /// RegExp compiler for compiling regular expressions.
        /// </summary>
        /// <remarks>
        /// Advanced feature for pre-compiled regular expressions. Not needed for normal RegExp usage.
        /// </remarks>
        RegExpCompiler = 1 << 5,

        /// <summary>
        /// JSON object with parse() and stringify() methods.
        /// </summary>
        Json = 1 << 6,

        /// <summary>
        /// Proxy object for intercepting and customizing object operations.
        /// </summary>
        Proxy = 1 << 7,

        /// <summary>
        /// Map, Set, WeakMap, and WeakSet collections.
        /// </summary>
        MapSet = 1 << 8,

        /// <summary>
        /// Typed arrays (Uint8Array, Int32Array, Float64Array, etc.) and ArrayBuffer.
        /// </summary>
        /// <remarks>
        /// Required for binary data manipulation and interop with native buffers.
        /// </remarks>
        TypedArrays = 1 << 9,

        /// <summary>
        /// Promise object for asynchronous operations.
        /// </summary>
        /// <remarks>
        /// Essential for async/await and modern asynchronous JavaScript patterns.
        /// </remarks>
        Promise = 1 << 10,

        /// <summary>
        /// BigInt type for arbitrary-precision integers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only available if QuickJS was compiled with BigNum support.
        /// Check <c>runtime.Utils.GetBuildInfo().HasBignum</c> to verify availability.
        /// </para>
        /// </remarks>
        BigInt = 1 << 11,

        /// <summary>
        /// BigFloat type for arbitrary-precision floating-point numbers.
        /// </summary>
        /// <remarks>
        /// Requires BigNum support in QuickJS build. This is a QuickJS extension, not standard JavaScript.
        /// </remarks>
        BigFloat = 1 << 12,

        /// <summary>
        /// BigDecimal type for arbitrary-precision decimal numbers.
        /// </summary>
        /// <remarks>
        /// Requires BigNum support in QuickJS build. This is a QuickJS extension, not standard JavaScript.
        /// </remarks>
        BigDecimal = 1 << 13,

        /// <summary>
        /// Operator overloading support for custom types.
        /// </summary>
        /// <remarks>
        /// This is a QuickJS extension allowing custom operators on objects.
        /// </remarks>
        OperatorOverloading = 1 << 14,

        /// <summary>
        /// Extended BigNum features and utilities.
        /// </summary>
        /// <remarks>
        /// Additional BigNum functionality beyond basic BigInt/BigFloat/BigDecimal.
        /// </remarks>
        BignumExt = 1 << 15,

        /// <summary>
        /// Performance measurement APIs (performance.now(), etc.).
        /// </summary>
        Performance = 1 << 16,

        /// <summary>
        /// Crypto APIs for cryptographic operations.
        /// </summary>
        /// <remarks>
        /// Provides basic cryptographic functions. Not a full implementation of Web Crypto API.
        /// </remarks>
        Crypto = 1 << 17,

        /// <summary>
        /// Standard JavaScript features for typical applications.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Includes: BaseObjects, Date, Eval, StringNormalize, RegExp, Json, Proxy, MapSet,
        /// TypedArrays, and Promise.
        /// </para>
        /// <para>
        /// This is the recommended default for most applications, providing a complete
        /// ES2015+ JavaScript environment without experimental extensions.
        /// </para>
        /// </remarks>
        Standard = BaseObjects | Date | Eval | StringNormalize | RegExp | Json |
                   Proxy | MapSet | TypedArrays | Promise,

        /// <summary>
        /// All available JavaScript features and extensions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Includes all standard features plus BigNum types, operator overloading,
        /// performance APIs, and crypto APIs.
        /// </para>
        /// <para>
        /// Some features (BigInt, BigFloat, BigDecimal) may not be available if QuickJS
        /// was not compiled with BigNum support.
        /// </para>
        /// </remarks>
        All = BaseObjects | Date | Eval | StringNormalize | RegExp | RegExpCompiler |
              Json | Proxy | MapSet | TypedArrays | Promise | BigInt | BigFloat |
              BigDecimal | OperatorOverloading | BignumExt | Performance | Crypto
    }

    /// <summary>
    /// Gets or sets which JavaScript built-in objects and features are available in the realm.
    /// </summary>
    /// <value>
    /// The set of intrinsics to enable. Default is <see cref="RealmIntrinsics.Standard"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// Intrinsics determine the JavaScript APIs available in the realm. Use <see cref="RealmIntrinsics.Standard"/>
    /// for typical applications, or customize the set for specialized environments.
    /// </para>
    /// <para>
    /// Reducing intrinsics can improve security and reduce memory usage by disabling unnecessary features.
    /// </para>
    /// </remarks>
    public RealmIntrinsics Intrinsics { get; set; } = RealmIntrinsics.Standard;

    /// <summary>
    /// Gets or sets an existing realm pointer to wrap instead of creating a new realm.
    /// </summary>
    /// <value>
    /// The pointer to an existing QuickJS context, or <c>null</c> to create a new realm.
    /// </value>
    /// <remarks>
    /// <para>
    /// This is an advanced option for wrapping existing QuickJS contexts created outside
    /// of HakoJS. Most users should leave this as <c>null</c> to create a new realm.
    /// </para>
    /// <para>
    /// When set, <see cref="Intrinsics"/> is ignored since the context already exists.
    /// </para>
    /// </remarks>
    public int? RealmPointer { get; set; }
    
}