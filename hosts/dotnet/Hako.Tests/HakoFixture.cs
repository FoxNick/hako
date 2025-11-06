namespace HakoJS.Tests;

/// <summary>
/// Optional fixture for collection-level setup.
/// </summary>
public class HakoFixture : IDisposable
{
    public bool IsAvailable { get; }

    public HakoFixture()
    {
        // You can use this to check if WASM/runtime is available at all
        // without actually initializing it
        IsAvailable = true; // Set based on your availability check
    }

    public void Dispose()
    {
        // Collection-level cleanup if needed
    }
}

[CollectionDefinition("Hako Collection")]
public class HakoCollection : ICollectionFixture<HakoFixture>
{
    // This class is never instantiated, it's just a marker for xUnit
}