// examples/hostfunctions

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;

var runtime = Hako.Initialize<WasmtimeEngine>();

var realm = runtime.CreateRealm().WithGlobals(g => g
    .WithConsole()
    .WithFunction("add", (ctx, _, args) =>
    {
        var a = (int)args[0].AsNumber();
        var b = (int)args[1].AsNumber();
        return ctx.NewNumber(a + b);
    })
    .WithFunction("greet", (ctx, _, args) => 
        ctx.NewString($"Hello, {args[0].AsString()}!"))
    .WithValue("version", "1.0.0")
    .WithFunctionAsync("fetchData", async (ctx, _, args) =>
    {
        var id = (int)args[0].AsNumber();
        await Task.Delay(50);
        
        var obj = ctx.NewObject();
        obj.SetProperty("id", id);
        obj.SetProperty("name", $"Item {id}");
        return obj;
    })
    .WithValue("config", new Dictionary<string, object>
    {
        ["host"] = "localhost",
        ["port"] = 3000,
        ["features"] = new[] { "auth", "api", "websockets" }
    }));

await realm.EvalAsync(@"
    console.log('2 + 3 =', add(2, 3));
    console.log(greet('World'));
    console.log('Version:', version);
");

var item = await realm.EvalAsync("fetchData(42)");
Console.WriteLine($"Fetched: {item.GetProperty("name").AsString()}");

await realm.EvalAsync(@"
    console.log(`Server: ${config.host}:${config.port}`);
    console.log('Features:', config.features.join(', '));
");

item.Dispose();
realm.Dispose();

await Hako.ShutdownAsync();