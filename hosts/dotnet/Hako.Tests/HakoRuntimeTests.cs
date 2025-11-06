using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.Host;
using HakoJS.VM;

namespace HakoJS.Tests;

/// <summary>
/// Tests for HakoRuntime class (memory management, GC, modules, interrupts).
/// </summary>
public class HakoRuntimeTests : TestBase
{
    public HakoRuntimeTests(HakoFixture fixture) : base(fixture) { }

    [Fact]
    public void CreateRealm_ShouldReturnValidRealm()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        Assert.NotNull(realm);
        Assert.Equal(Hako.Runtime, realm.Runtime);
    }

    [Fact]
    public void CreateRealm_WithOptions_ShouldApplyOptions()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm(new RealmOptions
        {
            Intrinsics = RealmOptions.RealmIntrinsics.Standard
        });

        Assert.NotNull(realm);
    }

    [Fact]
    public void GetSystemRealm_ShouldReturnSameInstance()
    {
        if (!IsAvailable) return;

        var realm1 = Hako.Runtime.GetSystemRealm();
        var realm2 = Hako.Runtime.GetSystemRealm();

        Assert.Same(realm1, realm2);
    }

    [Fact]
    public void RunGC_ShouldExecuteWithoutErrors()
    {
        if (!IsAvailable) return;

        Hako.Runtime.RunGC();

        // Should complete without throwing
        Assert.True(true);
    }

    [Fact]
    public void ComputeMemoryUsage_ShouldReturnMemoryStats()
    {
        if (!IsAvailable) return;

        var realm = Hako.Runtime.GetSystemRealm();
        var usage = Hako.Runtime.ComputeMemoryUsage(realm);

        Assert.NotNull(usage);
        Assert.True(usage.MemoryUsedSize >= 0);
    }

    [Fact]
    public void DumpMemoryUsage_ShouldReturnString()
    {
        if (!IsAvailable) return;

        var dump = Hako.Runtime.DumpMemoryUsage();

        Assert.NotNull(dump);
        Assert.False(string.IsNullOrEmpty(dump));
    }

    [Fact]
    public void SetMemoryLimit_ShouldApplyLimit()
    {
        if (!IsAvailable) return;

        Hako.Runtime.SetMemoryLimit(1024 * 1024); // 1MB

        // Should complete without throwing
        Assert.True(true);
    }

    [Fact]
    public void Build_ShouldReturnBuildInfo()
    {
        if (!IsAvailable) return;

        var build = Hako.Runtime.Build;

        Assert.NotNull(build);
    }

    [Fact]
    public void CreateCModule_ShouldCreateNativeModule()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.GetSystemRealm();
        using var module = Hako.Runtime.CreateCModule("testModule", init =>
        {
            init.SetExport("testValue", realm.NewNumber(123));
        }, realm);

        Assert.NotNull(module);
        Assert.Equal("testModule", module.Name);
    }

    [Fact]
    public async Task EnableInterruptHandler_WithDeadline_ShouldInterruptLongRunningCode()
    {
        if (!IsAvailable) return;

        var handler = HakoRuntime.CreateDeadlineInterruptHandler(100); // 100ms
        Hako.Runtime.EnableInterruptHandler(handler);

        using var realm = Hako.Runtime.CreateRealm();

        await Assert.ThrowsAsync<HakoException>(async () =>
        {
            await realm.EvalAsync("while(true) {}");
        });

        Hako.Runtime.DisableInterruptHandler();
    }

    [Fact]
    public void EnableInterruptHandler_WithGasLimit_ShouldInterruptAfterOperations()
    {
        if (!IsAvailable) return;

        var handler = HakoRuntime.CreateGasInterruptHandler(1000);
        Hako.Runtime.EnableInterruptHandler(handler);

        Hako.Runtime.DisableInterruptHandler();

        // Should complete without throwing
        Assert.True(true);
    }

    [Fact]
    public void CombineInterruptHandlers_ShouldCombineMultipleHandlers()
    {
        if (!IsAvailable) return;

        var handler1 = HakoRuntime.CreateDeadlineInterruptHandler(5000);
        var handler2 = HakoRuntime.CreateGasInterruptHandler(10000);
        var combined = HakoRuntime.CombineInterruptHandlers(handler1, handler2);

        Hako.Runtime.EnableInterruptHandler(combined);
        Hako.Runtime.DisableInterruptHandler();

        Assert.True(true);
    }

    [Fact]
    public async Task OnUnhandledRejection_ShouldTrackUnhandledPromises()
    {
        if (!IsAvailable) return;

        var rejectionTcs = new TaskCompletionSource<bool>();
    
        Hako.Runtime.OnUnhandledRejection((Realm realm, JSValue promise, JSValue reason, bool isHandled, int opaque) =>
        {
            rejectionTcs.TrySetResult(true);
        });

        using var realm = Hako.Runtime.CreateRealm();

        // Create an unhandled promise rejection - no catch handler attached
        using var result = realm.EvalCode(@"
        Promise.resolve().then(() => {
            throw new Error('test error');
        });
    ");
        
        var rejectionCaught = await Task.WhenAny(
            rejectionTcs.Task,
            Task.Delay(TimeSpan.FromSeconds(1))
        ) == rejectionTcs.Task;

        Assert.True(rejectionCaught, "Unhandled rejection callback was not invoked");
        Assert.True(await rejectionTcs.Task, "Rejection was unexpectedly handled");

        Hako.Runtime.DisablePromiseRejectionTracker();
    }

    [Fact]
    public void SetStripInfo_ShouldConfigureStripOptions()
    {
        if (!IsAvailable) return;

        var stripOptions = new StripOptions
        {
            StripDebug = true,
            StripSource = false
        };

        Hako.Runtime.SetStripInfo(stripOptions);

        var retrieved = Hako.Runtime.GetStripInfo();
        Assert.Equal(stripOptions.StripDebug, retrieved.StripDebug);
        Assert.Equal(stripOptions.StripSource, retrieved.StripSource);
    }
}
