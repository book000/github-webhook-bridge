using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace GitHubWebhookBridge.Managers;

/// <summary>ユーザーミュート設定を管理する。</summary>
public class MuteManager : BaseManager<List<MuteRecord>>, IMuteManager
{
    protected override string? FilePath { get; }
    protected override string? FileUrl { get; }
    protected override string? BlobPath { get; }

    public MuteManager(IConfiguration config, IHttpClientFactory httpClientFactory)
        : base(config, httpClientFactory)
    {
        FilePath = config["MUTES_FILE_PATH"];
        FileUrl = config["MUTES_FILE_URL"];
        BlobPath = config["MUTES_BLOB"];
    }

    protected override string GetDefaultFilePath() => "data/mutes.json";

    protected override List<MuteRecord>? Deserialize(string json)
        => DeserializeJson<List<MuteRecord>>(json);

    /// <summary>テスト専用: JSON を直接ロードする。</summary>
    internal void LoadForTest(string json)
    {
        var data = Deserialize(json)
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
            throw new InvalidOperationException(
                "MuteManager is not loaded. Call EnsureLoadedAsync() first.");

        var record = Data.Find(r => r.UserId == userId);
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
    [property: JsonPropertyName("events")] List<MuteEvent> Events);

/// <summary>ミュート対象のイベント設定。</summary>
public record MuteEvent(
    [property: JsonPropertyName("eventName")] string EventName,
    [property: JsonPropertyName("actions")] List<string>? Actions);

/// <summary>ミュート方式。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MuteType>))]
public enum MuteType
{
    [JsonStringEnumMemberName("include")] Include,
    [JsonStringEnumMemberName("exclude")] Exclude,
    [JsonStringEnumMemberName("all")]     All,
}
