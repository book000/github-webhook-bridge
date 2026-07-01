namespace GitHubWebhookBridge.Utils;

/// <summary>Class defining Discord Embed color constants. Ported from the TypeScript version embed-colors.ts</summary>
public static class EmbedColors
{
    /// <summary>Color for an unknown event (black)</summary>
    public const int Unknown = 0x000000;

    // PullRequest

    /// <summary>Color for a pull request opened (green)</summary>
    public const int PullRequestOpened = 0x2ecc71;

    /// <summary>Color for a pull request merged (black)</summary>
    public const int PullRequestMerged = 0x000000;

    /// <summary>Color for a pull request closed (gray)</summary>
    public const int PullRequestClosed = 0x95a5a6;

    /// <summary>Color for a pull request reopened (blue)</summary>
    public const int PullRequestReopened = 0x3498db;

    /// <summary>Color for a pull request assigned (orange)</summary>
    public const int PullRequestAssigned = 0xf39c12;

    /// <summary>Color for a pull request unassigned (orange)</summary>
    public const int PullRequestUnassigned = 0xf39c12;

    /// <summary>Color for a pull request review requested (purple)</summary>
    public const int PullRequestReviewRequested = 0x9b59b6;

    /// <summary>Color for a pull request review request removed (purple)</summary>
    public const int PullRequestReviewRequestRemoved = 0x9b59b6;

    /// <summary>Color for a pull request labeled (blue)</summary>
    public const int PullRequestLabeled = 0x3498db;

    /// <summary>Color for a pull request unlabeled (blue)</summary>
    public const int PullRequestUnlabeled = 0x3498db;

    /// <summary>Color for a pull request edited (blue)</summary>
    public const int PullRequestEdited = 0x3498db;

    /// <summary>Color for a pull request ready for review (green)</summary>
    public const int PullRequestReadyForReview = 0x2ecc71;

    /// <summary>Color for a pull request locked (dark gray)</summary>
    public const int PullRequestLocked = 0x7f8c8d;

    /// <summary>Color for a pull request unlocked (dark gray)</summary>
    public const int PullRequestUnlocked = 0x7f8c8d;

    /// <summary>Color for a pull request auto-merge enabled (green)</summary>
    public const int PullRequestAutoMergeEnabled = 0x2ecc71;

    /// <summary>Color for a pull request auto-merge disabled (red)</summary>
    public const int PullRequestAutoMergeDisabled = 0xe74c3c;

    /// <summary>Color for a pull request converted to draft (gray)</summary>
    public const int PullRequestConvertedToDraft = 0x95a5a6;

    /// <summary>Color for a pull request demilestoned (gray)</summary>
    public const int PullRequestDemilestoned = 0x95a5a6;

    /// <summary>Color for a pull request milestoned (blue)</summary>
    public const int PullRequestMilestoned = 0x3498db;

    /// <summary>Color for a pull request added to the merge queue (blue)</summary>
    public const int PullRequestEnqueued = 0x3498db;

    /// <summary>Color for a pull request removed from the merge queue (blue)</summary>
    public const int PullRequestDequeued = 0x3498db;

    // PullRequestReview

    /// <summary>Color for a pull request review approved (green)</summary>
    public const int PullRequestReviewApproved = 0x2ecc71;

    /// <summary>Color for a pull request review requesting changes (orange)</summary>
    public const int PullRequestReviewChangesRequested = 0xf39c12;

    /// <summary>Color for a pull request review dismissed (red)</summary>
    public const int PullRequestReviewDismissed = 0xe74c3c;

    /// <summary>Color for a pull request review edited (blue)</summary>
    public const int PullRequestReviewEdited = 0x3498db;

    // PullRequestReviewComment

    /// <summary>Color for a pull request review comment created (green)</summary>
    public const int PullRequestReviewCommentCreated = 0x2ecc71;

    /// <summary>Color for a pull request review comment edited (blue)</summary>
    public const int PullRequestReviewCommentEdited = 0x3498db;

    /// <summary>Color for a pull request review comment deleted (red)</summary>
    public const int PullRequestReviewCommentDeleted = 0xe74c3c;

    // PullRequestReviewThread

    /// <summary>Color for a pull request review thread resolved (green)</summary>
    public const int PullRequestReviewThreadResolved = 0x2ecc71;

    /// <summary>Color for a pull request review thread unresolved (red)</summary>
    public const int PullRequestReviewThreadUnresolved = 0xe74c3c;

    // Issues

    /// <summary>Color for an issue opened (green)</summary>
    public const int IssueOpened = 0x2ecc71;

    /// <summary>Color for an issue closed (gray)</summary>
    public const int IssueClosed = 0x95a5a6;

    /// <summary>Color for an issue reopened (blue)</summary>
    public const int IssueReopened = 0x3498db;

    /// <summary>Color for an issue assigned (orange)</summary>
    public const int IssueAssigned = 0xf39c12;

    /// <summary>Color for an issue unassigned (orange)</summary>
    public const int IssueUnassigned = 0xf39c12;

    /// <summary>Color for an issue labeled (blue)</summary>
    public const int IssueLabeled = 0x3498db;

    /// <summary>Color for an issue unlabeled (blue)</summary>
    public const int IssueUnlabeled = 0x3498db;

    /// <summary>Color for an issue edited (blue)</summary>
    public const int IssueEdited = 0x3498db;

    /// <summary>Color for an issue locked (dark gray)</summary>
    public const int IssueLocked = 0x7f8c8d;

    /// <summary>Color for an issue unlocked (dark gray)</summary>
    public const int IssueUnlocked = 0x7f8c8d;

    /// <summary>Color for an issue milestoned (blue)</summary>
    public const int IssueMilestoned = 0x3498db;

    /// <summary>Color for an issue demilestoned (gray)</summary>
    public const int IssueDemilestoned = 0x95a5a6;

    /// <summary>Color for an issue transferred to another repository (gray)</summary>
    public const int IssueTransferred = 0x95a5a6;

    /// <summary>Color for an issue pinned (green)</summary>
    public const int IssuePinned = 0x2ecc71;

    /// <summary>Color for an issue unpinned (red)</summary>
    public const int IssueUnpinned = 0xe74c3c;

    /// <summary>Color for an issue deleted (red)</summary>
    public const int IssueDeleted = 0xe74c3c;

    // IssueComment

    /// <summary>Color for an issue comment created (blue)</summary>
    public const int IssueCommentCreated = 0x3498db;

    /// <summary>Color for an issue comment edited (blue)</summary>
    public const int IssueCommentEdited = 0x3498db;

    /// <summary>Color for an issue comment deleted (red)</summary>
    public const int IssueCommentDeleted = 0xe74c3c;

    // Repository

    /// <summary>Color for a repository starred (gold)</summary>
    public const int Star = 0xffd700;

    /// <summary>Color for a repository unstarred (purple)</summary>
    public const int Unstar = 0x9b59b6;

    /// <summary>Color for a repository forked (green)</summary>
    public const int Fork = 0x2ecc71;

    /// <summary>Color for a push to a repository (green)</summary>
    public const int Push = 0x2ecc71;

    /// <summary>Color for a Webhook ping event (gray)</summary>
    public const int Ping = 0x95a5a6;

    /// <summary>Color for a repository made public (green)</summary>
    public const int Public = 0x2ecc71;

    // Discussion

    /// <summary>Color for a discussion created (green)</summary>
    public const int DiscussionCreated = 0x2ecc71;

    /// <summary>Color for a discussion edited (blue)</summary>
    public const int DiscussionEdited = 0x3498db;

    /// <summary>Color for a discussion deleted (red)</summary>
    public const int DiscussionDeleted = 0xe74c3c;

    /// <summary>Color for a discussion pinned (green)</summary>
    public const int DiscussionPinned = 0x2ecc71;

    /// <summary>Color for a discussion unpinned (red)</summary>
    public const int DiscussionUnpinned = 0xe74c3c;

    /// <summary>Color for a discussion labeled (blue)</summary>
    public const int DiscussionLabeled = 0x3498db;

    /// <summary>Color for a discussion unlabeled (blue)</summary>
    public const int DiscussionUnlabeled = 0x3498db;

    /// <summary>Color for a discussion transferred to another category (gray)</summary>
    public const int DiscussionTransferred = 0x95a5a6;

    /// <summary>Color for a discussion category changed (blue)</summary>
    public const int DiscussionCategoryChanged = 0x3498db;

    /// <summary>Color for a discussion answer chosen (green)</summary>
    public const int DiscussionAnswered = 0x2ecc71;

    /// <summary>Color for a discussion answer unchosen (red)</summary>
    public const int DiscussionUnanswered = 0xe74c3c;

    /// <summary>Color for a discussion locked (dark gray)</summary>
    public const int DiscussionLocked = 0x7f8c8d;

    /// <summary>Color for a discussion unlocked (dark gray)</summary>
    public const int DiscussionUnlocked = 0x7f8c8d;
}
