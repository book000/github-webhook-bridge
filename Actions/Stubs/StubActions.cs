using System.Text.Json;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Stubs;

/// <summary>スタブ Action の共通基底クラス。未実装イベントのプレースホルダー。</summary>
public abstract class StubAction(
    IDiscordClient discord,
    Uri webhookUrl,
    string eventName,
    JsonElement body,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILogger logger) : BaseAction<JsonElement>(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
{

    /// <summary>未実装のイベントハンドラー。</summary>
    public override Task RunAsync() => throw new NotImplementedException($"Event '{EventName}' is not yet implemented.");
}

/// <summary>branch_protection_rule イベントのスタブハンドラー。</summary>
public sealed class BranchProtectionRuleAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>check_run イベントのスタブハンドラー。</summary>
public sealed class CheckRunAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>check_suite イベントのスタブハンドラー。</summary>
public sealed class CheckSuiteAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>code_scanning_alert イベントのスタブハンドラー。</summary>
public sealed class CodeScanningAlertAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>commit_comment イベントのスタブハンドラー。</summary>
public sealed class CommitCommentAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>create イベントのスタブハンドラー。</summary>
public sealed class CreateAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>delete イベントのスタブハンドラー。</summary>
public sealed class DeleteAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>dependabot_alert イベントのスタブハンドラー。</summary>
public sealed class DependabotAlertAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>deploy_key イベントのスタブハンドラー。</summary>
public sealed class DeployKeyAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>deployment イベントのスタブハンドラー。</summary>
public sealed class DeploymentAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>deployment_review イベントのスタブハンドラー。</summary>
public sealed class DeploymentReviewAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>deployment_status イベントのスタブハンドラー。</summary>
public sealed class DeploymentStatusAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>discussion_comment イベントのスタブハンドラー。</summary>
public sealed class DiscussionCommentAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>github_app_authorization イベントのスタブハンドラー。</summary>
public sealed class GithubAppAuthorizationAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>gollum イベントのスタブハンドラー。</summary>
public sealed class GollumAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>installation イベントのスタブハンドラー。</summary>
public sealed class InstallationAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>installation_repositories イベントのスタブハンドラー。</summary>
public sealed class InstallationRepositoriesAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>label イベントのスタブハンドラー。</summary>
public sealed class LabelAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>marketplace_purchase イベントのスタブハンドラー。</summary>
public sealed class MarketplacePurchaseAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>member イベントのスタブハンドラー。</summary>
public sealed class MemberAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>membership イベントのスタブハンドラー。</summary>
public sealed class MembershipAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>merge_group イベントのスタブハンドラー。</summary>
public sealed class MergeGroupAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>meta イベントのスタブハンドラー。</summary>
public sealed class MetaAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>milestone イベントのスタブハンドラー。</summary>
public sealed class MilestoneAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>org_block イベントのスタブハンドラー。</summary>
public sealed class OrgBlockAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>organization イベントのスタブハンドラー。</summary>
public sealed class OrganizationAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>package イベントのスタブハンドラー。</summary>
public sealed class PackageAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>page_build イベントのスタブハンドラー。</summary>
public sealed class PageBuildAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>project イベントのスタブハンドラー。</summary>
public sealed class ProjectAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>project_card イベントのスタブハンドラー。</summary>
public sealed class ProjectCardAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>project_column イベントのスタブハンドラー。</summary>
public sealed class ProjectColumnAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>projects_v2_item イベントのスタブハンドラー。</summary>
public sealed class ProjectsV2ItemAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>release イベントのスタブハンドラー。</summary>
public sealed class ReleaseAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>repository イベントのスタブハンドラー。</summary>
public sealed class RepositoryAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>repository_dispatch イベントのスタブハンドラー。</summary>
public sealed class RepositoryDispatchAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>repository_import イベントのスタブハンドラー。</summary>
public sealed class RepositoryImportAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>repository_vulnerability_alert イベントのスタブハンドラー。</summary>
public sealed class RepositoryVulnerabilityAlertAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>security_advisory イベントのスタブハンドラー。</summary>
public sealed class SecurityAdvisoryAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>sponsorship イベントのスタブハンドラー。</summary>
public sealed class SponsorshipAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>status イベントのスタブハンドラー。</summary>
public sealed class StatusAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>team イベントのスタブハンドラー。</summary>
public sealed class TeamAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>team_add イベントのスタブハンドラー。</summary>
public sealed class TeamAddAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>watch イベントのスタブハンドラー。</summary>
public sealed class WatchAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>workflow_dispatch イベントのスタブハンドラー。</summary>
public sealed class WorkflowDispatchAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>workflow_job イベントのスタブハンドラー。</summary>
public sealed class WorkflowJobAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}

/// <summary>workflow_run イベントのスタブハンドラー。</summary>
public sealed class WorkflowRunAction(IDiscordClient d, Uri wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l) : StubAction(d, wu, en, b, c, u, l)
{
}
