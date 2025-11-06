using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.Host;
using HakoJS.VM;

namespace HakoJS.Tests;

/// <summary>
/// Tests for promise creation and handling.
/// </summary>
public class PromiseTests : TestBase
{
    public PromiseTests(HakoFixture fixture) : base(fixture) { }

    [Fact]
    public void NewPromise_CreatesPromise()
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
    public async Task ResolvePromise_WithResolvedValue_ReturnsValue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var promiseVal = await realm.EvalAsync("Promise.resolve(42)");
        Assert.Equal(42, promiseVal.AsNumber());
    }

    [Fact]
    public async Task ResolvePromise_WithRejection_ReturnsFailure()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var ex = await Assert.ThrowsAsync<HakoException>(async () =>
        {
            using var promiseVal = await realm.EvalAsync("Promise.reject('error')");
        });
        Assert.NotNull(ex.InnerException);
        Assert.IsType<PromiseRejectedException>(ex.InnerException);
        Assert.Equal("error", ((PromiseRejectedException)ex.InnerException).Reason);
    }

    [Fact]
    public async Task ResolvePromise_AlreadySettled_DoesNotDeadlock()
    {
        if (!IsAvailable) return;

        Hako.Runtime.EnableModuleLoader((_, _, name, attrs) =>
        {
            if (name == "test")
            {
                return ModuleLoaderResult.Source("export const run = async (name) => { return 'Hello' + name };");
            }
            return ModuleLoaderResult.Error();
        });

        using var realm = Hako.Runtime.CreateRealm();

        using var moduleValue = await realm.EvalAsync(@"
            import { run } from 'test';
            export {run }
        ", new RealmEvalOptions { Type = EvalType.Module });

        using var runFunction = moduleValue.GetProperty("run");

        using var arg = realm.NewValue("Test");
        using var callResult = realm.CallFunction(runFunction, null, arg);
        using var promiseHandle = callResult.Unwrap();

        Assert.True(promiseHandle.IsPromise());

        var startTime = DateTime.UtcNow;
        using var resolvedResult = await realm.ResolvePromise(promiseHandle);
        var endTime = DateTime.UtcNow;
        Assert.True((endTime - startTime).TotalSeconds < 5);
        using var resolvedHandle = resolvedResult.Unwrap();
        Assert.Equal("HelloTest", resolvedHandle.AsString());
    }

    [Fact]
    public async Task Promise_WithAsyncFunctionCallback_Resolves()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var global = realm.GetGlobalObject();

        using var readFileFunc = realm.NewFunctionAsync("readFile", async (ctx, thisArg, args) =>
        {
            var path = args[0].AsString();
            await Task.Delay(50);
            return ctx.NewString($"Content of {path}");
        });

        global.SetProperty("readFile", readFileFunc);

        using var code = realm.EvalCode(@"
            (async () => {
                const content = await readFile('example.txt');
                return content;
            })()
        ");

        using var promiseHandle = code.Unwrap();
        Assert.True(promiseHandle.IsPromise());

        using var resolvedResult = await realm.ResolvePromise(promiseHandle);
        using var resolvedValue = resolvedResult.Unwrap();

        Assert.Equal("Content of example.txt", resolvedValue.AsString());
    }

    [Fact]
    public void GetPromiseState_ReturnCorrectState()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        // Pending promise
        using var pendingPromise = realm.EvalCode("new Promise(() => {})").Unwrap();
        Assert.Equal(PromiseState.Pending, pendingPromise.GetPromiseState());

        // Fulfilled promise
        using var fulfilledPromise = realm.EvalCode("Promise.resolve(42)").Unwrap();
        Assert.Equal(PromiseState.Fulfilled, fulfilledPromise.GetPromiseState());

        // Rejected promise
        using var rejectedPromise = realm.EvalCode("Promise.reject('error')").Unwrap();
        Assert.Equal(PromiseState.Rejected, rejectedPromise.GetPromiseState());
    }

    [Fact]
    public void GetPromiseResult_ReturnsCorrectValue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        using var fulfilledPromise = realm.EvalCode("Promise.resolve(42)").Unwrap();
        var state = fulfilledPromise.GetPromiseState();
        if (state == PromiseState.Fulfilled)
        {
            using var result = fulfilledPromise.GetPromiseResult();
            Assert.NotNull(result);
            Assert.Equal(42, result.AsNumber());
        }
    }

    [Fact]
    public async Task EvalAsync_WithAsyncFunction_ReturnsResult()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var result = await realm.EvalAsync<string>(@"
            (async () => {
                await Promise.resolve();
                return 'Hello from async';
            })()
        ");

        Assert.Equal("Hello from async", result);
    }

    #region Top-Level Await Tests

    [Fact]
    public async Task TopLevelAwait_SimpleExpression_ReturnsValue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var result = await realm.EvalAsync<int>(@"
            const value = await Promise.resolve(42);
            value;
        ", new RealmEvalOptions { Async = true });

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task TopLevelAwait_MultipleAwaits_ReturnsLastValue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var result = await realm.EvalAsync<string>(@"
            const first = await Promise.resolve('Hello');
            const second = await Promise.resolve(' ');
            const third = await Promise.resolve('World');
            first + second + third;
        ", new RealmEvalOptions { Async = true });

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public async Task TopLevelAwait_WithRejection_ThrowsException()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var ex = await Assert.ThrowsAsync<HakoException>(async () =>
        {
            await realm.EvalAsync(@"
                await Promise.reject('Something went wrong');
            ", new RealmEvalOptions { Async = true });
        });

        Assert.NotNull(ex.InnerException);
        Assert.IsType<PromiseRejectedException>(ex.InnerException);
        Assert.Equal("Something went wrong", ((PromiseRejectedException)ex.InnerException).Reason);
    }

    [Fact]
    public async Task TopLevelAwait_WithAsyncFunction_ExecutesCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var result = await realm.EvalAsync<int>(@"
            async function calculate() {
                await Promise.resolve();
                return 10 + 20;
            }
            
            const value = await calculate();
            value * 2;
        ", new RealmEvalOptions { Async = true });

        Assert.Equal(60, result);
    }

    [Fact]
    public async Task TopLevelAwait_WithDelayedResolution_WaitsForCompletion()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var global = realm.GetGlobalObject();

        // Create an async function that returns a delayed promise
        using var delayFunc = realm.NewFunctionAsync("delay", async (ctx, thisArg, args) =>
        {
            var ms = (int)args[0].AsNumber();
            await Task.Delay(ms);
            return ctx.NewString("completed");
        });

        global.SetProperty("delay", delayFunc);

        var startTime = DateTime.UtcNow;
        var result = await realm.EvalAsync<string>(@"
            const result = await delay(100);
            result;
        ", new RealmEvalOptions { Async = true });
        var duration = DateTime.UtcNow - startTime;

        Assert.Equal("completed", result);
        Assert.True(duration.TotalMilliseconds >= 100);
    }

    [Fact]
    public async Task TopLevelAwait_WithPromiseAll_ReturnsAllResults()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            const results = await Promise.all([
                Promise.resolve(1),
                Promise.resolve(2),
                Promise.resolve(3)
            ]);
            results;
        ", new RealmEvalOptions { Async = true });

        Assert.True(result.IsArray());
        using var elem0 = result.GetProperty(0);
        using var elem1 = result.GetProperty(1);
        using var elem2 = result.GetProperty(2);

        Assert.Equal(1, elem0.AsNumber());
        Assert.Equal(2, elem1.AsNumber());
        Assert.Equal(3, elem2.AsNumber());
    }

    [Fact]
    public async Task TopLevelAwait_WithPromiseRace_ReturnsFirstResult()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        realm.WithGlobals((g) => g.WithTimers());

        var result = await realm.EvalAsync<string>(@"
            const winner = await Promise.race([
                new Promise(resolve => setTimeout(() => resolve('slow'), 100)),
                Promise.resolve('fast')
            ]);
            winner;
        ", new RealmEvalOptions { Async = true });

        Assert.Equal("fast", result);
    }

    [Fact]
    public void TopLevelAwait_WithNonGlobalEvalType_ThrowsException()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var options = new RealmEvalOptions
        {
            Type = EvalType.Module,
            Async = true
        };

        Assert.Throws<InvalidOperationException>(() =>
        {
            options.ToFlags();
        });
    }

    [Fact]
    public async Task TopLevelAwait_WithTryCatch_HandlesErrors()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var result = await realm.EvalAsync<string>(@"
            let result = 'success';
            try {
                await Promise.reject('error');
            } catch (e) {
                result = 'caught: ' + e;
            }
            result;
        ", new RealmEvalOptions { Async = true });

        Assert.Equal("caught: error", result);
    }

    [Fact]
    public async Task TopLevelAwait_WithComplexAsyncFlow_ExecutesInOrder()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            const log = [];
            
            log.push(1);
            
            await Promise.resolve().then(() => log.push(2));
            
            log.push(3);
            
            const value = await Promise.resolve(4);
            log.push(value);
            
            await Promise.all([
                Promise.resolve().then(() => log.push(5)),
                Promise.resolve().then(() => log.push(6))
            ]);
            
            log;
        ", new RealmEvalOptions { Async = true });

        Assert.True(result.IsArray());

        // Verify the execution order
        var logs = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            using var elem = result.GetProperty(i);
            logs.Add((int)elem.AsNumber());
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, logs);
    }

    [Fact]
    public async Task TopLevelAwait_WithReturnValue_ReturnsDirectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var result = await realm.EvalAsync<int>(@"
            await Promise.resolve();
            42;
        ", new RealmEvalOptions { Async = true });

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task TopLevelAwait_WithObjectReturn_ReturnsObject()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            const data = await Promise.resolve({ name: 'test', value: 123 });
            data;
        ", new RealmEvalOptions { Async = true });

        Assert.True(result.IsObject());
        using var name = result.GetProperty("name");
        using var value = result.GetProperty("value");

        Assert.Equal("test", name.AsString());
        Assert.Equal(123, value.AsNumber());
    }

    [Fact]
    public async Task TopLevelAwait_WithAsyncNativeFunction_IntegratesCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var global = realm.GetGlobalObject();

        // Create an async native function
        using var fetchDataFunc = realm.NewFunctionAsync("fetchData", async (ctx, thisArg, args) =>
        {
            var id = (int)args[0].AsNumber();
            await Task.Delay(50);
            
            var obj = ctx.NewObject();
            obj.SetProperty("id", id);
            obj.SetProperty("name", $"Item {id}");
            return obj;
        });

        global.SetProperty("fetchData", fetchDataFunc);

        using var result = await realm.EvalAsync(@"
            const item1 = await fetchData(1);
            const item2 = await fetchData(2);
            
            ({ items: [item1, item2] });
        ", new RealmEvalOptions { Async = true });

        Assert.True(result.IsObject());
        using var items = result.GetProperty("items");
        Assert.True(items.IsArray());

        using var item1 = items.GetProperty(0);
        using var item1Id = item1.GetProperty("id");
        using var item1Name = item1.GetProperty("name");
        Assert.Equal(1, item1Id.AsNumber());
        Assert.Equal("Item 1", item1Name.AsString());

        using var item2 = items.GetProperty(1);
        using var item2Id = item2.GetProperty("id");
        using var item2Name = item2.GetProperty("name");
        Assert.Equal(2, item2Id.AsNumber());
        Assert.Equal("Item 2", item2Name.AsString());
    }

    [Fact]
    public async Task TopLevelAwait_WithAsyncIterator_ProcessesSequentially()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        using var result = await realm.EvalAsync(@"
            async function* generateNumbers() {
                for (let i = 1; i <= 3; i++) {
                    await Promise.resolve();
                    yield i * 10;
                }
            }
            
            const results = [];
            for await (const num of generateNumbers()) {
                results.push(num);
            }
            
            results;
        ", new RealmEvalOptions { Async = true });

        Assert.True(result.IsArray());
        
        using var elem0 = result.GetProperty(0);
        using var elem1 = result.GetProperty(1);
        using var elem2 = result.GetProperty(2);

        Assert.Equal(10, elem0.AsNumber());
        Assert.Equal(20, elem1.AsNumber());
        Assert.Equal(30, elem2.AsNumber());
    }

    [Fact]
    public async Task TopLevelAwait_WithErrorInMiddle_PropagatesCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var ex = await Assert.ThrowsAsync<HakoException>(async () =>
        {
            await realm.EvalAsync(@"
                const first = await Promise.resolve(1);
                const second = await Promise.reject('middle error');
                const third = await Promise.resolve(3); // Should not execute
                first + second + third;
            ", new RealmEvalOptions { Async = true });
        });

        Assert.NotNull(ex.InnerException);
        Assert.IsType<PromiseRejectedException>(ex.InnerException);
        Assert.Equal("middle error", ((PromiseRejectedException)ex.InnerException).Reason);
    }

    [Fact]
    public async Task TopLevelAwait_NestedAsyncOperations_ResolvesCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var result = await realm.EvalAsync<int>(@"
            async function innerAsync() {
                const a = await Promise.resolve(5);
                const b = await Promise.resolve(10);
                return a + b;
            }
            
            async function outerAsync() {
                const x = await innerAsync();
                const y = await Promise.resolve(20);
                return x + y;
            }
            
            await outerAsync();
        ", new RealmEvalOptions { Async = true });

        Assert.Equal(35, result);
    }

    [Fact]
    public async Task TopLevelAwait_WithDynamicImport_LoadsModule()
    {
        if (!IsAvailable) return;

        Hako.Runtime.EnableModuleLoader((_, _, name, attrs) =>
        {
            if (name == "math")
            {
                return ModuleLoaderResult.Source("export const add = (a, b) => a + b;");
            }
            return ModuleLoaderResult.Error();
        });

        using var realm = Hako.Runtime.CreateRealm();

        var result = await realm.EvalAsync<int>(@"
            const mathModule = await import('math');
            const result = mathModule.add(15, 27);
            result;
        ", new RealmEvalOptions { Async = true });

        Assert.Equal(42, result);
    }

    #endregion
}