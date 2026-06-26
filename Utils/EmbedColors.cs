namespace GitHubWebhookBridge.Utils;

/// <summary>Discord Embed の色定数。TypeScript 版 embed-colors.ts からの移植。</summary>
public static class EmbedColors
{
    public const int Unknown = 0x000000;

    // PullRequest
    public const int PullRequestOpened = 0x2ecc71;
    public const int PullRequestMerged = 0x000000;
    public const int PullRequestClosed = 0x95a5a6;
    public const int PullRequestReopened = 0x3498db;
    public const int PullRequestAssigned = 0xf39c12;
    public const int PullRequestUnassigned = 0xf39c12;
    public const int PullRequestReviewRequested = 0x9b59b6;
    public const int PullRequestReviewRequestRemoved = 0x9b59b6;
    public const int PullRequestLabeled = 0x3498db;
    public const int PullRequestUnlabeled = 0x3498db;
    public const int PullRequestEdited = 0x3498db;
    public const int PullRequestReadyForReview = 0x2ecc71;
    public const int PullRequestLocked = 0x7f8c8d;
    public const int PullRequestUnlocked = 0x7f8c8d;
    public const int PullRequestAutoMergeEnabled = 0x2ecc71;
    public const int PullRequestAutoMergeDisabled = 0xe74c3c;
    public const int PullRequestConvertedToDraft = 0x95a5a6;
    public const int PullRequestDemilestoned = 0x95a5a6;
    public const int PullRequestMilestoned = 0x3498db;
    public const int PullRequestEnqueued = 0x3498db;
    public const int PullRequestDequeued = 0x3498db;

    // PullRequestReview
    public const int PullRequestReviewApproved = 0x2ecc71;
    public const int PullRequestReviewChangesRequested = 0xf39c12;
    public const int PullRequestReviewDismissed = 0xe74c3c;
    public const int PullRequestReviewEdited = 0x3498db;

    // PullRequestReviewComment
    public const int PullRequestReviewCommentCreated = 0x2ecc71;
    public const int PullRequestReviewCommentEdited = 0x3498db;
    public const int PullRequestReviewCommentDeleted = 0xe74c3c;

    // PullRequestReviewThread
    public const int PullRequestReviewThreadResolved = 0x2ecc71;
    public const int PullRequestReviewThreadUnresolved = 0xe74c3c;

    // Issues
    public const int IssueOpened = 0x2ecc71;
    public const int IssueClosed = 0x95a5a6;
    public const int IssueReopened = 0x3498db;
    public const int IssueAssigned = 0xf39c12;
    public const int IssueUnassigned = 0xf39c12;
    public const int IssueLabeled = 0x3498db;
    public const int IssueUnlabeled = 0x3498db;
    public const int IssueEdited = 0x3498db;
    public const int IssueLocked = 0x7f8c8d;
    public const int IssueUnlocked = 0x7f8c8d;
    public const int IssueMilestoned = 0x3498db;
    public const int IssueDemilestoned = 0x95a5a6;
    public const int IssueTransferred = 0x95a5a6;
    public const int IssuePinned = 0x2ecc71;
    public const int IssueUnpinned = 0xe74c3c;
    public const int IssueDeleted = 0xe74c3c;

    // IssueComment
    public const int IssueCommentCreated = 0x3498db;
    public const int IssueCommentEdited = 0x3498db;
    public const int IssueCommentDeleted = 0xe74c3c;

    // Repository
    public const int Star = 0xffd700;
    public const int Unstar = 0x9b59b6;
    public const int Fork = 0x2ecc71;
    public const int Push = 0x2ecc71;
    public const int Ping = 0x95a5a6;
    public const int Public = 0x2ecc71;

    // Discussion
    public const int DiscussionCreated = 0x2ecc71;
    public const int DiscussionEdited = 0x3498db;
    public const int DiscussionDeleted = 0xe74c3c;
    public const int DiscussionPinned = 0x2ecc71;
    public const int DiscussionUnpinned = 0xe74c3c;
    public const int DiscussionLabeled = 0x3498db;
    public const int DiscussionUnlabeled = 0x3498db;
    public const int DiscussionTransferred = 0x95a5a6;
    public const int DiscussionCategoryChanged = 0x3498db;
    public const int DiscussionAnswered = 0x2ecc71;
    public const int DiscussionUnanswered = 0xe74c3c;
    public const int DiscussionLocked = 0x7f8c8d;
    public const int DiscussionUnlocked = 0x7f8c8d;
}
