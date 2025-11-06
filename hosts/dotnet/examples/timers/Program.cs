// examples/timers

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;

using var runtime = Hako.Initialize<WasmtimeEngine>();
using var realm = runtime.CreateRealm().WithGlobals(g => g
    .WithConsole()
    .WithTimers());

// setTimeout returns a promise
var timeoutResult = await realm.EvalAsync<string>(@"
    new Promise(resolve => {
        setTimeout(() => resolve('Timeout fired!'), 100);
    })
");
Console.WriteLine(timeoutResult);

// setInterval with clearInterval
await realm.EvalAsync(@"
    let count = 0;
    const id = setInterval(() => {
        console.log(`Tick ${++count}`);
        if (count === 3) clearInterval(id);
    }, 50);
");

await Task.Delay(200); // Let intervals complete

// Multiple timers
var result = await realm.EvalAsync<int>(@"
    new Promise(resolve => {
        let sum = 0;
        setTimeout(() => sum += 1, 10);
        setTimeout(() => sum += 2, 20);
        setTimeout(() => {
            sum += 3;
            resolve(sum);
        }, 30);
    })
");
Console.WriteLine($"Sum: {result}");

await Hako.ShutdownAsync();