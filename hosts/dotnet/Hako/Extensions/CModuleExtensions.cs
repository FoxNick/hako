using HakoJS.Host;
using HakoJS.VM;

namespace HakoJS.Extensions;

/// <summary>
/// Provides extension methods for <see cref="CModule"/> to simplify module configuration and data access.
/// </summary>
public static class CModuleExtensions
{
    /// <summary>
    /// Sets a private value on a module and automatically disposes it, returning the module for chaining.
    /// </summary>
    /// <param name="module">The module to configure.</param>
    /// <param name="value">The JavaScript value to store as private module data.</param>
    /// <returns>The same module instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="module"/> or <paramref name="value"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// Private values are typically used to store module-specific data that should be accessible
    /// during module initialization but not exposed as exports.
    /// </para>
    /// <para>
    /// The value is automatically disposed after being set, preventing memory leaks.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var module = runtime.CreateCModule("config", init => {
    ///     var data = init.GetPrivateValue();
    ///     init.SetExport("default", data);
    /// })
    /// .AddExport("default")
    /// .WithPrivateValue(ctx.ParseJson("{\"key\": \"value\"}", "config"));
    /// </code>
    /// </para>
    /// </remarks>
    public static CModule WithPrivateValue(this CModule module, JSValue value)
    {
        module.SetPrivateValue(value);
        value.Dispose();
        return module;
    }

    /// <summary>
    /// Retrieves the module's private value, converts it using the provided converter function,
    /// and automatically disposes the value.
    /// </summary>
    /// <typeparam name="T">The type to convert the private value to.</typeparam>
    /// <param name="module">The module containing the private value.</param>
    /// <param name="converter">A function that converts the JavaScript value to type <typeparamref name="T"/>.</param>
    /// <returns>The converted value of type <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="module"/> or <paramref name="converter"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// This is a convenience method for accessing and converting module private data
    /// without manually managing disposal of the JavaScript value.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var config = module.UsePrivateValue(value => {
    ///     return value.GetPropertyOrDefault("setting", "default");
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public static T UsePrivateValue<T>(this CModule module, Func<JSValue, T> converter)
    {
        using var value = module.GetPrivateValue();
        return converter(value);
    }
}