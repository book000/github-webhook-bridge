using System.Text.Json;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Actions.Stubs;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions;

/// <summary>イベント名から適切な IAction を生成するファクトリ。</summary>
public class ActionFactory(
    IDiscordClient discordClient,
    IMessageCacheService cache,
    IGitHubUserMapManager userMapManager,
    ILoggerFactory loggerFactory) : IActionFactory
{
    private readonly IDiscordClient _discordClient = discordClient;
    private readonly IMessageCacheService _cache = cache;
    private readonly IGitHubUserMapManager _userMapManager = userMapManager;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    private static T Deserialize<T>(JsonElement body)
        => body.Deserialize<T>(_jsonOptions)
           ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");

    private ILogger<T> Logger<T>() => _loggerFactory.CreateLogger<T>();

    /// <summary>
    /// イベント名から適切な IAction インスタンスを生成して返す。
    /// </summary>
    /// <param name="eventName">GitHub Webhook の X-GitHub-Event ヘッダー値。</param>
    /// <param name="body">Webhook ペイロードの JSON 要素。</param>
    /// <param name="webhookUrl">通知先 Discord Webhook URL。</param>
    /// <returns>イベントに対応する <see cref="IAction"/> インスタンス。</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Factory メソッドは全イベント型を参照するため必然的に結合度が高い")]
    public IAction GetAction(string eventName, JsonElement body, Uri webhookUrl)
        => eventName switch
        {
            // ── 実装済み 12 種（デシリアライズあり） ──────────────────────────
            "ping" => new PingAction(_discordClient, webhookUrl, eventName, Deserialize<PingEvent>(body), _cache, _userMapManager, Logger<PingAction>()),
            "push" => new PushAction(_discordClient, webhookUrl, eventName, Deserialize<PushEvent>(body), _cache, _userMapManager, Logger<PushAction>()),
            "star" => new StarAction(_discordClient, webhookUrl, eventName, Deserialize<StarEvent>(body), _cache, _userMapManager, Logger<StarAction>()),
            "fork" => new ForkAction(_discordClient, webhookUrl, eventName, Deserialize<ForkEvent>(body), _cache, _userMapManager, Logger<ForkAction>()),
            "public" => new PublicAction(_discordClient, webhookUrl, eventName, Deserialize<PublicEvent>(body), _cache, _userMapManager, Logger<PublicAction>()),
            "pull_request_review_comment" => new PullRequestReviewCommentAction(_discordClient, webhookUrl, eventName, Deserialize<PullRequestReviewCommentEvent>(body), _cache, _userMapManager, Logger<PullRequestReviewCommentAction>()),
            "pull_request_review_thread" => new PullRequestReviewThreadAction(_discordClient, webhookUrl, eventName, Deserialize<PullRequestReviewThreadEvent>(body), _cache, _userMapManager, Logger<PullRequestReviewThreadAction>()),
            "pull_request_review" => new PullRequestReviewAction(_discordClient, webhookUrl, eventName, Deserialize<PullRequestReviewEvent>(body), _cache, _userMapManager, Logger<PullRequestReviewAction>()),
            "pull_request" => new PullRequestAction(_discordClient, webhookUrl, eventName, Deserialize<PullRequestEvent>(body), _cache, _userMapManager, Logger<PullRequestAction>()),
            "issue_comment" => new IssueCommentAction(_discordClient, webhookUrl, eventName, Deserialize<IssueCommentEvent>(body), _cache, _userMapManager, Logger<IssueCommentAction>()),
            "issues" => new IssuesAction(_discordClient, webhookUrl, eventName, Deserialize<IssuesEvent>(body), _cache, _userMapManager, Logger<IssuesAction>()),
            "discussion" => new DiscussionAction(_discordClient, webhookUrl, eventName, Deserialize<DiscussionEvent>(body), _cache, _userMapManager, Logger<DiscussionAction>()),

            // ── スタブ 46 種（JsonElement 受け渡し） ───────────────────────────
            "branch_protection_rule" => new BranchProtectionRuleAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<BranchProtectionRuleAction>()),
            "check_run" => new CheckRunAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<CheckRunAction>()),
            "check_suite" => new CheckSuiteAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<CheckSuiteAction>()),
            "code_scanning_alert" => new CodeScanningAlertAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<CodeScanningAlertAction>()),
            "commit_comment" => new CommitCommentAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<CommitCommentAction>()),
            "create" => new CreateAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<CreateAction>()),
            "delete" => new DeleteAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<DeleteAction>()),
            "dependabot_alert" => new DependabotAlertAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<DependabotAlertAction>()),
            "deploy_key" => new DeployKeyAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<DeployKeyAction>()),
            "deployment" => new DeploymentAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<DeploymentAction>()),
            "deployment_review" => new DeploymentReviewAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<DeploymentReviewAction>()),
            "deployment_status" => new DeploymentStatusAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<DeploymentStatusAction>()),
            "discussion_comment" => new DiscussionCommentAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<DiscussionCommentAction>()),
            "github_app_authorization" => new GithubAppAuthorizationAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<GithubAppAuthorizationAction>()),
            "gollum" => new GollumAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<GollumAction>()),
            "installation" => new InstallationAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<InstallationAction>()),
            "installation_repositories" => new InstallationRepositoriesAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<InstallationRepositoriesAction>()),
            "label" => new LabelAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<LabelAction>()),
            "marketplace_purchase" => new MarketplacePurchaseAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<MarketplacePurchaseAction>()),
            "member" => new MemberAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<MemberAction>()),
            "membership" => new MembershipAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<MembershipAction>()),
            "merge_group" => new MergeGroupAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<MergeGroupAction>()),
            "meta" => new MetaAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<MetaAction>()),
            "milestone" => new MilestoneAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<MilestoneAction>()),
            "org_block" => new OrgBlockAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<OrgBlockAction>()),
            "organization" => new OrganizationAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<OrganizationAction>()),
            "package" => new PackageAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<PackageAction>()),
            "page_build" => new PageBuildAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<PageBuildAction>()),
            "project" => new ProjectAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<ProjectAction>()),
            "project_card" => new ProjectCardAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<ProjectCardAction>()),
            "project_column" => new ProjectColumnAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<ProjectColumnAction>()),
            "projects_v2_item" => new ProjectsV2ItemAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<ProjectsV2ItemAction>()),
            "release" => new ReleaseAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<ReleaseAction>()),
            "repository" => new RepositoryAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<RepositoryAction>()),
            "repository_dispatch" => new RepositoryDispatchAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<RepositoryDispatchAction>()),
            "repository_import" => new RepositoryImportAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<RepositoryImportAction>()),
            "repository_vulnerability_alert" => new RepositoryVulnerabilityAlertAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<RepositoryVulnerabilityAlertAction>()),
            "security_advisory" => new SecurityAdvisoryAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<SecurityAdvisoryAction>()),
            "sponsorship" => new SponsorshipAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<SponsorshipAction>()),
            "status" => new StatusAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<StatusAction>()),
            "team" => new TeamAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<TeamAction>()),
            "team_add" => new TeamAddAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<TeamAddAction>()),
            "watch" => new WatchAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<WatchAction>()),
            "workflow_dispatch" => new WorkflowDispatchAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<WorkflowDispatchAction>()),
            "workflow_job" => new WorkflowJobAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<WorkflowJobAction>()),
            "workflow_run" => new WorkflowRunAction(_discordClient, webhookUrl, eventName, body, _cache, _userMapManager, Logger<WorkflowRunAction>()),

            _ => throw new NotImplementedException($"Event '{eventName}' is not supported"),
        };
}
