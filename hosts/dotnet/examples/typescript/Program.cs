// examples/typescript

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;

using var runtime = Hako.Initialize<WasmtimeEngine>();
using var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

// Automatic type stripping with .ts extension
var result = await realm.EvalAsync<int>(@"
    interface User {
        name: string;
        age: number;
    }

    function greet(user: User): string {
        return `${user.name} is ${user.age} years old`;
    }

    const alice: User = { name: 'Alice', age: 30 };
    console.log(greet(alice));
    
    alice.age + 12;
", new() { FileName = "app.ts" });

Console.WriteLine($"Result: {result}");

// Manual stripping
var typescript = @"
    type Operation = 'add' | 'multiply';
    const calculate = (a: number, b: number, op: Operation): number => {
        return op === 'add' ? a + b : a * b;
    };
    calculate(5, 3, 'multiply');
";

var javascript = runtime.StripTypes(typescript);
var calcResult = await realm.EvalAsync<int>(javascript);
Console.WriteLine($"Calculation: {calcResult}");

await Hako.ShutdownAsync();