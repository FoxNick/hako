using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.VM;

namespace HakoJS.SourceGeneration;

/// <summary>
/// Provides automatic marshaling between JavaScript values and C# types.
/// Call <c>runtime.RegisterObjectConverters()</c> during startup to register all generated converters.
/// </summary>
public static class JSMarshalingRegistry
{
    /// <summary>
    /// Maps JSObject type IDs to their reification converters (JS → .NET).
    /// </summary>
    private static readonly ConcurrentDictionary<uint, Func<Realm, JSValue, object>> ObjectReifiers = new();

    /// <summary>
    /// Maps JSClass IDs to their reification converters (JS → .NET).
    /// </summary>
    private static readonly ConcurrentDictionary<uint, Func<JSValue, object?>> ClassReifiers = new();

    /// <summary>
    /// The registered projector function for converting .NET objects to JavaScript (if available).
    /// </summary>
    private static Func<Realm, object, JSValue?>? _projector;

    /// <summary>
    /// Registers a JSObject reifier (JS → .NET converter). Called by generated code during RegisterObjectConverters().
    /// </summary>
    /// <param name="typeId">The type ID hash.</param>
    /// <param name="reifier">The reifier function.</param>
    /// <exception cref="HakoException">When the reifier cannot be added to the converter map</exception>
    public static void RegisterObjectReifier(uint typeId, Func<Realm, JSValue, object> reifier)
    {
        if (!ObjectReifiers.TryAdd(typeId, reifier))
        {
            throw new HakoException($"Cannot register object reifier for type ID {typeId}. A reifier for this type is already registered.");
        }
    }

    /// <summary>
    /// Registers a JSClass reifier (JS → .NET converter). Called automatically when a class prototype is created.
    /// </summary>
    /// <typeparam name="T">The JSClass type.</typeparam>
    /// <param name="classId">The runtime-assigned class ID.</param>
    public static void RegisterClassReifier<T>(int classId) where T : class, IJSBindable<T>
    {
        if (classId <= 0) throw new ArgumentOutOfRangeException(nameof(classId));
        ClassReifiers[(uint)classId] = jsValue => jsValue.ToInstance<T>();
    }

    /// <summary>
    /// Registers the projector function (.NET → JS converter). Called by generated code during RegisterObjectConverters().
    /// </summary>
    /// <param name="projector">The projector function that converts .NET objects to JSValues.</param>
    /// <exception cref="HakoException">When a projector is already registered</exception>
    public static void RegisterProjector(Func<Realm, object, JSValue?> projector)
    {
        if (_projector != null)
        {
            throw new HakoException("A projector function is already registered. Each assembly should only register one projector.");
        }
        _projector = projector;
    }

    /// <summary>
    /// Attempts to reify (convert) a JSValue to its corresponding C# object.
    /// </summary>
    /// <param name="jsValue">The JavaScript value to convert.</param>
    /// <param name="result">The reified C# object, or null if reification fails.</param>
    /// <returns>True if reification succeeded; otherwise, false.</returns>
    /// <remarks>
    /// This method attempts reification in the following order:
    /// <list type="number">
    /// <item>JSClass instances (using ClassId)</item>
    /// <item>JSObject instances (using _hako_id property)</item>
    /// </list>
    /// </remarks>
    public static bool TryReify(this JSValue jsValue, out object? result)
    {
        // Try as JSClass first (check if it has a class ID)
        var classId = jsValue.ClassId();
        if (classId > 0 && ClassReifiers.TryGetValue((uint)classId, out var classReifier))
        {
            result = classReifier(jsValue);
            return true;
        }

        // Try as JSObject (check for _hako_id property)
        if (jsValue.IsObject() && !jsValue.IsArray())
        {
            var typeId = jsValue.GetPropertyOrDefault<uint>("_hako_id");
            if (typeId > 0)
            {
                if (ObjectReifiers.TryGetValue(typeId, out var objectReifier))
                {
                    result = objectReifier(jsValue.Realm, jsValue);
                    return true;
                }
            }
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Attempts to reify (convert) a JSValue to a specific C# type.
    /// </summary>
    /// <typeparam name="T">The target C# type.</typeparam>
    /// <param name="jsValue">The JavaScript value to convert.</param>
    /// <param name="result">The reified value, or default if reification fails.</param>
    /// <returns>True if reification succeeded and the result is of type T; otherwise, false.</returns>
    public static bool TryReify<T>(this JSValue jsValue, out T? result)
    {
        if (TryReify(jsValue, out var obj) && obj is T typed)
        {
            result = typed;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to project (convert) a .NET object to a JavaScript value.
    /// </summary>
    /// <param name="realm">The realm to create the value in.</param>
    /// <param name="obj">The .NET object to convert.</param>
    /// <param name="result">The projected JSValue, or null if projection fails.</param>
    /// <returns>True if projection succeeded; otherwise, false.</returns>
    /// <remarks>
    /// This method uses the registered projector function to convert .NET objects to JavaScript.
    /// Call <c>runtime.RegisterObjectConverters()</c> to register the projector.
    /// </remarks>
    public static bool TryProject(this Realm realm, object? obj, [NotNullWhen(true)] out JSValue? result)
    {
        if (obj == null)
        {
            result = null;
            return false;
        }

        if (_projector != null)
        {
            result = _projector(realm, obj);
            return result != null;
        }

        result = null;
        return false;
    }
}