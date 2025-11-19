using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.Host;
using HakoJS.SourceGeneration;
using HakoJS.VM;

namespace HakoJS.Builders;

/// <summary>
/// Provides a fluent API for configuring and registering JavaScript modules with the runtime's module loader.
/// </summary>
/// <remarks>
/// <para>
/// This builder allows you to register C# modules, JSON modules, and custom module resolution logic
/// before enabling the module loader. Loaders are chained together, with precompiled modules always
/// available as a final fallback.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// runtime.ConfigureModuleLoader(loader =>
/// {
///     loader.WithModule&lt;MyModule&gt;()
///           .WithJsonModule("config", jsonString)
///           .WithFileSystemLoader("./modules")  // Supports both .js and .ts!
///           .Apply();
/// });
/// 
/// // JavaScript can now:
/// // import { myFunction } from 'MyModule';      // from precompiled
/// // import config from 'config';                 // from JSON
/// // import utils from './modules/utils.js';     // from file system
/// // import helpers from './modules/helpers.ts'; // TypeScript - auto-stripped!
/// </code>
/// </para>
/// </remarks>
public class ModuleLoaderBuilder
{
    private readonly Dictionary<string, CModule> _moduleMap = new();
    private readonly List<CModule> _modulesToRegister = [];
    private readonly List<ModuleLoaderFunction> _loaders = [];
    private readonly HakoRuntime _runtime;
    private ModuleNormalizerFunction? _normalizer;

    internal ModuleLoaderBuilder(HakoRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <summary>
    /// Registers a pre-created C module with the specified name.
    /// </summary>
    /// <param name="name">The module name used in JavaScript import statements.</param>
    /// <param name="module">The C module to register.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="module"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The module will be registered with the runtime and made available for import when <see cref="Apply"/> is called.
    /// Precompiled modules are always checked first, regardless of the order of other loaders.
    /// </remarks>
    public ModuleLoaderBuilder WithModule(string name, CModule module)
    {
        _modulesToRegister.Add(module);
        _moduleMap[name] = module;
        return this;
    }

    /// <summary>
    /// Creates and registers a source-generated module using its static factory method.
    /// </summary>
    /// <typeparam name="T">
    /// The module type decorated with [JSModule] that implements <see cref="IJSModuleBindable"/>.
    /// </typeparam>
    /// <param name="context">
    /// An optional realm context for creating the module. If <c>null</c>, uses the system realm.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// The module type must be decorated with the [JSModule] attribute, which generates the necessary
    /// binding code including a static <c>Name</c> property and <c>Create</c> method.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// [JSModule(Name = "MyModule")]
    /// public static partial class MyModule
    /// {
    ///     [JSModuleMethod]
    ///     public static string GetVersion() => "1.0.0";
    /// }
    /// 
    /// // Register it:
    /// loader.WithModule&lt;MyModule&gt;();
    /// 
    /// // JavaScript can now:
    /// // import { getVersion } from 'MyModule';
    /// </code>
    /// </para>
    /// </remarks>
    public ModuleLoaderBuilder WithModule<T>(Realm? context = null) where T : class, IJSModuleBindable
    {
        var module = T.Create(_runtime, context);
        _modulesToRegister.Add(module);
        _moduleMap[T.Name] = module;
        return this;
    }

    /// <summary>
    /// Creates and registers a JSON module from a JSON string.
    /// </summary>
    /// <param name="name">The module name used in JavaScript import statements.</param>
    /// <param name="json">The JSON string to parse and expose as a module.</param>
    /// <param name="context">
    /// An optional realm context for parsing the JSON. If <c>null</c>, uses the system realm.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="json"/> is <c>null</c>.</exception>
    /// <exception cref="HakoException">The JSON string is invalid and cannot be parsed.</exception>
    /// <remarks>
    /// <para>
    /// JSON modules allow you to import JSON data directly in JavaScript using ES6 import syntax.
    /// The entire JSON structure is exposed as the default export.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var configJson = "{\"version\": \"1.0.0\", \"debug\": true}";
    /// loader.WithJsonModule("config", configJson);
    /// 
    /// // JavaScript can now:
    /// // import config from 'config';
    /// // console.log(config.version); // "1.0.0"
    /// </code>
    /// </para>
    /// </remarks>
    public ModuleLoaderBuilder WithJsonModule(string name, string json, Realm? context = null)
    {
        var ctx = context ?? _runtime.GetSystemRealm();
        var module = ctx.Runtime.CreateJsonModule(name, json, ctx);
        return WithModule(name, module);
    }

    /// <summary>
    /// Adds a custom module loader function to the loader chain.
    /// </summary>
    /// <param name="loader">
    /// A function that receives runtime, realm, module name, and attributes, and returns a <see cref="ModuleLoaderResult"/>.
    /// Return <c>null</c> or <c>ModuleLoaderResult.Error()</c> to pass control to the next loader in the chain.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="loader"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// Multiple loaders can be added and will be tried in order. If a loader returns null or an error result,
    /// the next loader in the chain is tried. Precompiled modules registered via <see cref="WithModule"/>
    /// are always checked first, before any custom loaders.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// loader.AddLoader((runtime, realm, name, attributes) =>
    /// {
    ///     if (name.StartsWith("http://") || name.StartsWith("https://"))
    ///     {
    ///         var source = DownloadModule(name);
    ///         return ModuleLoaderResult.Source(source);
    ///     }
    ///     return null; // Try next loader
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public ModuleLoaderBuilder AddLoader(ModuleLoaderFunction loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loaders.Add(loader);
        return this;
    }

    /// <summary>
    /// Adds a file system based module loader that loads JavaScript and TypeScript files from the specified directory.
    /// </summary>
    /// <param name="basePath">The base directory path to load modules from.</param>
    /// <param name="stripTypeScript">
    /// Whether to automatically strip TypeScript type annotations from .ts files. Defaults to <c>true</c>.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="basePath"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// This is a convenience method that adds a loader for JavaScript and TypeScript files from the file system.
    /// The loader will resolve module names to file paths, load the source code, and automatically strip
    /// TypeScript type annotations when loading .ts files (if <paramref name="stripTypeScript"/> is true).
    /// </para>
    /// <para>
    /// The loader tries files in this order:
    /// 1. Exact path as specified
    /// 2. Path with .js extension
    /// 3. Path with .ts extension
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// loader.WithFileSystemLoader("./modules");
    /// 
    /// // JavaScript can now:
    /// // import { helper } from 'utils';      // loads ./modules/utils.js or .ts
    /// // import { add } from './math.ts';     // loads ./modules/math.ts (types stripped)
    /// </code>
    /// </para>
    /// </remarks>
    public ModuleLoaderBuilder WithFileSystemLoader(string basePath, bool stripTypeScript = true)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        
        return AddLoader((runtime, realm, name, attributes) =>
        {
            try
            {
                var exactPath = Path.Combine(basePath, name);
                if (File.Exists(exactPath))
                {
                    var source = File.ReadAllText(exactPath);
                    if (stripTypeScript && Path.GetExtension(exactPath).Equals(".ts", StringComparison.OrdinalIgnoreCase))
                    {
                        source = runtime.StripTypes(source);
                    }
                    
                    return ModuleLoaderResult.Source(source);
                }
                
                if (!Path.HasExtension(name))
                {
                    var jsPath = Path.Combine(basePath, $"{name}.js");
                    if (File.Exists(jsPath))
                    {
                        var source = File.ReadAllText(jsPath);
                        return ModuleLoaderResult.Source(source);
                    }
                    
                    var tsPath = Path.Combine(basePath, $"{name}.ts");
                    if (File.Exists(tsPath))
                    {
                        var source = File.ReadAllText(tsPath);
                        if (stripTypeScript)
                        {
                            source = runtime.StripTypes(source);
                        }
                        return ModuleLoaderResult.Source(source);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        });
    }

    /// <summary>
    /// Sets a custom module name normalizer function for resolving relative module paths.
    /// </summary>
    /// <param name="normalizer">
    /// A function that receives a base module name and an imported module name, and returns
    /// the normalized absolute module name.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="normalizer"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// The normalizer is used to resolve relative imports like <c>import './utils'</c> into
    /// absolute module names. This is essential for supporting relative imports in JavaScript modules.
    /// </para>
    /// <para>
    /// If no normalizer is provided, relative imports may not work correctly.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// loader.WithNormalizer((baseName, importName) =>
    /// {
    ///     if (importName.StartsWith("./") || importName.StartsWith("../"))
    ///     {
    ///         var baseDir = Path.GetDirectoryName(baseName) ?? "";
    ///         var combined = Path.Combine(baseDir, importName);
    ///         return Path.GetFullPath(combined);
    ///     }
    ///     return importName;
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public ModuleLoaderBuilder WithNormalizer(ModuleNormalizerFunction normalizer)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        _normalizer = normalizer;
        return this;
    }

    /// <summary>
    /// Applies the module loader configuration to the runtime, registering all modules
    /// and enabling the module loader with the configured loader chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method must be called to finalize the configuration. After calling <see cref="Apply"/>,
    /// all registered modules will be available for import in JavaScript code.
    /// </para>
    /// <para>
    /// The loader chain works as follows:
    /// 1. Precompiled modules (registered via <see cref="WithModule"/>) are checked first
    /// 2. Custom loaders (added via <see cref="AddLoader"/>) are tried in order
    /// 3. If all loaders return null or error, a final error is returned
    /// </para>
    /// <para>
    /// This method clears the internal module registration state after applying the configuration.
    /// </para>
    /// </remarks>
    public void Apply()
    {
        foreach (var module in _modulesToRegister) 
            _runtime.RegisterModule(module);
        
        var capturedMap = new Dictionary<string, CModule>(_moduleMap);
        var capturedLoaders = _loaders.ToList();

        _runtime.EnableModuleLoader(ComposedLoader, _normalizer);

        // Clear state
        _modulesToRegister.Clear();
        _moduleMap.Clear();
        _loaders.Clear();
        return;

        ModuleLoaderResult? ComposedLoader(HakoRuntime runtime, Realm realm, string name, Dictionary<string, string> attributes)
        {
            if (capturedMap.TryGetValue(name, out var module))
                return ModuleLoaderResult.Precompiled(module.Pointer);
            
            foreach (var loader in capturedLoaders)
            {
                var result = loader(runtime, realm, name, attributes);
                if (result != null)
                    return result;
            }
            return ModuleLoaderResult.Error();
        }
    }
}