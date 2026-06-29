using System.Text.Json;
using GitHubWebhookBridge.Actions;
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Actions.Stubs;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>ActionFactory のイベント名→アクション型マッピングテスト。</summary>
public class ActionFactoryTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static ActionFactory CreateFactory()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();
        Mock<ILoggerFactory> loggerFactory = new();

        loggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());

        return new ActionFactory(discord.Object, cache.Object, userMap.Object, loggerFactory.Object);
    }

    /// <summary>ping イベントは PingAction を返す。</summary>
    [Fact]
    public void GetActionPingReturnsPingAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""{"zen":"Keep it logically awesome.","hook_id":1,"hook":{"type":"Repository"}}""").RootElement;

        IAction action = factory.GetAction("ping", body, _webhookUri);

        Assert.IsType<PingAction>(action);
    }

    /// <summary>push イベントは PushAction を返す。</summary>
    [Fact]
    public void GetActionPushReturnsPushAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""{"ref":"refs/heads/main","commits":[],"pusher":{"name":"u","email":"u@e"},"repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},"sender":{"login":"u","id":1}}""").RootElement;

        IAction action = factory.GetAction("push", body, _webhookUri);

        Assert.IsType<PushAction>(action);
    }

    /// <summary>star イベントは StarAction を返す。</summary>
    [Fact]
    public void GetActionStarReturnsStarAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""{"action":"created","repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},"sender":{"login":"u","id":1}}""").RootElement;

        IAction action = factory.GetAction("star", body, _webhookUri);

        Assert.IsType<StarAction>(action);
    }

    /// <summary>fork イベントは ForkAction を返す。</summary>
    [Fact]
    public void GetActionForkReturnsForkAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""{"forkee":{"full_name":"u/r","html_url":"https://github.com/u/r","owner":{"login":"u","id":2}},"repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},"sender":{"login":"u","id":1}}""").RootElement;

        IAction action = factory.GetAction("fork", body, _webhookUri);

        Assert.IsType<ForkAction>(action);
    }

    /// <summary>public イベントは PublicAction を返す。</summary>
    [Fact]
    public void GetActionPublicReturnsPublicAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""{"repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},"sender":{"login":"u","id":1}}""").RootElement;

        IAction action = factory.GetAction("public", body, _webhookUri);

        Assert.IsType<PublicAction>(action);
    }

    /// <summary>pull_request_review_comment イベントは PullRequestReviewCommentAction を返す。</summary>
    [Fact]
    public void GetActionPullRequestReviewCommentReturnsPrReviewCommentAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""
        {
          "action":"created",
          "comment":{"id":1,"body":"comment","html_url":"https://github.com/o/r/pull/1#discussion_r1","user":{"login":"u","id":2},"pull_request_review_id":1},
          "pull_request":{"number":1,"title":"PR","html_url":"https://github.com/o/r/pull/1","user":{"login":"owner","id":3}},
          "repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},
          "sender":{"login":"u","id":2}
        }
        """).RootElement;

        IAction action = factory.GetAction("pull_request_review_comment", body, _webhookUri);

        Assert.IsType<PullRequestReviewCommentAction>(action);
    }

    /// <summary>pull_request_review_thread イベントは PullRequestReviewThreadAction を返す。</summary>
    [Fact]
    public void GetActionPullRequestReviewThreadReturnsPrReviewThreadAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""
        {
          "action":"resolved",
          "thread":{"node_id":"RT_1","resolved":true},
          "pull_request":{"number":1,"title":"PR","html_url":"https://github.com/o/r/pull/1","user":{"login":"owner","id":3}},
          "repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},
          "sender":{"login":"u","id":2}
        }
        """).RootElement;

        IAction action = factory.GetAction("pull_request_review_thread", body, _webhookUri);

        Assert.IsType<PullRequestReviewThreadAction>(action);
    }

    /// <summary>pull_request_review イベントは PullRequestReviewAction を返す。</summary>
    [Fact]
    public void GetActionPullRequestReviewReturnsPrReviewAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""
        {
          "action":"submitted",
          "review":{"id":1,"state":"approved","body":null,"html_url":"https://github.com/o/r/pull/1#pullrequestreview-1","user":{"login":"reviewer","id":4}},
          "pull_request":{"number":1,"title":"PR","html_url":"https://github.com/o/r/pull/1","user":{"login":"owner","id":3}},
          "repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},
          "sender":{"login":"reviewer","id":4}
        }
        """).RootElement;

        IAction action = factory.GetAction("pull_request_review", body, _webhookUri);

        Assert.IsType<PullRequestReviewAction>(action);
    }

    /// <summary>pull_request イベントは PullRequestAction を返す。</summary>
    [Fact]
    public void GetActionPullRequestReturnsPullRequestAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""
        {
          "action":"opened",
          "number":1,
          "pull_request":{"number":1,"title":"PR","html_url":"https://github.com/o/r/pull/1","user":{"login":"u","id":2},"draft":false,"state":"open","base":{"repo":{"full_name":"o/r"}}},
          "repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},
          "sender":{"login":"u","id":2}
        }
        """).RootElement;

        IAction action = factory.GetAction("pull_request", body, _webhookUri);

        Assert.IsType<PullRequestAction>(action);
    }

    /// <summary>issue_comment イベントは IssueCommentAction を返す。</summary>
    [Fact]
    public void GetActionIssueCommentReturnsIssueCommentAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""
        {
          "action":"created",
          "issue":{"number":1,"title":"Issue","html_url":"https://github.com/o/r/issues/1","user":{"login":"u","id":2}},
          "comment":{"id":1,"body":"comment","html_url":"https://github.com/o/r/issues/1#issuecomment-1","user":{"login":"u","id":2}},
          "repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},
          "sender":{"login":"u","id":2}
        }
        """).RootElement;

        IAction action = factory.GetAction("issue_comment", body, _webhookUri);

        Assert.IsType<IssueCommentAction>(action);
    }

    /// <summary>issues イベントは IssuesAction を返す。</summary>
    [Fact]
    public void GetActionIssuesReturnsIssuesAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""
        {
          "action":"opened",
          "issue":{"number":1,"title":"Issue","html_url":"https://github.com/o/r/issues/1","user":{"login":"u","id":2},"state":"open"},
          "repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},
          "sender":{"login":"u","id":2}
        }
        """).RootElement;

        IAction action = factory.GetAction("issues", body, _webhookUri);

        Assert.IsType<IssuesAction>(action);
    }

    /// <summary>discussion イベントは DiscussionAction を返す。</summary>
    [Fact]
    public void GetActionDiscussionReturnsDiscussionAction()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("""
        {
          "action":"created",
          "discussion":{"number":1,"title":"Discussion","html_url":"https://github.com/o/r/discussions/1","body":"body","user":{"login":"u","id":2},"category":{"name":"General"}},
          "repository":{"full_name":"o/r","html_url":"https://github.com/o/r"},
          "sender":{"login":"u","id":2}
        }
        """).RootElement;

        IAction action = factory.GetAction("discussion", body, _webhookUri);

        Assert.IsType<DiscussionAction>(action);
    }

    /// <summary>未知のイベント名は NotImplementedException をスローする。</summary>
    [Fact]
    public void GetActionUnknownEventThrowsNotImplementedException()
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("{}").RootElement;

        Assert.Throws<NotImplementedException>(
            () => factory.GetAction("totally_unknown_event_xyz", body, _webhookUri));
    }

    /// <summary>
    /// スタブ実装の全 46 イベントは対応するスタブ型を返す。
    /// 各スタブは空の JSON body でインスタンス化できる。
    /// </summary>
    [Theory]
    [InlineData("branch_protection_rule", typeof(BranchProtectionRuleAction))]
    [InlineData("check_run", typeof(CheckRunAction))]
    [InlineData("check_suite", typeof(CheckSuiteAction))]
    [InlineData("code_scanning_alert", typeof(CodeScanningAlertAction))]
    [InlineData("commit_comment", typeof(CommitCommentAction))]
    [InlineData("create", typeof(CreateAction))]
    [InlineData("delete", typeof(DeleteAction))]
    [InlineData("dependabot_alert", typeof(DependabotAlertAction))]
    [InlineData("deploy_key", typeof(DeployKeyAction))]
    [InlineData("deployment", typeof(DeploymentAction))]
    [InlineData("deployment_review", typeof(DeploymentReviewAction))]
    [InlineData("deployment_status", typeof(DeploymentStatusAction))]
    [InlineData("discussion_comment", typeof(DiscussionCommentAction))]
    [InlineData("github_app_authorization", typeof(GithubAppAuthorizationAction))]
    [InlineData("gollum", typeof(GollumAction))]
    [InlineData("installation", typeof(InstallationAction))]
    [InlineData("installation_repositories", typeof(InstallationRepositoriesAction))]
    [InlineData("label", typeof(LabelAction))]
    [InlineData("marketplace_purchase", typeof(MarketplacePurchaseAction))]
    [InlineData("member", typeof(MemberAction))]
    [InlineData("membership", typeof(MembershipAction))]
    [InlineData("merge_group", typeof(MergeGroupAction))]
    [InlineData("meta", typeof(MetaAction))]
    [InlineData("milestone", typeof(MilestoneAction))]
    [InlineData("org_block", typeof(OrgBlockAction))]
    [InlineData("organization", typeof(OrganizationAction))]
    [InlineData("package", typeof(PackageAction))]
    [InlineData("page_build", typeof(PageBuildAction))]
    [InlineData("project", typeof(ProjectAction))]
    [InlineData("project_card", typeof(ProjectCardAction))]
    [InlineData("project_column", typeof(ProjectColumnAction))]
    [InlineData("projects_v2_item", typeof(ProjectsV2ItemAction))]
    [InlineData("release", typeof(ReleaseAction))]
    [InlineData("repository", typeof(RepositoryAction))]
    [InlineData("repository_dispatch", typeof(RepositoryDispatchAction))]
    [InlineData("repository_import", typeof(RepositoryImportAction))]
    [InlineData("repository_vulnerability_alert", typeof(RepositoryVulnerabilityAlertAction))]
    [InlineData("security_advisory", typeof(SecurityAdvisoryAction))]
    [InlineData("sponsorship", typeof(SponsorshipAction))]
    [InlineData("status", typeof(StatusAction))]
    [InlineData("team", typeof(TeamAction))]
    [InlineData("team_add", typeof(TeamAddAction))]
    [InlineData("watch", typeof(WatchAction))]
    [InlineData("workflow_dispatch", typeof(WorkflowDispatchAction))]
    [InlineData("workflow_job", typeof(WorkflowJobAction))]
    [InlineData("workflow_run", typeof(WorkflowRunAction))]
    public void GetActionStubEventReturnsExpectedType(string eventName, Type expectedType)
    {
        ActionFactory factory = CreateFactory();
        JsonElement body = JsonDocument.Parse("{}").RootElement;

        IAction action = factory.GetAction(eventName, body, _webhookUri);

        Assert.IsType(expectedType, action);
    }
}
