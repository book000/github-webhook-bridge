using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace GitHubWebhookBridge.Managers;

/// <summary>Class that manages users' mute settings.</summary>
public class MuteManager(IConfiguration config, IHttpClientFactory httpClientFactory) : BaseManager<List<MuteRecord>>(config, httpClientFactory), IMuteManager
{
    protected override string? FilePath { get; } = config["MUTES_FILE_PATH"];

    protected override Uri? FileUrl { get; } = config["MUTES_FILE_URL"] is string url ? new Uri(url) : null;

    protected override string? BlobPath { get; } = config["MUTES_BLOB"];

    protected override string GetDefaultFilePath() => "data/mutes.json";

    /// <inheritdoc/>
    protected override List<MuteRecord>? Deserialize(string json)
        => DeserializeJson<List<MuteRecord>>(json);

    /// <summary>Test-only: loads JSON directly.</summary>
    internal void LoadForTest(string json)
    {
        List<MuteRecord> data = Deserialize(json)
            ?? throw new InvalidOperationException("Invalid test JSON");
        SetDataForTest(data);
    }

    /// <summary>Returns whether the user is muted for the specified event.</summary>
    /// <param name="userId">The GitHub user ID to evaluate.</param>
    /// <param name="eventName">The GitHub webhook event name.</param>
    /// <param name="action">The event's action type (optional).</param>
    /// <returns><see langword="true"/> if muted; otherwise <see langword="false"/>.</returns>
    public bool IsMuted(long userId, string eventName, string? action)
    {
        if (Data is null)
        {
            throw new InvalidOperationException(
                "MuteManager is not loaded. Call EnsureLoadedAsync() first.");
        }

        MuteRecord? record = Data.Find(r => r.UserId == userId);
        if (record is null) return false;
        if (record.Type == MuteType.All) return true;

        if (record.Type == MuteType.Include)
        {
            // Mute when the specified event/action is present in the list.
            return record.Events.Any(muteEvent =>
                muteEvent.EventName == eventName
                && (muteEvent.Actions is null
                    || (action != null && muteEvent.Actions.Contains(action))));
        }

        // Exclude mode: mute events that are not in the list.
        // Entries with Actions == null do not count as an exemption condition.
        return !record.Events.Any(muteEvent =>
            muteEvent.EventName == eventName
            && muteEvent.Actions != null
            && (action == null || muteEvent.Actions.Contains(action)));
    }
}

/// <summary>Record representing the per-user mute settings.</summary>
public record MuteRecord(
    [property: JsonPropertyName("userId")] long UserId,
    [property: JsonPropertyName("type")] MuteType Type,
    [property: JsonPropertyName("events")] IList<MuteEvent> Events);

/// <summary>Record representing the configuration of an event to mute.</summary>
public record MuteEvent(
    [property: JsonPropertyName("eventName")] string EventName,
    [property: JsonPropertyName("actions")] IList<string>? Actions);

/// <summary>Enumeration representing the mute mode.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MuteType>))]
public enum MuteType
{
    /// <summary>Mute only the specified events/actions.</summary>
    [JsonStringEnumMemberName("include")]
    Include,

    /// <summary>Mute everything except the specified events/actions.</summary>
    [JsonStringEnumMemberName("exclude")]
    Exclude,

    /// <summary>Mute all events.</summary>
    [JsonStringEnumMemberName("all")]
    All,
}
