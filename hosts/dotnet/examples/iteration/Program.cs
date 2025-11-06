// examples/iteration

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;

var runtime = Hako.Initialize<WasmtimeEngine>();
var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

// Arrays - using generic Iterate<T>
var array = await realm.EvalAsync("[1, 2, 3, 4, 5]");
Console.WriteLine("Array:");
foreach (var number in array.Iterate<double>())
{
    Console.WriteLine($"  {number}");
}

// Maps - using generic IterateMap<TKey, TValue>
var map = await realm.EvalAsync(@"
    const m = new Map();
    m.set('name', 'Alice');
    m.set('age', 30);
    m.set('city', 'NYC');
    m;
");

Console.WriteLine("\nMap:");
foreach (var (key, value) in map.IterateMap<string, string>())
{
    Console.WriteLine($"  {key} = {value}");
}

// Sets - using generic IterateSet<T>
var set = await realm.EvalAsync("new Set([10, 20, 30, 40])");
Console.WriteLine("\nSet:");
foreach (var value in set.IterateSet<int>())
{
    Console.WriteLine($"  {value}");
}

// Async iteration - using generic IterateAsync<T>
var asyncIterable = await realm.EvalAsync(@"
    async function* generate() {
        for (let i = 1; i <= 3; i++) {
            await Promise.resolve();
            yield i * 10;
        }
    }
    generate();
");

Console.WriteLine("\nAsync Iterator:");
await foreach (var number in asyncIterable.IterateAsync<double>())
{
    Console.WriteLine($"  {number}");
}

asyncIterable.Dispose();
set.Dispose();
map.Dispose();
array.Dispose();
realm.Dispose();

await Hako.ShutdownAsync();