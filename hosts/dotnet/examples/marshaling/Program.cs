// examples/marshaling

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.SourceGeneration;

var runtime = Hako.Initialize<WasmtimeEngine>();
var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

// JS to C# with records
var jsData = await realm.EvalAsync(@"({
    name: 'TestApp',
    port: 3000,
    features: ['logging', 'metrics']
})");

var csConfig = jsData.As<Config>();
Console.WriteLine($"{csConfig.Name}: {csConfig.Port}");
foreach (var feature in csConfig.Features)
{
    Console.WriteLine($"Feature: {feature}");
}

// JSON parsing
var jsonObj = realm.ParseJson(@"{
    ""users"": [
        {""name"": ""Alice"", ""age"": 30},
        {""name"": ""Bob"", ""age"": 25}
    ]
}");

var users = jsonObj.GetProperty("users");
foreach (var userResult in users.Iterate())
{
    if (userResult.TryGetSuccess(out var user))
    {
        var name = user.GetPropertyOrDefault<string>("name");
        var age = user.GetPropertyOrDefault<double>("age");

        Console.WriteLine($"{name}: {age}");
        user.Dispose();
    }
}

// Clean up in reverse order (LIFO)
users.Dispose();
jsonObj.Dispose();
jsData.Dispose();
realm.Dispose();

await Hako.ShutdownAsync();

[JSObject]
internal partial record Config(string Name, int Port, string[] Features);