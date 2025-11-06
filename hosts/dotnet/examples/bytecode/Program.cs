// examples/bytecode

using HakoJS;
using HakoJS.Backend.Wasmtime;

using var runtime = Hako.Initialize<WasmtimeEngine>();
using var realm = runtime.CreateRealm();

var code = @"
    function factorial(n) {
        return n <= 1 ? 1 : n * factorial(n - 1);
    }
    factorial(10);
";

// Compile to bytecode once
using var compileResult = realm.CompileToByteCode(code);
var bytecode = compileResult.Unwrap();

Console.WriteLine($"Compiled {bytecode.Length} bytes");

// Execute bytecode multiple times
for (int i = 0; i < 3; i++)
{
    using var result = realm.EvalByteCode(bytecode);
    Console.WriteLine($"Run {i + 1}: {result.Unwrap().AsNumber()}");
}

await using (var fs = new FileStream(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose))
{
    fs.Write(bytecode, 0, bytecode.Length);
    fs.Position = 0;
    
    var cached = new byte[bytecode.Length];
    fs.ReadExactly(cached);

    using var cachedResult = realm.EvalByteCode(cached);
    Console.WriteLine($"From cache: {cachedResult.Unwrap().AsNumber()}");
}

await Hako.ShutdownAsync();