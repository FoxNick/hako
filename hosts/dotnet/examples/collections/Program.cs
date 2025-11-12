// examples/collections

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HakoJS.VM;

var runtime = Hako.Initialize<WasmtimeEngine>();
var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

realm.RegisterClass<DataStore>();
runtime.ConfigureModules().WithModule<CollectionsModule>().Apply();

Console.WriteLine("=== Collections Examples ===\n");

var storeResult = await realm.EvalAsync("const store = new DataStore(); store");
var store = storeResult.ToInstance<DataStore>()!;

// List property (C# → JS)
Console.WriteLine("--- List property ---");
store.Numbers = new List<int> { 1, 2, 3, 4, 5 };
await realm.EvalAsync(@"
    console.log('Numbers:', store.numbers);
    console.log('Sum:', store.numbers.reduce((a, b) => a + b, 0));
");

// Array (JS → C#)
Console.WriteLine("\n--- Array from JS ---");
var arrayResult = await realm.EvalAsync("[10, 20, 30, 40]");
var numbers = arrayResult.ToArray<int>();
Console.WriteLine($"Received: [{string.Join(", ", numbers)}], Sum: {numbers.Sum()}");

// Dictionary property (C# → JS)
Console.WriteLine("\n--- Dictionary property ---");
store.Settings = new Dictionary<string, string>
{
    ["theme"] = "dark",
    ["language"] = "en"
};
await realm.EvalAsync(@"
    console.log('Settings:', JSON.stringify(store.settings));
    console.log('Theme:', store.settings.theme);
");

// Dictionary (JS → C#)
Console.WriteLine("\n--- Dictionary from JS ---");
var dictResult = await realm.EvalAsync("({ name: 'Alice', age: '30', city: 'NYC' })");
var dict = dictResult.ToDictionary<string, string>();
Console.WriteLine($"Received {dict.Count} items:");
foreach (var (key, value) in dict)
    Console.WriteLine($"  {key}: {value}");

// Custom record type (JS → C#)
Console.WriteLine("\n--- Custom record type ---");
var userResult = await realm.EvalAsync("({ name: 'Bob', age: 25, tags: ['admin', 'user'] })");
var user = userResult.As<User>();
Console.WriteLine($"User: {user.Name}, Age: {user.Age}, Tags: {string.Join(", ", user.Tags)}");

// Custom type (C# → JS)
Console.WriteLine("\n--- Custom type to JS ---");
var alice = new User("Alice", 30, new[] { "developer", "admin" });
var jsUser = realm.NewValue(alice);
using (var global = realm.GetGlobalObject())
{
    global.SetProperty("alice", jsUser);
}
await realm.EvalAsync(@"
    console.log('User:', alice.name, '| Tags:', alice.tags);
");

// Module with collections
Console.WriteLine("\n--- Module collections ---");
var modResult = await realm.EvalAsync(@"
    const { getScores, processNumbers, groupNumbers, getReadonlyScores } = await import('collections');
    
    console.log('Scores:', getScores());
    console.log('Doubled:', processNumbers([1, 2, 3, 4, 5]));
    
    const groups = groupNumbers([1, 2, 3, 4, 5, 6]);
    console.log('Even:', groups.even, '| Odd:', groups.odd);
    
    const readonly = getReadonlyScores();
    console.log('Readonly scores:', readonly, '| Frozen:', Object.isFrozen(readonly));
    
    processNumbers([1, 2, 3, 4, 5])
", new RealmEvalOptions() { Async = true });
var doubled = modResult.ToArray<int>();
Console.WriteLine($"C# received: [{string.Join(", ", doubled)}]");

// Method returning collection
Console.WriteLine("\n--- Method returning collection ---");
store.Numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
(await realm.EvalAsync("store.getEvenNumbers()")).Dispose();

// Array of custom types
Console.WriteLine("\n--- Array of custom types ---");
var pointsResult = await realm.EvalAsync("[{ x: 1, y: 2 }, { x: 3, y: 4 }, { x: 5, y: 6 }]");
var points = pointsResult.ToArrayOf<Point>();
Console.WriteLine($"Received {points.Length} points:");
foreach (var p in points)
    Console.WriteLine($"  ({p.X}, {p.Y})");

// Mutable vs readonly collections
Console.WriteLine("\n--- Mutable vs Readonly ---");
store.Numbers = new List<int> { 1, 2, 3 };
store.Settings = new Dictionary<string, string> { ["theme"] = "dark" };
store.ReadonlyNumbers = new ReadOnlyCollection<int>(new[] { 10, 20, 30 });
store.Config = new ReadOnlyDictionary<string, string>(
    new Dictionary<string, string> { ["version"] = "1.0" });

await realm.EvalAsync(@"
    console.log('Mutable frozen?', Object.isFrozen(store.numbers), '| Readonly frozen?', Object.isFrozen(store.readonlyNumbers));
    
    store.numbers.push(4);
    console.log('After push:', store.numbers);
    
    try {
        store.readonlyNumbers.push(40);
    } catch (e) {
        console.log('✓ Cannot modify readonly array');
    }
    
    try {
        store.config.version = '2.0';
    } catch (e) {
        console.log('✓ Cannot modify readonly dict');
    }
");

// Cleanup (FILO order)
Console.WriteLine("\n=== Done ===");
pointsResult.Dispose();
modResult.Dispose();
jsUser.Dispose();
userResult.Dispose();
dictResult.Dispose();
arrayResult.Dispose();
storeResult.Dispose();
realm.Dispose();
runtime.Dispose();
await Hako.ShutdownAsync();

// ============================================
// Type Definitions
// ============================================

[JSObject]
internal partial record Point(double X, double Y);

[JSObject]
internal partial record User(string Name, int Age, string[] Tags);

[JSClass]
internal partial class DataStore
{
    [JSProperty]
    public List<int> Numbers { get; set; } = new();

    [JSProperty]
    public Dictionary<string, string> Settings { get; set; } = new();

    [JSProperty]
    public IReadOnlyCollection<int> ReadonlyNumbers { get; set; } = 
        new ReadOnlyCollection<int>(Array.Empty<int>());

    [JSProperty]
    public IReadOnlyDictionary<string, string> Config { get; set; } = 
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    [JSMethod]
    public void AddNumber(int num)
    {
        Numbers.Add(num);
        Console.WriteLine($"[C#] Added: {num}");
    }

    [JSMethod]
    public List<int> GetEvenNumbers()
    {
        return Numbers.Where(n => n % 2 == 0).ToList();
    }
}

[JSModule(Name = "collections")]
internal partial class CollectionsModule
{
    [JSModuleMethod]
    public static Dictionary<int, int> GetScores()
    {
        return new Dictionary<int, int>
        {
            [1] = 100,
            [2] = 200,
            [3] = 300
        };
    }

    [JSModuleMethod]
    public static int[] ProcessNumbers(int[] numbers)
    {
        Console.WriteLine($"[C#] Processing {numbers.Length} numbers");
        return numbers.Select(n => n * 2).ToArray();
    }

    [JSModuleMethod]
    public static Dictionary<string, List<int>> GroupNumbers(int[] numbers)
    {
        return new Dictionary<string, List<int>>
        {
            ["even"] = numbers.Where(n => n % 2 == 0).ToList(),
            ["odd"] = numbers.Where(n => n % 2 != 0).ToList()
        };
    }

    [JSModuleMethod]
    public static IReadOnlyList<int> GetReadonlyScores()
    {
        return new List<int> { 100, 200, 300 }.AsReadOnly();
    }
}