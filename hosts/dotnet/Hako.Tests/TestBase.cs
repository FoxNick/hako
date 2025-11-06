using HakoJS.Backend.Wacs;
using HakoJS.Backend.Wasmtime;

namespace HakoJS.Tests;

/// <summary>
/// Base class for Hako tests that provides lifecycle management for the runtime.
/// Each test gets its own isolated runtime instance.
/// </summary>
[Collection("Hako Collection")]
public abstract class TestBase : IAsyncLifetime
{
    private readonly HakoFixture _fixture;
    protected bool IsAvailable => _fixture.IsAvailable;

    protected TestBase(HakoFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Called before each test runs - initializes the runtime.
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        if (_fixture.IsAvailable)
        {
            try
            {
                // Ensure any previous runtime is fully shut down first
                if (Hako.IsInitialized)
                {
                    await Hako.ShutdownAsync();
                }

                var runtime = Hako.Initialize<WasmtimeEngine>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Runtime initialization failed: {ex.Message}");
                throw;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Called after each test runs - shuts down the runtime.
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        try
        {
            // Always try to shutdown if initialized
            if (Hako.IsInitialized)
            {
                await Hako.ShutdownAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Runtime shutdown failed: {ex.Message}");
            // Don't rethrow - we're in cleanup
        }
    }
}