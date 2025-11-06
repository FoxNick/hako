using HakoJS.Builders;
using HakoJS.Exceptions;
using HakoJS.Host;
using HakoJS.SourceGeneration;
using HakoJS.VM;

namespace HakoJS.Extensions;

public static class HakoRuntimeExtensions
{
    public static CModule CreateJsonModule(
        this HakoRuntime runtime,
        string moduleName,
        string jsonContent,
        Realm? context = null)
    {
        var ctx = context ?? runtime.GetSystemRealm();
        var mod = ctx.Runtime
            .CreateCModule(moduleName, init => { init.SetExport("default", init.GetPrivateValue()); }, ctx)
            .AddExport("default").WithPrivateValue(ctx.ParseJson(jsonContent, moduleName));
        return mod;
    }

    public static CModule CreateValueModule<T>(
        this HakoRuntime runtime,
        string moduleName,
        string exportName,
        T value,
        Realm? context = null)
    {
        var ctx = context ?? runtime.GetSystemRealm();
        return ctx.Runtime.CreateCModule(moduleName, init => { init.SetExport(exportName, value); }, ctx)
            .AddExport(exportName);
    }

    public static CModule CreateModule(
        this HakoRuntime runtime,
        string moduleName,
        Action<CModuleInitializer> configure,
        Realm? context = null)
    {
        var ctx = context ?? runtime.GetSystemRealm();
        var module = ctx.Runtime.CreateCModule(moduleName, configure, ctx);
        return module;
    }

    public static ModuleLoaderBuilder ConfigureModules(this HakoRuntime runtime)
    {
        return new ModuleLoaderBuilder(runtime);
    }
    
    
    /// <summary>
    /// Registers a JSClass in the runtime's global registry.
    /// This allows bidirectional marshaling between C# and JavaScript.
    /// Classes are automatically cleaned up when their associated context is disposed.
    /// </summary>
    public static void RegisterJSClass<T>(this HakoRuntime runtime, JSClass jsClass) where T : class, IJSBindable<T>
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(T.TypeKey);
        ArgumentNullException.ThrowIfNull(jsClass);
        if (!runtime.JSClassRegistry.TryAdd(T.TypeKey, jsClass))
        {
            throw new HakoException($"Failed to register JSClass for type '{T.TypeKey}'");
        }
    }

    /// <summary>
    /// Gets a previously registered JSClass by its type key.
    /// Returns null if the class hasn't been registered.
    /// </summary>
    public static JSClass? GetJSClass<T>(this HakoRuntime runtime) where T : class, IJSBindable<T>
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(T.TypeKey);

        return runtime.JSClassRegistry.GetValueOrDefault(T.TypeKey);
    }

    public static CModule CreateModule<T>(
        this HakoRuntime runtime,
        Realm? context = null
        
    ) where T : class, IJSModuleBindable
    {
        return T.Create(runtime, context);
    }
}