// examples/scopes

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;

using var runtime = Hako.Initialize<WasmtimeEngine>();
using var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

// Without scopes - manual disposal in reverse order
void ManualDisposal()
{
    var obj = realm.EvalCode("({ a: 1, b: 2, c: 3 })").Unwrap();
    var a = obj.GetProperty("a");
    var b = obj.GetProperty("b");
    var c = obj.GetProperty("c");
    
    Console.WriteLine($"Sum: {a.AsNumber() + b.AsNumber() + c.AsNumber()}");
    
    // Must dispose in reverse order!
    c.Dispose();
    b.Dispose();
    a.Dispose();
    obj.Dispose();
}

ManualDisposal();

// With scopes - automatic disposal in correct order
realm.UseScope((r, scope) =>
{
    var obj = scope.Defer(r.EvalCode("({ a: 1, b: 2, c: 3 })").Unwrap());
    var a = scope.Defer(obj.GetProperty("a"));
    var b = scope.Defer(obj.GetProperty("b"));
    var c = scope.Defer(obj.GetProperty("c"));
    
    Console.WriteLine($"Sum: {a.AsNumber() + b.AsNumber() + c.AsNumber()}");
    // All disposed automatically in reverse: c, b, a, obj
});

// Async scope for working with promises
await realm.UseScopeAsync(async (r, scope) =>
{
    var user = scope.Defer(await r.EvalAsync(@"({
        name: 'Alice',
        fetchAge: async () => 30,
        greet() { return `Hello, I'm ${this.name}`; }
    })"));
    
    var name = user.GetPropertyOrDefault<string>("name");
    var greetFunc = scope.Defer(user.GetProperty("greet"));
    var ageFunc = scope.Defer(user.GetProperty("fetchAge"));
    
    var greeting = greetFunc.Bind(user).Invoke<string>();
    var age = await ageFunc.InvokeAsync<int>();
    
    Console.WriteLine($"{name}: {greeting}, age {age}");
});

// Nested scopes for complex operations
realm.UseScope((r, outerScope) =>
{
    var array = outerScope.Defer(r.EvalCode("[1, 2, 3, 4, 5]").Unwrap());
    
    var sum = 0.0;
    foreach (var itemResult in array.Iterate())
    {
        // Inner scope for each iteration
        r.UseScope((_, innerScope) =>
        {
            if (itemResult.TryGetSuccess(out var item))
            {
                innerScope.Defer(item);
                sum += item.AsNumber();
            }
        });
    }
    
    Console.WriteLine($"Array sum: {sum}");
});

// JSValue.UseScope extension method
var result = realm.EvalCode("({ x: 10, y: 20 })").Unwrap()
    .UseScope((obj, scope) =>
    {
        var x = scope.Defer(obj.GetProperty("x"));
        var y = scope.Defer(obj.GetProperty("y"));
        return x.AsNumber() + y.AsNumber();
    });

Console.WriteLine($"Result: {result}");

await Hako.ShutdownAsync();