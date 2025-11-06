// examples/safety

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.Exceptions;
using HakoJS.Host;

using var runtime = Hako.Initialize<WasmtimeEngine>();

// Set up timeout interrupt
var timeout = HakoRuntime.CreateDeadlineInterruptHandler(1000);
runtime.EnableInterruptHandler(timeout);

using var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

// Memory limit
runtime.SetMemoryLimit(10 * 1024 * 1024); // 10MB

// Safe execution with timeout
try
{
    await realm.EvalAsync("while(true) {}"); // Infinite loop
}
catch (HakoException ex)
{
    Console.WriteLine($"Caught timeout: {ex.Message}");
}

runtime.DisableInterruptHandler();

// Error handling
try
{
    await realm.EvalAsync("throw new Error('Something went wrong')");
}
catch (HakoException ex)
{
    var jsError = ex.InnerException as JavaScriptException;
    Console.WriteLine($"JS Error: {jsError?.Message}");
    if (jsError?.StackTrace != null)
        Console.WriteLine($"Stack:\n{jsError.StackTrace}");
}

// Promise rejection tracking
runtime.OnUnhandledRejection((_, promise, reason, isHandled, _) =>
{
    if (!isHandled)
        Console.WriteLine($"Unhandled rejection: {reason.Realm.Dump(reason)}");
});

await realm.EvalAsync(@"
    Promise.reject('Unhandled error');
");

await Task.Delay(100); // Let rejection tracker fire

await Hako.ShutdownAsync();