namespace GitHubWebhookBridge.Utils;

/// <summary>Discord Embed の色定数を定義するクラス。TypeScript 版 embed-colors.ts からの移植。</summary>
public static class EmbedColors
{
    /// <summary>不明なイベントの色（黒）を示す。</summary>
    public const int Unknown = 0x000000;

    // PullRequest

    /// <summary>プルリクエストがオープンされた色（緑）を示す。</summary>
    public const int PullRequestOpened = 0x2ecc71;

    /// <summary>プルリクエストがマージされた色（黒）を示す。</summary>
    public const int PullRequestMerged = 0x000000;

    /// <summary>プルリクエストがクローズされた色（グレー）を示す。</summary>
    public const int PullRequestClosed = 0x95a5a6;

    /// <summary>プルリクエストが再オープンされた色（青）を示す。</summary>
    public const int PullRequestReopened = 0x3498db;

    /// <summary>プルリクエストにアサインされた色（オレンジ）を示す。</summary>
    public const int PullRequestAssigned = 0xf39c12;

    /// <summary>プルリクエストのアサインが解除された色（オレンジ）を示す。</summary>
    public const int PullRequestUnassigned = 0xf39c12;

    /// <summary>プルリクエストにレビューが依頼された色（紫）を示す。</summary>
    public const int PullRequestReviewRequested = 0x9b59b6;

    /// <summary>プルリクエストのレビュー依頼が取り消された色（紫）を示す。</summary>
    public const int PullRequestReviewRequestRemoved = 0x9b59b6;

    /// <summary>プルリクエストにラベルが付与された色（青）を示す。</summary>
    public const int PullRequestLabeled = 0x3498db;

    /// <summary>プルリクエストのラベルが削除された色（青）を示す。</summary>
    public const int PullRequestUnlabeled = 0x3498db;

    /// <summary>プルリクエストが編集された色（青）を示す。</summary>
    public const int PullRequestEdited = 0x3498db;

    /// <summary>プルリクエストがレビュー準備完了になった色（緑）を示す。</summary>
    public const int PullRequestReadyForReview = 0x2ecc71;

    /// <summary>プルリクエストがロックされた色（暗いグレー）を示す。</summary>
    public const int PullRequestLocked = 0x7f8c8d;

    /// <summary>プルリクエストのロックが解除された色（暗いグレー）を示す。</summary>
    public const int PullRequestUnlocked = 0x7f8c8d;

    /// <summary>プルリクエストの自動マージが有効化された色（緑）を示す。</summary>
    public const int PullRequestAutoMergeEnabled = 0x2ecc71;

    /// <summary>プルリクエストの自動マージが無効化された色（赤）を示す。</summary>
    public const int PullRequestAutoMergeDisabled = 0xe74c3c;

    /// <summary>プルリクエストがドラフトに変換された色（グレー）を示す。</summary>
    public const int PullRequestConvertedToDraft = 0x95a5a6;

    /// <summary>プルリクエストのマイルストーンが解除された色（グレー）を示す。</summary>
    public const int PullRequestDemilestoned = 0x95a5a6;

    /// <summary>プルリクエストにマイルストーンが設定された色（青）を示す。</summary>
    public const int PullRequestMilestoned = 0x3498db;

    /// <summary>プルリクエストがマージキューに追加された色（青）を示す。</summary>
    public const int PullRequestEnqueued = 0x3498db;

    /// <summary>プルリクエストがマージキューから削除された色（青）を示す。</summary>
    public const int PullRequestDequeued = 0x3498db;

    // PullRequestReview

    /// <summary>プルリクエストレビューが承認された色（緑）を示す。</summary>
    public const int PullRequestReviewApproved = 0x2ecc71;

    /// <summary>プルリクエストレビューで変更が要求された色（オレンジ）を示す。</summary>
    public const int PullRequestReviewChangesRequested = 0xf39c12;

    /// <summary>プルリクエストレビューが却下された色（赤）を示す。</summary>
    public const int PullRequestReviewDismissed = 0xe74c3c;

    /// <summary>プルリクエストレビューが編集された色（青）を示す。</summary>
    public const int PullRequestReviewEdited = 0x3498db;

    // PullRequestReviewComment

    /// <summary>プルリクエストレビューコメントが作成された色（緑）を示す。</summary>
    public const int PullRequestReviewCommentCreated = 0x2ecc71;

    /// <summary>プルリクエストレビューコメントが編集された色（青）を示す。</summary>
    public const int PullRequestReviewCommentEdited = 0x3498db;

    /// <summary>プルリクエストレビューコメントが削除された色（赤）を示す。</summary>
    public const int PullRequestReviewCommentDeleted = 0xe74c3c;

    // PullRequestReviewThread

    /// <summary>プルリクエストレビュースレッドが解決された色（緑）を示す。</summary>
    public const int PullRequestReviewThreadResolved = 0x2ecc71;

    /// <summary>プルリクエストレビュースレッドの解決が取り消された色（赤）を示す。</summary>
    public const int PullRequestReviewThreadUnresolved = 0xe74c3c;

    // Issues

    /// <summary>Issue がオープンされた色（緑）を示す。</summary>
    public const int IssueOpened = 0x2ecc71;

    /// <summary>Issue がクローズされた色（グレー）を示す。</summary>
    public const int IssueClosed = 0x95a5a6;

    /// <summary>Issue が再オープンされた色（青）を示す。</summary>
    public const int IssueReopened = 0x3498db;

    /// <summary>Issue にアサインされた色（オレンジ）を示す。</summary>
    public const int IssueAssigned = 0xf39c12;

    /// <summary>Issue のアサインが解除された色（オレンジ）を示す。</summary>
    public const int IssueUnassigned = 0xf39c12;

    /// <summary>Issue にラベルが付与された色（青）を示す。</summary>
    public const int IssueLabeled = 0x3498db;

    /// <summary>Issue のラベルが削除された色（青）を示す。</summary>
    public const int IssueUnlabeled = 0x3498db;

    /// <summary>Issue が編集された色（青）を示す。</summary>
    public const int IssueEdited = 0x3498db;

    /// <summary>Issue がロックされた色（暗いグレー）を示す。</summary>
    public const int IssueLocked = 0x7f8c8d;

    /// <summary>Issue のロックが解除された色（暗いグレー）を示す。</summary>
    public const int IssueUnlocked = 0x7f8c8d;

    /// <summary>Issue にマイルストーンが設定された色（青）を示す。</summary>
    public const int IssueMilestoned = 0x3498db;

    /// <summary>Issue のマイルストーンが解除された色（グレー）を示す。</summary>
    public const int IssueDemilestoned = 0x95a5a6;

    /// <summary>Issue が別リポジトリへ移転された色（グレー）を示す。</summary>
    public const int IssueTransferred = 0x95a5a6;

    /// <summary>Issue がピン留めされた色（緑）を示す。</summary>
    public const int IssuePinned = 0x2ecc71;

    /// <summary>Issue のピン留めが解除された色（赤）を示す。</summary>
    public const int IssueUnpinned = 0xe74c3c;

    /// <summary>Issue が削除された色（赤）を示す。</summary>
    public const int IssueDeleted = 0xe74c3c;

    // IssueComment

    /// <summary>Issue コメントが作成された色（青）を示す。</summary>
    public const int IssueCommentCreated = 0x3498db;

    /// <summary>Issue コメントが編集された色（青）を示す。</summary>
    public const int IssueCommentEdited = 0x3498db;

    /// <summary>Issue コメントが削除された色（赤）を示す。</summary>
    public const int IssueCommentDeleted = 0xe74c3c;

    // Repository

    /// <summary>リポジトリにスターが付いた色（金）を示す。</summary>
    public const int Star = 0xffd700;

    /// <summary>リポジトリのスターが削除された色（紫）を示す。</summary>
    public const int Unstar = 0x9b59b6;

    /// <summary>リポジトリがフォークされた色（緑）を示す。</summary>
    public const int Fork = 0x2ecc71;

    /// <summary>リポジトリにプッシュされた色（緑）を示す。</summary>
    public const int Push = 0x2ecc71;

    /// <summary>Webhook の ping イベントの色（グレー）を示す。</summary>
    public const int Ping = 0x95a5a6;

    /// <summary>リポジトリが公開された色（緑）を示す。</summary>
    public const int Public = 0x2ecc71;

    // Discussion

    /// <summary>ディスカッションが作成された色（緑）を示す。</summary>
    public const int DiscussionCreated = 0x2ecc71;

    /// <summary>ディスカッションが編集された色（青）を示す。</summary>
    public const int DiscussionEdited = 0x3498db;

    /// <summary>ディスカッションが削除された色（赤）を示す。</summary>
    public const int DiscussionDeleted = 0xe74c3c;

    /// <summary>ディスカッションがピン留めされた色（緑）を示す。</summary>
    public const int DiscussionPinned = 0x2ecc71;

    /// <summary>ディスカッションのピン留めが解除された色（赤）を示す。</summary>
    public const int DiscussionUnpinned = 0xe74c3c;

    /// <summary>ディスカッションにラベルが付与された色（青）を示す。</summary>
    public const int DiscussionLabeled = 0x3498db;

    /// <summary>ディスカッションのラベルが削除された色（青）を示す。</summary>
    public const int DiscussionUnlabeled = 0x3498db;

    /// <summary>ディスカッションが別カテゴリへ移転された色（グレー）を示す。</summary>
    public const int DiscussionTransferred = 0x95a5a6;

    /// <summary>ディスカッションのカテゴリが変更された色（青）を示す。</summary>
    public const int DiscussionCategoryChanged = 0x3498db;

    /// <summary>ディスカッションの回答が選択された色（緑）を示す。</summary>
    public const int DiscussionAnswered = 0x2ecc71;

    /// <summary>ディスカッションの回答選択が解除された色（赤）を示す。</summary>
    public const int DiscussionUnanswered = 0xe74c3c;

    /// <summary>ディスカッションがロックされた色（暗いグレー）を示す。</summary>
    public const int DiscussionLocked = 0x7f8c8d;

    /// <summary>ディスカッションのロックが解除された色（暗いグレー）を示す。</summary>
    public const int DiscussionUnlocked = 0x7f8c8d;
}
