// examples/modules

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.Host;
using HakoJS.VM;

var runtime = Hako.Initialize<WasmtimeEngine>();

runtime.EnableModuleLoader((_, _, name, _) => name switch
{
    "utils" => ModuleLoaderResult.Source(@"
        export const add = (a, b) => a + b;
        export const multiply = (a, b) => a * b;
    "),
    "config" => ModuleLoaderResult.Source(@"
        export default { host: 'localhost', port: 3000 };
    "),
    _ => ModuleLoaderResult.Error()
});

var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

var module = await realm.EvalAsync(@"
    import { add, multiply } from 'utils';
    import config from 'config';
    
    console.log('10 + 5 =', add(10, 5));
    console.log('Server:', `${config.host}:${config.port}`);
    
    export const result = multiply(6, 7);
", new() { Type = EvalType.Module });

var resultProp = module.GetProperty("result");
Console.WriteLine($"Result: {resultProp.AsNumber()}");

resultProp.Dispose();
module.Dispose();
realm.Dispose(); ;

await Hako.ShutdownAsync();