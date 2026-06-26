using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;

namespace GitHubWebhookBridge.Actions.Impl;

/// <summary>ping イベントハンドラー。</summary>
public sealed class PingAction : BaseAction<PingEvent>
{
    public PingAction(IDiscordClient d, string wu, string en, PingEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("PingAction は未実装です。");
}

/// <summary>push イベントハンドラー。</summary>
public sealed class PushAction : BaseAction<PushEvent>
{
    public PushAction(IDiscordClient d, string wu, string en, PushEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("PushAction は未実装です。");
}

/// <summary>star イベントハンドラー。</summary>
public sealed class StarAction : BaseAction<StarEvent>
{
    public StarAction(IDiscordClient d, string wu, string en, StarEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("StarAction は未実装です。");
}

/// <summary>fork イベントハンドラー。</summary>
public sealed class ForkAction : BaseAction<ForkEvent>
{
    public ForkAction(IDiscordClient d, string wu, string en, ForkEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("ForkAction は未実装です。");
}

/// <summary>public イベントハンドラー。</summary>
public sealed class PublicAction : BaseAction<PublicEvent>
{
    public PublicAction(IDiscordClient d, string wu, string en, PublicEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("PublicAction は未実装です。");
}

/// <summary>issues イベントハンドラー。</summary>
public sealed class IssuesAction : BaseAction<IssuesEvent>
{
    public IssuesAction(IDiscordClient d, string wu, string en, IssuesEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("IssuesAction は未実装です。");
}

/// <summary>issue_comment イベントハンドラー。</summary>
public sealed class IssueCommentAction : BaseAction<IssueCommentEvent>
{
    public IssueCommentAction(IDiscordClient d, string wu, string en, IssueCommentEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("IssueCommentAction は未実装です。");
}

/// <summary>pull_request イベントハンドラー。</summary>
public sealed class PullRequestAction : BaseAction<PullRequestEvent>
{
    public PullRequestAction(IDiscordClient d, string wu, string en, PullRequestEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("PullRequestAction は未実装です。");
}

/// <summary>pull_request_review イベントハンドラー。</summary>
public sealed class PullRequestReviewAction : BaseAction<PullRequestReviewEvent>
{
    public PullRequestReviewAction(IDiscordClient d, string wu, string en, PullRequestReviewEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("PullRequestReviewAction は未実装です。");
}

/// <summary>pull_request_review_comment イベントハンドラー。</summary>
public sealed class PullRequestReviewCommentAction : BaseAction<PullRequestReviewCommentEvent>
{
    public PullRequestReviewCommentAction(IDiscordClient d, string wu, string en, PullRequestReviewCommentEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("PullRequestReviewCommentAction は未実装です。");
}

/// <summary>pull_request_review_thread イベントハンドラー。</summary>
public sealed class PullRequestReviewThreadAction : BaseAction<PullRequestReviewThreadEvent>
{
    public PullRequestReviewThreadAction(IDiscordClient d, string wu, string en, PullRequestReviewThreadEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("PullRequestReviewThreadAction は未実装です。");
}

/// <summary>discussion イベントハンドラー。</summary>
public sealed class DiscussionAction : BaseAction<DiscussionEvent>
{
    public DiscussionAction(IDiscordClient d, string wu, string en, DiscussionEvent e, IMessageCacheService c, IGitHubUserMapManager u, ILogger l)
        : base(d, wu, en, e, c, u, l) { }

    /// <inheritdoc/>
    public override Task RunAsync() => throw new NotImplementedException("DiscussionAction は未実装です。");
}
