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
    /// Registers a JSClass in the runtime's registry for the specified realm.
    /// This allows bidirectional marshaling between C# and JavaScript.
    /// Classes are automatically cleaned up when their associated realm is disposed.
    /// </summary>
    public static void RegisterJSClass<T>(this HakoRuntime runtime, JSClass jsClass) where T : class, IJSBindable<T>
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(T.TypeKey);
        ArgumentNullException.ThrowIfNull(jsClass);

        var key = (jsClass.Context.Pointer, T.TypeKey);
        if (!runtime.JSClassRegistry.TryAdd(key, jsClass))
        {
            throw new HakoException($"JSClass for type '{T.TypeKey}' is already registered in this realm");
        }
    }

    /// <summary>
    /// Gets a previously registered JSClass by its type key for the specified realm.
    /// Returns null if the class hasn't been registered.
    /// </summary>
    public static JSClass? GetJSClass<T>(this HakoRuntime runtime, Realm realm) where T : class, IJSBindable<T>
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(T.TypeKey);

        var key = (realm.Pointer, T.TypeKey);
        return runtime.JSClassRegistry.GetValueOrDefault(key);
    }

    public static CModule CreateModule<T>(
        this HakoRuntime runtime,
        Realm? context = null
        
    ) where T : class, IJSModuleBindable
    {
        return T.Create(runtime, context);
    }
}