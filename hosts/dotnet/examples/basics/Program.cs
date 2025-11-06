using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;

// Initialize the runtime
var runtime = Hako.Initialize<WasmtimeEngine>();

// Create a realm (isolated JS execution context)
var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

// Synchronous evaluation
var syncResult = realm.EvalCode("2 + 2");
Console.WriteLine($"2 + 2 = {syncResult.Unwrap().AsNumber()}");
syncResult.Dispose();

// Async evaluation automatically handles promises
var promiseResult = await realm.EvalAsync<int>("Promise.resolve(42)");
Console.WriteLine($"Promise resolved to: {promiseResult}");

// Working with objects
var obj = await realm.EvalAsync(@"
    const user = {
        name: 'Alice',
        age: 30,
        greet() {
            return `Hello, I'm ${this.name}`;
        }
    };
    user;
");

var name = obj.GetPropertyOrDefault<string>("name");
var greeting = obj.GetProperty("greet");
Console.WriteLine($"{name}: {greeting.Invoke()}");
greeting.Dispose();
obj.Dispose();

// Error handling with try-catch
try
{
    await realm.EvalAsync("Promise.reject('oops')");
}
catch (Exception ex)
{
    Console.WriteLine($"Caught: {ex.InnerException?.Message}");
}

realm.Dispose();
runtime.Dispose();

await Hako.ShutdownAsync();