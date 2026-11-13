using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Managers;

/// <summary>Class that manages users' mute settings.</summary>
public class MuteManager(
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    TimeProvider? timeProvider = null,
    ILogger<MuteManager>? logger = null) : BaseManager<List<MuteRecord>>(config, httpClientFactory, timeProvider, logger), IMuteManager
{
    protected override string? FilePath { get; } = config["MUTES_FILE_PATH"];

    protected override Uri? FileUrl { get; } = config["MUTES_FILE_URL"] is string url ? new Uri(url) : null;

    protected override string? BlobPath { get; } = config["MUTES_BLOB"];

    protected override string GetDefaultFilePath() => "data/mutes.json";

    /// <inheritdoc/>
    protected override List<MuteRecord>? Deserialize(string json)
        => Normalize(DeserializeJson<List<MuteRecord?>>(json));

    private List<MuteRecord>? Normalize(List<MuteRecord?>? records)
    {
        if (records is null) return null;

        List<MuteRecord> normalized = [];
        HashSet<long> seenUserIds = [];
        foreach (MuteRecord? record in records)
        {
            if (record is null)
            {
                logger?.LogWarning("Dropping a null entry in the mute rules");
                continue;
            }

            if (!seenUserIds.Add(record.UserId))
            {
                logger?.LogWarning(
                    "Dropping a duplicate mute record for userId {UserId}; the first record for that user is used",
                    record.UserId);
                continue;
            }

            normalized.Add(record with { Events = NormalizeEvents(record.UserId, record.Events) });
        }

        return normalized;
    }

    private List<MuteEvent> NormalizeEvents(long userId, IEnumerable<MuteEvent?>? events)
    {
        if (events is null)
        {
            logger?.LogWarning("The mute record for userId {UserId} has no events; treating it as an empty list", userId);
            return [];
        }

        List<MuteEvent> normalized = [];
        foreach (MuteEvent? muteEvent in events)
        {
            if (muteEvent is null || string.IsNullOrWhiteSpace(muteEvent.EventName))
            {
                logger?.LogWarning("Dropping an event entry without an event name in the mute record for userId {UserId}", userId);
                continue;
            }

            normalized.Add(new MuteEvent(muteEvent.EventName.Trim(), NormalizeActions(userId, muteEvent.Actions)));
        }

        return normalized;
    }

    private List<string>? NormalizeActions(long userId, IList<string>? actions)
    {
        if (actions is null) return null;

        List<string> normalized = [.. actions
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Select(action => action.Trim())];
        if (normalized.Count != actions.Count)
        {
            logger?.LogWarning("Dropping blank action entries in the mute record for userId {UserId}", userId);
        }

        return normalized;
    }

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
            return record.Events.Any(muteEvent =>
                IsSameName(muteEvent.EventName, eventName)
                && (muteEvent.Actions is null
                    || (action != null && ContainsAction(muteEvent.Actions, action))));
        }

        return !record.Events.Any(muteEvent =>
            IsSameName(muteEvent.EventName, eventName)
            && (muteEvent.Actions is null
                || action == null
                || ContainsAction(muteEvent.Actions, action)));
    }

    private static bool IsSameName(string configured, string actual)
        => string.Equals(configured, actual, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAction(IList<string> actions, string action)
        => actions.Contains(action, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Record representing the per-user mute settings.</summary>
/// <remarks>
/// <see cref="Events"/> is never <see langword="null"/> once loaded by <see cref="MuteManager"/>:
/// a record without <c>events</c> is treated as having an empty list.
/// Only the first record for a given <see cref="UserId"/> is used.
/// </remarks>
public record MuteRecord(
    [property: JsonPropertyName("userId")] long UserId,
    [property: JsonPropertyName("type")] MuteType Type,
    [property: JsonPropertyName("events")] IList<MuteEvent> Events);

/// <summary>Record representing the configuration of an event to mute.</summary>
/// <remarks>
/// Event names and actions are matched case-insensitively with surrounding whitespace ignored.
/// A <see langword="null"/> <c>Actions</c> (property omitted) targets every action of the event, for both <c>include</c> and <c>exclude</c>.
/// </remarks>
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
