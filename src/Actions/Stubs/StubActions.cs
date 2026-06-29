using System.Text.Json;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Stubs;

/// <summary>
/// スタブ Action の共通基底クラス。未実装イベントのプレースホルダー。
/// <see cref="BaseAction{TEvent}"/> を使わず <see cref="IAction"/> を直接実装する。
/// </summary>
public abstract class StubAction(
    IDiscordClient discord,
    Uri webhookUrl,
    string eventName,
    JsonElement body,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger logger) : IAction
{
    // ログ出力に使用するロガー。RunAsync でスローする前のデバッグ用途に保持する。
    private readonly ILogger _logger = logger;

    // 残りの DI パラメーターはスタブ実装では使用しないが、
    // ActivatorUtilities.CreateInstance による DI 解決パターンを統一するため受け取る。
    // discard 変数に代入することでコンパイラの未使用警告を抑制する（pragma なし）。
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0052")]
    private readonly IDiscordClient _discord = discord;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0052")]
    private readonly Uri _webhookUrl = webhookUrl;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0052")]
    private readonly IMessageCacheService _cache = cache;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0052")]
    private readonly IGitHubUserMapManager _userMapManager = userMapManager;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0052")]
    private readonly JsonElement _body = body;

    /// <summary>スタブがハンドルするイベント名。</summary>
    protected string EventName { get; } = eventName;

    /// <summary>未実装のイベントハンドラー。常に <see cref="NotImplementedException"/> をスローする。</summary>
    /// <returns>このメソッドは常に例外をスローするため、値を返さない。</returns>
    public virtual Task RunAsync()
    {
        _logger.LogWarning("Unimplemented event handler invoked for event '{EventName}'.", EventName);
        throw new NotImplementedException($"Event '{EventName}' is not yet implemented.");
    }
}

/// <summary>branch_protection_rule イベントのスタブハンドラー。</summary>
public sealed class BranchProtectionRuleAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>check_run イベントのスタブハンドラー。</summary>
public sealed class CheckRunAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>check_suite イベントのスタブハンドラー。</summary>
public sealed class CheckSuiteAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>code_scanning_alert イベントのスタブハンドラー。</summary>
public sealed class CodeScanningAlertAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>commit_comment イベントのスタブハンドラー。</summary>
public sealed class CommitCommentAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>create イベントのスタブハンドラー。</summary>
public sealed class CreateAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>delete イベントのスタブハンドラー。</summary>
public sealed class DeleteAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>dependabot_alert イベントのスタブハンドラー。</summary>
public sealed class DependabotAlertAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>deploy_key イベントのスタブハンドラー。</summary>
public sealed class DeployKeyAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>deployment イベントのスタブハンドラー。</summary>
public sealed class DeploymentAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>deployment_review イベントのスタブハンドラー。</summary>
public sealed class DeploymentReviewAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>deployment_status イベントのスタブハンドラー。</summary>
public sealed class DeploymentStatusAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>discussion_comment イベントのスタブハンドラー。</summary>
public sealed class DiscussionCommentAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>github_app_authorization イベントのスタブハンドラー。</summary>
public sealed class GithubAppAuthorizationAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>gollum イベントのスタブハンドラー。</summary>
public sealed class GollumAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>installation イベントのスタブハンドラー。</summary>
public sealed class InstallationAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>installation_repositories イベントのスタブハンドラー。</summary>
public sealed class InstallationRepositoriesAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>label イベントのスタブハンドラー。</summary>
public sealed class LabelAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>marketplace_purchase イベントのスタブハンドラー。</summary>
public sealed class MarketplacePurchaseAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>member イベントのスタブハンドラー。</summary>
public sealed class MemberAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>membership イベントのスタブハンドラー。</summary>
public sealed class MembershipAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>merge_group イベントのスタブハンドラー。</summary>
public sealed class MergeGroupAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>meta イベントのスタブハンドラー。</summary>
public sealed class MetaAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>milestone イベントのスタブハンドラー。</summary>
public sealed class MilestoneAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>org_block イベントのスタブハンドラー。</summary>
public sealed class OrgBlockAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>organization イベントのスタブハンドラー。</summary>
public sealed class OrganizationAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>package イベントのスタブハンドラー。</summary>
public sealed class PackageAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>page_build イベントのスタブハンドラー。</summary>
public sealed class PageBuildAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>project イベントのスタブハンドラー。</summary>
public sealed class ProjectAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>project_card イベントのスタブハンドラー。</summary>
public sealed class ProjectCardAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>project_column イベントのスタブハンドラー。</summary>
public sealed class ProjectColumnAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>projects_v2_item イベントのスタブハンドラー。</summary>
public sealed class ProjectsV2ItemAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>release イベントのスタブハンドラー。</summary>
public sealed class ReleaseAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>repository イベントのスタブハンドラー。</summary>
public sealed class RepositoryAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>repository_dispatch イベントのスタブハンドラー。</summary>
public sealed class RepositoryDispatchAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>repository_import イベントのスタブハンドラー。</summary>
public sealed class RepositoryImportAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>repository_vulnerability_alert イベントのスタブハンドラー。</summary>
public sealed class RepositoryVulnerabilityAlertAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>security_advisory イベントのスタブハンドラー。</summary>
public sealed class SecurityAdvisoryAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>sponsorship イベントのスタブハンドラー。</summary>
public sealed class SponsorshipAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>status イベントのスタブハンドラー。</summary>
public sealed class StatusAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>team イベントのスタブハンドラー。</summary>
public sealed class TeamAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>team_add イベントのスタブハンドラー。</summary>
public sealed class TeamAddAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>watch イベントのスタブハンドラー。</summary>
public sealed class WatchAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>workflow_dispatch イベントのスタブハンドラー。</summary>
public sealed class WorkflowDispatchAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>workflow_job イベントのスタブハンドラー。</summary>
public sealed class WorkflowJobAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}

/// <summary>workflow_run イベントのスタブハンドラー。</summary>
public sealed class WorkflowRunAction(IDiscordClient discord, Uri webhookUrl, string eventName, JsonElement body, IMessageCacheService cache, IGitHubUserMapManager userMapManager, ILogger logger) : StubAction(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{
}
