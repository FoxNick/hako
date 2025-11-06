using HakoJS.Extensions;
using HakoJS.Host;
using HakoJS.VM;

namespace HakoJS.Tests;

/// <summary>
/// Tests for module loading and C modules.
/// </summary>
public class ModuleTests : TestBase
{
    public ModuleTests(HakoFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ModuleLoader_SimpleModule_LoadsCorrectly()
    {
        if (!IsAvailable) return;

        Hako.Runtime.EnableModuleLoader((runtime, realm, name, attrs) =>
        {
            if (name == "my-module")
            {
                return ModuleLoaderResult.Source(@"
                    export const hello = (name) => {
                        return 'Hello, ' + name + '!';
                    };
                ");
            }
            return ModuleLoaderResult.Error();
        });

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            import { hello } from 'my-module';
            export const greeter = () => hello('World');
        ", new RealmEvalOptions { Type = EvalType.Module });

        using var greeter = result.GetProperty("greeter");
        Assert.Equal(JSType.Function, greeter.Type);
        Assert.Equal("Hello, World!", greeter.Invoke<string>());
    }

    [Fact]
    public async Task ModuleLoader_WithExports_ExportsCorrectly()
    {
        if (!IsAvailable) return;

        Hako.Runtime.EnableModuleLoader((runtime, realm, name, attrs) =>
        {
            if (name == "math")
            {
                return ModuleLoaderResult.Source(@"
                    export const PI = 3.14159;
                    export function square(x) {
                        return x * x;
                    }
                ");
            }
            return ModuleLoaderResult.Error();
        });

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            import { PI } from 'math';
            export { PI };
        ", new RealmEvalOptions { Type = EvalType.Module });

        using var piProp = result.GetProperty("PI");
        Assert.Equal(3.14159, piProp.AsNumber(), 5);
    }

    [Fact]
    public void CreateCModule_WithExports_CreatesCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.GetSystemRealm();
        using var module = Hako.Runtime.CreateCModule("testModule", init =>
        {
            init.SetExport("testValue", realm.NewNumber(123));
            init.SetExport("greeting", realm.NewString("Hello from C module"));
        }, realm);

        Assert.NotNull(module);
        Assert.Equal("testModule", module.Name);
    }

    [Fact]
    public async Task CModule_WithFunction_WorksCorrectly()
    {
        if (!IsAvailable) return;

        var initCalled = false;

        var moduleBuilder = Hako.Runtime.CreateCModule("math-module", init =>
        {
            initCalled = true;

            init.SetExport("greeting", "Hello from C!");
            init.SetExport("version", "1.0.0");
            init.SetExport("count", 42);
        }).AddExports("greeting", "version", "count");

        Hako.Runtime.EnableModuleLoader((runtime, realm, name, attrs) =>
        {
            if (name == "math-module")
            {
                return ModuleLoaderResult.Precompiled(moduleBuilder.Pointer);
            }
            return ModuleLoaderResult.Error();
        });

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            import { greeting, version, count } from 'math-module';
            export const message = greeting + ' v' + version + ' count=' + count;
        ", new RealmEvalOptions { Type = EvalType.Module });

        Assert.True(initCalled);
        using var message = result.GetProperty("message");
        Assert.Equal("Hello from C! v1.0.0 count=42", message.AsString());
    }

    [Fact]
    public async Task ModuleLoader_WithNormalizer_NormalizesPath()
    {
        if (!IsAvailable) return;

        Hako.Runtime.EnableModuleLoader(
            (runtime, realm, name, attrs) =>
            {
                if (name == "normalized-module")
                {
                    return ModuleLoaderResult.Source("export const value = 42;");
                }
                return ModuleLoaderResult.Error();
            },
            (baseName, moduleName) => "normalized-module" // Always normalize to this
        );

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            import { value } from './some/path/module.js';
            export { value };
        ", new RealmEvalOptions { Type = EvalType.Module });

        using var valueProp = result.TryGetProperty<int>("value");
        Assert.Equal(42, valueProp.Value);
    }

    [Fact]
    public async Task GetModuleNamespace_ReturnsNamespace()
    {
        if (!IsAvailable) return;

        Hako.Runtime.EnableModuleLoader((runtime, realm, name, attrs) =>
        {
            if (name == "test")
            {
                return ModuleLoaderResult.Source("export const value = 42;");
            }
            return ModuleLoaderResult.Error();
        });

        using var realm = Hako.Runtime.CreateRealm();

        using var module = realm.EvalCode(@"
            import('test')
        ", new RealmEvalOptions { Type = EvalType.Global }).Unwrap();

        // Module import returns a promise
        Assert.True(module.IsPromise());
    }

    [Fact]
    public async Task ConfigureModules_WithJsonModule_Works()
    {
        if (!IsAvailable) return;

        const string jsonTest = """
            {
                "name": "my-package",
                "version": "1.2.3",
                "dependencies": {
                    "lodash": "^4.17.21"
                }
            }
        """;

        Hako.Runtime.ConfigureModules()
            .WithJsonModule("package.json", jsonTest)
            .Apply();

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            import pkg from 'package.json' with {'type': 'json'};
            export const packageName = pkg.name;
        ", new RealmEvalOptions { Type = EvalType.Module });

        using var nameProp = result.GetProperty("packageName");
        Assert.Equal("my-package", nameProp.AsString());
    }

    [Fact]
    public async Task ModuleLoader_TypeScriptModule_StripsTypes()
    {
        if (!IsAvailable) return;

        Hako.Runtime.EnableModuleLoader((runtime, realm, name, attrs) =>
        {
            if (name == "ts-module")
            {
                const string tsSource = @"
                    export function greet(name: string): string {
                        return 'Hello, ' + name;
                    }
                    export const version: number = 1;
                ";
                
                // Strip TypeScript types
                var jsSource = runtime.StripTypes(tsSource);
                return ModuleLoaderResult.Source(jsSource);
            }
            return ModuleLoaderResult.Error();
        });

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            import { greet, version } from 'ts-module';
            export const message = greet('TypeScript');
            export const v = version;
        ", new RealmEvalOptions { Type = EvalType.Module });

        using var message = result.GetProperty("message");
        Assert.Equal("Hello, TypeScript", message.AsString());
        
        using var versionProp = result.GetProperty("v");
        Assert.Equal(1, versionProp.AsNumber());
    }

    [Fact]
    public async Task ModuleLoader_ChainedLoaders_FallsThrough()
    {
        if (!IsAvailable) return;

        Hako.Runtime.ConfigureModules()
            .AddLoader((runtime, realm, name, attrs) =>
            {
                // First loader only handles 'special' modules
                if (name == "special")
                    return ModuleLoaderResult.Source("export const value = 'special';");
                return null; // Fall through to next loader
            })
            .AddLoader((runtime, realm, name, attrs) =>
            {
                // Second loader handles everything else
                if (name == "fallback")
                    return ModuleLoaderResult.Source("export const value = 'fallback';");
                return null;
            })
            .Apply();

        using var realm = Hako.Runtime.CreateRealm();

        // Test first loader
        using var result1 = await realm.EvalAsync(@"
            import { value } from 'special';
            export { value };
        ", new RealmEvalOptions { Type = EvalType.Module });

        using var value1 = result1.GetProperty("value");
        Assert.Equal("special", value1.AsString());

        // Test second loader
        using var result2 = await realm.EvalAsync(@"
            import { value } from 'fallback';
            export { value };
        ", new RealmEvalOptions { Type = EvalType.Module });

        using var value2 = result2.GetProperty("value");
        Assert.Equal("fallback", value2.AsString());
    }
}