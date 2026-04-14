using System.Text.Json.Serialization;
using TAuto.Core.Models;

namespace TAuto.Core.Services;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(BotPersona))]
[JsonSerializable(typeof(BotSession))]
internal partial class PersonaJsonSerializerContext : JsonSerializerContext
{
}
