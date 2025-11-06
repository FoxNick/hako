using Embed;

namespace HakoJS;

[ResourceDictionary]
internal static partial class HakoResources
{
    [Embed("Resources/hako.wasm")]
    public static partial ReadOnlySpan<byte> Reactor { get; }
}