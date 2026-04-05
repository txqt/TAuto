using System.Text.Json;
using System.Text.Json.Serialization;

namespace TAuto.Automation.Models;

/// <summary>
/// Handles serialization and deserialization of BotProfiles with schema safety.
/// </summary>
public static class BotProfileSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(),
            new ActionJsonConverter()
        }
    };

    public static string Serialize(BotProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, Options);
    }

    public static BotProfile? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var profile = JsonSerializer.Deserialize<BotProfile>(json, Options);
        if (profile == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(profile.Version))
        {
            profile.Version = BotProfile.CurrentSchemaVersion;
        }

        if (profile.StateMachine.States.Count == 0)
        {
            profile.StateMachine.States.Add(new StateMachine.State { Name = "Main" });
            profile.StateMachine.InitialStateName = "Main";
        }

        return profile;
    }
}
