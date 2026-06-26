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
    /// TypeScript 版の mute.ts（lines 65-84）と同一ロジック。
    /// </summary>
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
            return record.Events.Any(e =>
                e.EventName == eventName
                && (e.Actions is null
                    || (action != null && e.Actions.Contains(action))));
        }

        // Exclude モード: リストにないイベントをミュートする
        // TypeScript 版準拠: e.Actions == null のエントリは免除条件にならない
        return !record.Events.Any(e =>
            e.EventName == eventName
            && e.Actions != null
            && (action == null || e.Actions.Contains(action)));
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
    [JsonStringEnumMemberName("include")] Include,
    [JsonStringEnumMemberName("exclude")] Exclude,
    [JsonStringEnumMemberName("all")] All,
}
