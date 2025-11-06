using System.Text.Json.Serialization;
using HakoJS.Host;

namespace HakoJS.Utils;

[JsonSerializable(typeof(MemoryUsage))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
internal partial class JsonContext : JsonSerializerContext
{
    
}