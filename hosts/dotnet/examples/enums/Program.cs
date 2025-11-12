// examples/enums

using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.SourceGeneration;
using HakoJS.VM;
using System;

var runtime = Hako.Initialize<WasmtimeEngine>();
var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());

realm.RegisterClass<Logger>();
runtime.ConfigureModules().WithModule<FileSystemModule>().Apply();

// Example 1
var logResult = await realm.EvalAsync("const log = new Logger(); log");
var logger = logResult.ToInstance<Logger>() ?? throw new Exception("Logger not found");
logger.Level = LogLevel.Warning;
await realm.EvalAsync("console.log('JS level:', log.level);");
logResult.Dispose();

// Example 2
var modResult = await realm.EvalAsync(@"
    import { FileAccess, setPermissions } from 'fs';
    const rw = FileAccess.Read | FileAccess.Write;
    console.log('Read|Write:', rw);
    setPermissions('/file.txt', rw);
", new RealmEvalOptions { Type = EvalType.Module });
modResult.Dispose();


// C# 14
Console.WriteLine(FileAccess.TypeDefinition);
Console.WriteLine(LogEntry.TypeDefinition);
Console.WriteLine(FileSystemModule.TypeDefinition);


// Example 3
var entry = new LogEntry("Test", LogLevel.Info);
var jsEntry = realm.NewValue(entry);
var testFn = await realm.EvalAsync("function test(e){ console.log('Entry:', e.message, e.level); } test");
var invoke = await testFn.InvokeAsync(jsEntry);
invoke.Dispose();
testFn.Dispose();
jsEntry.Dispose();

// Example 4
var parsed = await realm.EvalAsync("({ message: 'Error', level: 'Error' })");
var csEntry = parsed.As<LogEntry>();
Console.WriteLine($"Parsed: {csEntry.Level}");
parsed.Dispose();

// Cleanup
realm.Dispose();
runtime.Dispose();
await Hako.ShutdownAsync();

// Enums
[JSEnum]
internal enum LogLevel { Debug, Info, Warning, Error }



// Classes
[JSClass]
internal partial class Logger
{
    [JSProperty] public LogLevel Level { get; set; } = LogLevel.Info;
}

[JSObject]
internal partial record LogEntry(string Message, LogLevel Level);

[JSModule(Name = "fs")]
internal partial class FileSystemModule
{
    [Flags]
    [JSEnum]
    internal enum FileAccess { None = 0, Read = 1, Write = 2, Execute = 4 }
    
    [JSModuleMethod]
    public static void SetPermissions(string path, FileAccess access)
        => Console.WriteLine($"Set {path}: {access} ({(int)access})");
}
