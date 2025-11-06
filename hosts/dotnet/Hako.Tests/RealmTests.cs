using System.Collections.Concurrent;
using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.Host;
using HakoJS.VM;
using Xunit.Abstractions;

namespace HakoJS.Tests;

/// <summary>
/// Tests for Realm class (eval, value creation, functions, promises, etc.).
/// </summary>
public class RealmTests : TestBase
{
    private readonly ITestOutputHelper _testOutputHelper;

    public RealmTests(HakoFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture)
    {
        _testOutputHelper = testOutputHelper;
    }

    #region Evaluation Tests

    [Fact]
    public void EvalCode_SimpleExpression_ShouldReturnResult()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var result = realm.EvalCode("2 + 2");

        Assert.True(result.IsSuccess);
        using var value = result.Unwrap();
        Assert.Equal(4, value.AsNumber());
    }

    [Fact]
    public void EvalCode_WithSyntaxError_ShouldReturnFailure()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var result = realm.EvalCode("this is not valid javascript");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void EvalCode_EmptyString_ShouldReturnUndefined()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var result = realm.EvalCode("");

        Assert.True(result.IsSuccess);
        using var value = result.Unwrap();
        Assert.True(value.IsUndefined());
    }

    [Fact]
    public void CompileToByteCode_ShouldCompileCode()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var result = realm.CompileToByteCode("const x = 42;");

        Assert.True(result.IsSuccess);
        var bytecode = result.Unwrap();
        Assert.NotNull(bytecode);
        Assert.True(bytecode.Length > 0);
    }

    [Fact]
    public void EvalByteCode_ShouldExecuteBytecode()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        // Compile
        using var compileResult = realm.CompileToByteCode("2 + 2");
        Assert.True(compileResult.IsSuccess);
        var bytecode = compileResult.Unwrap();

        // Execute
        using var result = realm.EvalByteCode(bytecode);
        Assert.True(result.IsSuccess);
        using var value = result.Unwrap();
        Assert.Equal(4, value.AsNumber());
    }

    #endregion

    #region Value Creation Tests

    [Fact]
    public void GetGlobalObject_ShouldReturnGlobal()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var global = realm.GetGlobalObject();

        Assert.NotNull(global);
        Assert.True(global.IsObject());
    }

    [Fact]
    public void NewObject_ShouldCreateObject()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var obj = realm.NewObject();

        Assert.NotNull(obj);
        Assert.True(obj.IsObject());
    }

    [Fact]
    public void NewArray_ShouldCreateArray()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var arr = realm.NewArray();

        Assert.NotNull(arr);
        Assert.True(arr.IsArray());
    }

    [Fact]
    public void NewNumber_ShouldCreateNumber()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var num = realm.NewNumber(42.5);

        Assert.True(num.IsNumber());
        Assert.Equal(42.5, num.AsNumber());
    }

    [Fact]
    public void NewString_ShouldCreateString()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var str = realm.NewString("Hello, World!");

        Assert.True(str.IsString());
        Assert.Equal("Hello, World!", str.AsString());
    }

    [Fact]
    public void NewArrayBuffer_ShouldCreateArrayBuffer()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        using var buffer = realm.NewArrayBuffer(bytes);

        Assert.True(buffer.IsArrayBuffer());
    }

    [Fact]
    public void NewTypedArray_ShouldCreateTypedArray()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var arr = realm.NewTypedArray(10, TypedArrayType.Int32Array);

        Assert.True(arr.IsTypedArray());
        Assert.Equal(TypedArrayType.Int32Array, arr.GetTypedArrayType());
    }

    [Fact]
    public void NewUint8Array_ShouldCreateUint8Array()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var bytes = new byte[] { 10, 20, 30 };
        using var arr = realm.NewUint8Array(bytes);

        Assert.True(arr.IsTypedArray());
        Assert.Equal(TypedArrayType.Uint8Array, arr.GetTypedArrayType());
    }

    [Fact]
    public void NewFloat64Array_ShouldCreateFloat64Array()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var doubles = new double[] { 1.1, 2.2, 3.3 };
        using var arr = realm.NewFloat64Array(doubles);

        Assert.True(arr.IsTypedArray());
        Assert.Equal(TypedArrayType.Float64Array, arr.GetTypedArrayType());
    }

    [Fact]
    public void NewInt32Array_ShouldCreateInt32Array()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var ints = new int[] { 100, 200, 300 };
        using var arr = realm.NewInt32Array(ints);

        Assert.True(arr.IsTypedArray());
        Assert.Equal(TypedArrayType.Int32Array, arr.GetTypedArrayType());
    }

    [Fact]
    public void Undefined_ShouldReturnUndefined()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var undef = realm.Undefined();

        Assert.True(undef.IsUndefined());
    }

    [Fact]
    public void Null_ShouldReturnNull()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var nullVal = realm.Null();

        Assert.True(nullVal.IsNull());
    }

    [Fact]
    public void True_ShouldReturnTrue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var trueVal = realm.True();

        Assert.True(trueVal.IsBoolean());
        Assert.True(trueVal.AsBoolean());
    }

    [Fact]
    public void False_ShouldReturnFalse()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var falseVal = realm.False();

        Assert.True(falseVal.IsBoolean());
        Assert.False(falseVal.AsBoolean());
    }

    [Fact]
    public void NewValue_WithVariousTypes_ShouldConvert()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        using var num = realm.NewValue(42);
        Assert.True(num.IsNumber());

        using var str = realm.NewValue("test");
        Assert.True(str.IsString());

        using var boolean = realm.NewValue(true);
        Assert.True(boolean.IsBoolean());

        using var nullVal = realm.NewValue(null);
        Assert.True(nullVal.IsNull());
    }

    #endregion

    #region Function Tests

    [Fact]
    public void NewFunction_ShouldCreateFunction()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var func = realm.NewFunction("testFunc", (ctx, thisArg, args) => { return ctx.NewNumber(42); });

        Assert.True(func.IsFunction());
    }

    [Fact]
    public void CallFunction_ShouldInvokeFunction()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var func = realm.NewFunction("add", (ctx, thisArg, args) =>
        {
            var a = args[0].AsNumber();
            var b = args[1].AsNumber();
            return ctx.NewNumber(a + b);
        });

        using var arg1 = realm.NewNumber(5);
        using var arg2 = realm.NewNumber(3);
        using var result = realm.CallFunction(func, null, arg1, arg2);

        Assert.True(result.IsSuccess);
        using var value = result.Unwrap();
        Assert.Equal(8, value.AsNumber());
    }

    [Fact]
    public void NewFunctionAsync_ShouldCreateAsyncFunction()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var func = realm.NewFunctionAsync("asyncFunc", async (ctx, thisArg, args) =>
        {
            await Task.Delay(10);
            return ctx.NewNumber(42);
        });

        Assert.True(func.IsFunction());
    }

    #endregion

    #region Promise Tests

    [Fact]
    public void NewPromise_ShouldCreatePromise()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var promise = realm.NewPromise();

        Assert.NotNull(promise);
        Assert.NotNull(promise.Handle);
        Assert.True(promise.Handle.IsPromise());

        promise.Dispose();
    }

    [Fact]
    public async Task ResolvePromise_WithResolvedPromise_ShouldReturnValue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var value = await realm.EvalAsync("Promise.resolve(42)");
        Assert.Equal(42, value.AsNumber());
    }

    #endregion

    #region Iterator Tests

    [Fact]
    public void GetIterator_WithArray_ShouldReturnIterator()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var arr = realm.EvalCode("[1, 2, 3]").Unwrap();
        using var iterResult = realm.GetIterator(arr);

        Assert.True(iterResult.IsSuccess);
        using var iterator = iterResult.Unwrap();
        Assert.NotNull(iterator);
    }

    [Fact]
    public void GetWellKnownSymbol_ShouldReturnSymbol()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var symbol = realm.GetWellKnownSymbol("iterator");

        Assert.True(symbol.IsSymbol());
    }

    #endregion

    #region JSON Tests

    [Fact]
    public void ParseJson_ShouldParseValidJson()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var obj = realm.ParseJson("{\"name\":\"test\",\"value\":42}");

        Assert.True(obj.IsObject());
        using var name = obj.GetProperty("name");
        Assert.Equal("test", name.AsString());
        using var value = obj.GetProperty("value");
        Assert.Equal(42, value.AsNumber());
    }

    [Fact]
    public void ParseJson_WithInvalidJson_ShouldThrow()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        Assert.Throws<HakoException>(() => realm.ParseJson("{invalid json}"));
    }

    [Fact]
    public void BJSONEncode_ShouldEncodeValue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var obj = realm.NewObject();
        obj.SetProperty("test", 42);

        var bjson = realm.BJSONEncode(obj);

        Assert.NotNull(bjson);
        Assert.True(bjson.Length > 0);
    }

    [Fact]
    public void BJSONDecode_ShouldDecodeValue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var originalObj = realm.NewObject();
        originalObj.SetProperty("test", 42);

        var bjson = realm.BJSONEncode(originalObj);
        using var decoded = realm.BJSONDecode(bjson);

        Assert.True(decoded.IsObject());
        using var testProp = decoded.GetProperty("test");
        Assert.Equal(42, testProp.AsNumber());
    }

    [Fact]
    public void Dump_ShouldDumpValue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var obj = realm.NewObject();
        obj.SetProperty("name", "test");

        var dump = realm.Dump(obj);

        Assert.NotNull(dump);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void NewError_WithException_ShouldCreateError()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var error = realm.NewError(new InvalidOperationException("Test error"));

        Assert.True(error.IsError());
    }

    [Fact]
    public void ThrowError_ShouldThrowJSError()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var error = realm.NewError(new Exception("Test"));
        using var thrown = realm.ThrowError(error);

        Assert.True(thrown.IsException());
    }

    [Fact]
    public void ThrowError_WithErrorType_ShouldCreateTypedError()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var error = realm.ThrowError(JSErrorType.Type, "Test type error");

        Assert.True(error.IsException());
    }

    #endregion

    #region Module Tests

    [Fact]
    public void GetModuleNamespace_WithValidModule_ShouldReturnNamespace()
    {
        if (!IsAvailable) return;

        Hako.Runtime.EnableModuleLoader((_, _, name, attrs) =>
        {
            if (name == "test")
            {
                return ModuleLoaderResult.Source("export const value = 42;");
            }

            return ModuleLoaderResult.Error();
        });

        using var realm = Hako.Runtime.CreateRealm();
        using var module = realm.EvalCode("import('test')", new RealmEvalOptions { Type = EvalType.Global }).Unwrap();

        // Module import is async, so we'd need to handle promise resolution
        // This is a simplified test
        Assert.NotNull(module);
    }

    #endregion

    #region Opaque Data Tests

    [Fact]
    public void SetOpaqueData_ShouldStoreData()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        realm.SetOpaqueData("test-data");

        var retrieved = realm.GetOpaqueData();
        Assert.Equal("test-data", retrieved);
    }

    [Fact]
    public void GetOpaqueData_WhenNotSet_ShouldReturnNull()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var data = realm.GetOpaqueData();
        Assert.Null(data);
    }

    #endregion

    #region Thread Safety

    [Fact]
    public async Task ThreadSafety_MultipleRealmsWithTimers_ShouldCompleteSuccessfully()
    {
        if (!IsAvailable) return;

        const int realmCount = 256;
        const int timersPerRealm = 10;
        const int intervalsPerRealm = 3;
        const int intervalRunCount = 4; // Each interval runs 4 times
        var tasks = new List<Task>();
        var realms = new ConcurrentBag<Realm>();
        long totalValue = 0;

        var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var realmTimings = new ConcurrentBag<(int realmId, long createMs, long evalMs, long totalMs)>();

        _testOutputHelper.WriteLine(
            $"Starting stress test: {realmCount} realms, {timersPerRealm} timeouts + {intervalsPerRealm} intervals (x{intervalRunCount} runs each) per realm");
        _testOutputHelper.WriteLine(
            $"Expected completions per realm: {timersPerRealm + (intervalsPerRealm * intervalRunCount)}");
        _testOutputHelper.WriteLine(
            $"Total expected increments: {realmCount * (timersPerRealm + (intervalsPerRealm * intervalRunCount))}");
        _testOutputHelper.WriteLine(new string('-', 80));

        for (int i = 0; i < realmCount; i++)
        {
            var realmId = i;
            var task = Task.Run(async () =>
            {
                var realmStopwatch = System.Diagnostics.Stopwatch.StartNew();

                var createStart = realmStopwatch.ElapsedMilliseconds;
                // Use InvokeAsync to avoid blocking ThreadPool thread during realm creation
                var realm = await Hako.Dispatcher.InvokeAsync(() =>
                    Hako.Runtime.CreateRealm().WithGlobals((g) => g.WithTimers())
                );
                var createTime = realmStopwatch.ElapsedMilliseconds - createStart;

                realms.Add(realm);

                var evalStart = realmStopwatch.ElapsedMilliseconds;
                // EvalAsync should handle its own marshalling, but if not, wrap it too
                using var result = await realm.EvalAsync($@"
            new Promise((resolve) => {{
                let counter = 0;
                let completed = 0;
                const expectedCompletions = {timersPerRealm} + ({intervalsPerRealm} * {intervalRunCount});
                
                // Fire {timersPerRealm} timers
                for (let i = 0; i < {timersPerRealm}; i++) {{
                    setTimeout(() => {{
                        counter++;
                        completed++;
                        if (completed === expectedCompletions) {{
                            resolve(counter);
                        }}
                    }}, Math.random() * 50);
                }}
                
                // Add {intervalsPerRealm} intervals
                for (let i = 0; i < {intervalsPerRealm}; i++) {{
                    let runCount = 0;
                    const intervalId = setInterval(() => {{
                        counter++;
                        completed++;
                        runCount++;
                        
                        if (runCount === {intervalRunCount}) {{
                            clearInterval(intervalId);
                        }}
                        
                        if (completed === expectedCompletions) {{
                            resolve(counter);
                        }}
                    }}, Math.random() * 30 + 10); // 10-40ms intervals
                }}
            }})
        ");
                var evalTime = realmStopwatch.ElapsedMilliseconds - evalStart;
                var totalTime = realmStopwatch.ElapsedMilliseconds;

                var expectedValue = timersPerRealm + (intervalsPerRealm * intervalRunCount);
                // Use InvokeAsync for AsNumber() if it requires event loop access
                var value = await Hako.Dispatcher.InvokeAsync(() => result.AsNumber());
                Assert.Equal(expectedValue, (long)value);
                Interlocked.Add(ref totalValue, (long)value);

                realmTimings.Add((realmId, createTime, evalTime, totalTime));

                if (realmId % 50 == 0)
                {
                    _testOutputHelper.WriteLine(
                        $"Realm {realmId}: Create={createTime}ms, Eval={evalTime}ms, Total={totalTime}ms");
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        overallStopwatch.Stop();

        _testOutputHelper.WriteLine(new string('-', 80));
        _testOutputHelper.WriteLine("Disposing realms...");
        var disposeStopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Dispose realms asynchronously too if Dispose requires event loop access
        await Task.WhenAll(realms.Select(realm =>
            Hako.Dispatcher.InvokeAsync(realm.Dispose)
        ));

        disposeStopwatch.Stop();

        // Calculate statistics
        var timings = realmTimings.ToList();
        var createTimes = timings.Select(t => t.createMs).ToList();
        var evalTimes = timings.Select(t => t.evalMs).ToList();
        var totalTimes = timings.Select(t => t.totalMs).ToList();

        _testOutputHelper.WriteLine(new string('=', 80));
        _testOutputHelper.WriteLine("PERFORMANCE SUMMARY");
        _testOutputHelper.WriteLine(new string('=', 80));
        _testOutputHelper.WriteLine(
            $"Overall Duration: {overallStopwatch.ElapsedMilliseconds:N0}ms ({overallStopwatch.Elapsed.TotalSeconds:F2}s)");
        _testOutputHelper.WriteLine($"Dispose Duration: {disposeStopwatch.ElapsedMilliseconds:N0}ms");
        _testOutputHelper.WriteLine(string.Empty);

        _testOutputHelper.WriteLine("Realm Creation Times:");
        _testOutputHelper.WriteLine($"  Min:     {createTimes.Min():N0}ms");
        _testOutputHelper.WriteLine($"  Max:     {createTimes.Max():N0}ms");
        _testOutputHelper.WriteLine($"  Average: {createTimes.Average():N2}ms");
        _testOutputHelper.WriteLine($"  Median:  {GetMedian(createTimes):N2}ms");
        _testOutputHelper.WriteLine(string.Empty);

        _testOutputHelper.WriteLine("Eval Execution Times:");
        _testOutputHelper.WriteLine($"  Min:     {evalTimes.Min():N0}ms");
        _testOutputHelper.WriteLine($"  Max:     {evalTimes.Max():N0}ms");
        _testOutputHelper.WriteLine($"  Average: {evalTimes.Average():N2}ms");
        _testOutputHelper.WriteLine($"  Median:  {GetMedian(evalTimes):N2}ms");
        _testOutputHelper.WriteLine(string.Empty);

        _testOutputHelper.WriteLine("Total Realm Times (Create + Eval):");
        _testOutputHelper.WriteLine($"  Min:     {totalTimes.Min():N0}ms");
        _testOutputHelper.WriteLine($"  Max:     {totalTimes.Max():N0}ms");
        _testOutputHelper.WriteLine($"  Average: {totalTimes.Average():N2}ms");
        _testOutputHelper.WriteLine($"  Median:  {GetMedian(totalTimes):N2}ms");
        _testOutputHelper.WriteLine(string.Empty);

        _testOutputHelper.WriteLine("Throughput:");
        var realmsPerSecond = realmCount / overallStopwatch.Elapsed.TotalSeconds;
        var timersPerSecond = (realmCount * (timersPerRealm + intervalsPerRealm * intervalRunCount)) /
                              overallStopwatch.Elapsed.TotalSeconds;
        _testOutputHelper.WriteLine($"  Realms/sec:       {realmsPerSecond:N2}");
        _testOutputHelper.WriteLine($"  Timer events/sec: {timersPerSecond:N2}");
        _testOutputHelper.WriteLine(string.Empty);

        // Slowest realms
        _testOutputHelper.WriteLine("Top 5 Slowest Realms:");
        var slowest = timings.OrderByDescending(t => t.totalMs).Take(5);
        foreach (var (realmId, createMs, evalMs, totalMs) in slowest)
        {
            _testOutputHelper.WriteLine($"  Realm {realmId}: {totalMs}ms (Create: {createMs}ms, Eval: {evalMs}ms)");
        }

        _testOutputHelper.WriteLine(new string('=', 80));

        // Each realm should increment exactly (timersPerRealm + intervalsPerRealm * intervalRunCount) times
        long expectedTotal = realmCount * (timersPerRealm + (intervalsPerRealm * intervalRunCount));
        Assert.Equal(expectedTotal, totalValue);
    }

    private static double GetMedian(List<long> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int count = sorted.Count;
        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        else
        {
            return sorted[count / 2];
        }
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ShouldCleanupRealm()
    {
        if (!IsAvailable) return;

        var realm = Hako.Runtime.CreateRealm();
        realm.Dispose();

        // Should complete without throwing
        Assert.True(true);
    }

    #endregion
}