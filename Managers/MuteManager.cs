using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace GitHubWebhookBridge.Managers;

/// <summary>ユーザーミュート設定を管理する。</summary>
public class MuteManager(IConfiguration config, IHttpClientFactory httpClientFactory) : BaseManager<List<MuteRecord>>(config, httpClientFactory), IMuteManager
{
    protected override string? FilePath { get; } = config["MUTES_FILE_PATH"];

    protected override Uri? FileUrl { get; } = config["MUTES_FILE_URL"] is string url ? new Uri(url) : null;

    protected override string? BlobPath { get; } = config["MUTES_BLOB"];

    protected override string GetDefaultFilePath() => "data/mutes.json";

    /// <inheritdoc/>
    protected override List<MuteRecord>? Deserialize(string json)
        => DeserializeJson<List<MuteRecord>>(json);

    /// <summary>テスト専用: JSON を直接ロードする。</summary>
    internal void LoadForTest(string json)
    {
        List<MuteRecord> data = Deserialize(json)
            ?? throw new InvalidOperationException("Invalid test JSON");
        SetDataForTest(data);
    }

    /// <summary>
    /// ユーザーがミュートされているかどうかを返す。
    /// </summary>
    /// <param name="userId">判定対象の GitHub ユーザー ID。</param>
    /// <param name="eventName">GitHub Webhook イベント名。</param>
    /// <param name="action">イベントのアクション種別（省略可）。</param>
    /// <returns>ミュート対象の場合は true、それ以外は false。</returns>
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
            // 指定イベント・アクションがリストにある場合にミュート
            return record.Events.Any(muteEvent =>
                muteEvent.EventName == eventName
                && (muteEvent.Actions is null
                    || (action != null && muteEvent.Actions.Contains(action))));
        }

        // Exclude モード: リストにないイベントをミュートする
        // Actions == null のエントリは免除条件にならない
        return !record.Events.Any(muteEvent =>
            muteEvent.EventName == eventName
            && muteEvent.Actions != null
            && (action == null || muteEvent.Actions.Contains(action)));
    }
}

/// <summary>ユーザーごとのミュート設定レコード。</summary>
public record MuteRecord(
    [property: JsonPropertyName("userId")] long UserId,
    [property: JsonPropertyName("type")] MuteType Type,
    [property: JsonPropertyName("events")] IList<MuteEvent> Events);

/// <summary>ミュート対象のイベント設定。</summary>
public record MuteEvent(
    [property: JsonPropertyName("eventName")] string EventName,
    [property: JsonPropertyName("actions")] IList<string>? Actions);

/// <summary>ミュート方式。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MuteType>))]
public enum MuteType
{
    /// <summary>指定したイベント・アクションのみミュートする。</summary>
    [JsonStringEnumMemberName("include")]
    Include,

    /// <summary>指定したイベント・アクション以外をミュートする。</summary>
    [JsonStringEnumMemberName("exclude")]
    Exclude,

    /// <summary>全イベントをミュートする。</summary>
    [JsonStringEnumMemberName("all")]
    All,
}
