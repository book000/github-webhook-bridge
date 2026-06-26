using System.Text.Json;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Stubs;

/// <summary>スタブ Action の共通基底クラス。未実装イベントのプレースホルダー。</summary>
public abstract class StubAction : BaseAction<JsonElement>
{
    protected StubAction(
        IDiscordClient        discord,
        string                webhookUrl,
        string                eventName,
        JsonElement           body,
        IMessageCacheService  cache,
        IGitHubUserMapManager userMapManager,
        ILogger               logger)
        : base(discord, webhookUrl, eventName, body, cache, userMapManager, logger)
    {
    }

    /// <summary>未実装のイベントハンドラー。</summary>
    public override Task RunAsync() => throw new NotImplementedException($"Event '{EventName}' is not yet implemented.");
}

/// <summary>branch_protection_rule イベントのスタブハンドラー。</summary>
public sealed class BranchProtectionRuleAction : StubAction
{
    public BranchProtectionRuleAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>check_run イベントのスタブハンドラー。</summary>
public sealed class CheckRunAction : StubAction
{
    public CheckRunAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>check_suite イベントのスタブハンドラー。</summary>
public sealed class CheckSuiteAction : StubAction
{
    public CheckSuiteAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>code_scanning_alert イベントのスタブハンドラー。</summary>
public sealed class CodeScanningAlertAction : StubAction
{
    public CodeScanningAlertAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>commit_comment イベントのスタブハンドラー。</summary>
public sealed class CommitCommentAction : StubAction
{
    public CommitCommentAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>create イベントのスタブハンドラー。</summary>
public sealed class CreateAction : StubAction
{
    public CreateAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>delete イベントのスタブハンドラー。</summary>
public sealed class DeleteAction : StubAction
{
    public DeleteAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>dependabot_alert イベントのスタブハンドラー。</summary>
public sealed class DependabotAlertAction : StubAction
{
    public DependabotAlertAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>deploy_key イベントのスタブハンドラー。</summary>
public sealed class DeployKeyAction : StubAction
{
    public DeployKeyAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>deployment イベントのスタブハンドラー。</summary>
public sealed class DeploymentAction : StubAction
{
    public DeploymentAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>deployment_review イベントのスタブハンドラー。</summary>
public sealed class DeploymentReviewAction : StubAction
{
    public DeploymentReviewAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>deployment_status イベントのスタブハンドラー。</summary>
public sealed class DeploymentStatusAction : StubAction
{
    public DeploymentStatusAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>discussion_comment イベントのスタブハンドラー。</summary>
public sealed class DiscussionCommentAction : StubAction
{
    public DiscussionCommentAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>github_app_authorization イベントのスタブハンドラー。</summary>
public sealed class GithubAppAuthorizationAction : StubAction
{
    public GithubAppAuthorizationAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>gollum イベントのスタブハンドラー。</summary>
public sealed class GollumAction : StubAction
{
    public GollumAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>installation イベントのスタブハンドラー。</summary>
public sealed class InstallationAction : StubAction
{
    public InstallationAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>installation_repositories イベントのスタブハンドラー。</summary>
public sealed class InstallationRepositoriesAction : StubAction
{
    public InstallationRepositoriesAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>label イベントのスタブハンドラー。</summary>
public sealed class LabelAction : StubAction
{
    public LabelAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>marketplace_purchase イベントのスタブハンドラー。</summary>
public sealed class MarketplacePurchaseAction : StubAction
{
    public MarketplacePurchaseAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>member イベントのスタブハンドラー。</summary>
public sealed class MemberAction : StubAction
{
    public MemberAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>membership イベントのスタブハンドラー。</summary>
public sealed class MembershipAction : StubAction
{
    public MembershipAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>merge_group イベントのスタブハンドラー。</summary>
public sealed class MergeGroupAction : StubAction
{
    public MergeGroupAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>meta イベントのスタブハンドラー。</summary>
public sealed class MetaAction : StubAction
{
    public MetaAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>milestone イベントのスタブハンドラー。</summary>
public sealed class MilestoneAction : StubAction
{
    public MilestoneAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>org_block イベントのスタブハンドラー。</summary>
public sealed class OrgBlockAction : StubAction
{
    public OrgBlockAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>organization イベントのスタブハンドラー。</summary>
public sealed class OrganizationAction : StubAction
{
    public OrganizationAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>package イベントのスタブハンドラー。</summary>
public sealed class PackageAction : StubAction
{
    public PackageAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>page_build イベントのスタブハンドラー。</summary>
public sealed class PageBuildAction : StubAction
{
    public PageBuildAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>project イベントのスタブハンドラー。</summary>
public sealed class ProjectAction : StubAction
{
    public ProjectAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>project_card イベントのスタブハンドラー。</summary>
public sealed class ProjectCardAction : StubAction
{
    public ProjectCardAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>project_column イベントのスタブハンドラー。</summary>
public sealed class ProjectColumnAction : StubAction
{
    public ProjectColumnAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>projects_v2_item イベントのスタブハンドラー。</summary>
public sealed class ProjectsV2ItemAction : StubAction
{
    public ProjectsV2ItemAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>release イベントのスタブハンドラー。</summary>
public sealed class ReleaseAction : StubAction
{
    public ReleaseAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>repository イベントのスタブハンドラー。</summary>
public sealed class RepositoryAction : StubAction
{
    public RepositoryAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>repository_dispatch イベントのスタブハンドラー。</summary>
public sealed class RepositoryDispatchAction : StubAction
{
    public RepositoryDispatchAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>repository_import イベントのスタブハンドラー。</summary>
public sealed class RepositoryImportAction : StubAction
{
    public RepositoryImportAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>repository_vulnerability_alert イベントのスタブハンドラー。</summary>
public sealed class RepositoryVulnerabilityAlertAction : StubAction
{
    public RepositoryVulnerabilityAlertAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>security_advisory イベントのスタブハンドラー。</summary>
public sealed class SecurityAdvisoryAction : StubAction
{
    public SecurityAdvisoryAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>sponsorship イベントのスタブハンドラー。</summary>
public sealed class SponsorshipAction : StubAction
{
    public SponsorshipAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>status イベントのスタブハンドラー。</summary>
public sealed class StatusAction : StubAction
{
    public StatusAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>team イベントのスタブハンドラー。</summary>
public sealed class TeamAction : StubAction
{
    public TeamAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>team_add イベントのスタブハンドラー。</summary>
public sealed class TeamAddAction : StubAction
{
    public TeamAddAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>watch イベントのスタブハンドラー。</summary>
public sealed class WatchAction : StubAction
{
    public WatchAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>workflow_dispatch イベントのスタブハンドラー。</summary>
public sealed class WorkflowDispatchAction : StubAction
{
    public WorkflowDispatchAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>workflow_job イベントのスタブハンドラー。</summary>
public sealed class WorkflowJobAction : StubAction
{
    public WorkflowJobAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}

/// <summary>workflow_run イベントのスタブハンドラー。</summary>
public sealed class WorkflowRunAction : StubAction
{
    public WorkflowRunAction(IDiscordClient d, string wu, string en, JsonElement b, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, b, c, u, l) { }
}
