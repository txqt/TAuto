using System.Text.Json.Serialization;

namespace TAuto.Shared.Vision;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(VisionIpcRequest))]
[JsonSerializable(typeof(VisionIpcResponse))]
[JsonSerializable(typeof(VisionIpcTemplateBatch))]
[JsonSerializable(typeof(VisionMatchResultDto))]
[JsonSerializable(typeof(VisionOcrBlockDto))]
[JsonSerializable(typeof(VisionColorResultDto))]
[JsonSerializable(typeof(VisionRectDto))]
[JsonSerializable(typeof(VisionIpcTemplateBatch[]))]
[JsonSerializable(typeof(VisionRectDto[]))]
[JsonSerializable(typeof(VisionMatchResultDto[]))]
[JsonSerializable(typeof(VisionOcrBlockDto[]))]
[JsonSerializable(typeof(string[]))]
public partial class VisionJsonSerializerContext : JsonSerializerContext
{
}
