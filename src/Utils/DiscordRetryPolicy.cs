using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace GitHubWebhookBridge.Utils;

/// <summary>
/// "discord" 名前付き HttpClient に適用する再試行ポリシーを定義する。
/// 対象は 429 (レート制限) のみとし、Retry-After ヘッダーを尊重しつつ短い上限で打ち切ることで、
/// Discord 側の障害・輻輳時に Webhook 処理全体が長時間ブロックされるのを防ぐ。
/// <see cref="Program"/> と単体テストの両方から参照し、実装を一箇所に集約する
/// </summary>
public static class DiscordRetryPolicy
{
    /// <summary>AddResilienceHandler に渡すハンドラー名。</summary>
    public const string HandlerName = "discord-retry";

    /// <summary>再試行の最大回数（初回試行を含まない）。</summary>
    public const int MaxRetryAttempts = 2;

    /// <summary>Retry-After ヘッダーが無い場合の基準待機時間。</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);

    /// <summary>Retry-After ヘッダーの指定値を含め、待機時間の上限とする値。</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// <see cref="ResiliencePipelineBuilder{HttpResponseMessage}"/> に再試行戦略を追加する
    /// </summary>
    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        HttpRetryStrategyOptions options = new()
        {
            ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests),
            MaxRetryAttempts = MaxRetryAttempts,
            BackoffType = DelayBackoffType.Constant,
            Delay = Delay,
            MaxDelay = MaxDelay,
            UseJitter = true,
            // ShouldRetryAfterHeader = true（既定値）が内部で設定する DelayGenerator は
            // Retry-After ヘッダーの値をそのまま採用し、MaxDelay で頭打ちにしない
            // （Microsoft.Extensions.Http.Resilience 10.7.0 で確認済みの挙動）。
            // 「長すぎるリトライを避ける」要件を満たすため、ここでは無効化し、
            // 下記の DelayGenerator で Retry-After を解釈しつつ MaxDelay に丸める
            ShouldRetryAfterHeader = false,
            DelayGenerator = args =>
            {
                RetryConditionHeaderValue? retryAfter = args.Outcome.Result?.Headers.RetryAfter;
                TimeSpan? delay = retryAfter switch
                {
                    { Delta: { } d } => d,
                    { Date: { } date } => date - DateTimeOffset.UtcNow,
                    _ => null,
                };
                return ValueTask.FromResult(delay is { } d2 && d2 > TimeSpan.Zero
                    ? (TimeSpan?)(d2 > MaxDelay ? MaxDelay : d2)
                    : null);
            },
        };

        builder.AddRetry(options);
    }
}
