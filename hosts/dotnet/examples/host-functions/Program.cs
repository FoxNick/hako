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
    })
    // Error handling examples
    .WithFunction("throwSimpleError", (ctx, _, args) => throw new Exception("Something went wrong!"))
    .WithFunction("throwWithCause", (ctx, _, args) =>
    {
        try
        {
            throw new InvalidOperationException("Database connection failed");
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to fetch user data", ex);
        }
    })
    .WithFunction("divide", (ctx, _, args) =>
    {
        var a = (int)args[0].AsNumber();
        var b = (int)args[1].AsNumber();
        
        return b == 0 ? throw new DivideByZeroException("Cannot divide by zero") : ctx.NewNumber(a / b);
    }));

Console.WriteLine("=== Basic Host Functions ===\n");

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

Console.WriteLine("\n=== Error Handling Examples ===\n");

await realm.EvalAsync(@"
console.log('1. Simple error:');
try {
    throwSimpleError();
} catch (e) {
    console.log('Caught:', e.message);
    console.log(e.stack);
}

console.log('\n2. Error with cause:');
try {
    throwWithCause();
} catch (e) {
    console.log('Caught:', e.message);
    console.log(e.stack);
}

console.log('\n3. Division by zero:');
try {
    console.log('10 / 2 =', divide(10, 2));
    console.log('10 / 0 =', divide(10, 0));
} catch (e) {
    console.log('Caught:', e.message);
}
");

item.Dispose();
realm.Dispose();

await Hako.ShutdownAsync();