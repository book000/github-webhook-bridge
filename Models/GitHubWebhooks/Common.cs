using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitHubWebhookBridge.Models.GitHubWebhooks
{
    /// <summary>
    /// GitHub ユーザー情報
    /// </summary>
    public class User
    {
        /// <summary>ログイン名</summary>
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        /// <summary>ユーザー ID</summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>アバター画像 URL</summary>
        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        /// <summary>プロフィールページ URL</summary>
        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }

    /// <summary>
    /// GitHub リポジトリ情報
    /// </summary>
    public class Repository
    {
        /// <summary>リポジトリ ID</summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>リポジトリ名</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>オーナー名を含む完全なリポジトリ名</summary>
        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        /// <summary>リポジトリページ URL</summary>
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        /// <summary>プライベートリポジトリかどうか</summary>
        [JsonPropertyName("private")]
        public bool Private { get; set; }

        /// <summary>リポジトリオーナー</summary>
        [JsonPropertyName("owner")]
        public User Owner { get; set; } = new();
    }

    /// <summary>
    /// GitHub App インストール情報
    /// </summary>
    public class Installation
    {
        /// <summary>インストール ID</summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>ノード ID</summary>
        [JsonPropertyName("node_id")]
        public string? NodeId { get; set; }
    }

    /// <summary>
    /// コミット作者情報
    /// </summary>
    public class CommitAuthor
    {
        /// <summary>作者名</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>メールアドレス</summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    /// <summary>
    /// コミット情報
    /// </summary>
    public class Commit
    {
        /// <summary>コミット SHA</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>コミットメッセージ</summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>コミット URL</summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>コミット作者</summary>
        [JsonPropertyName("author")]
        public CommitAuthor Author { get; set; } = new();
    }

    /// <summary>
    /// Issue に関連するプルリクエスト参照
    /// </summary>
    public class IssuePullRequestRef
    {
        /// <summary>プルリクエスト URL</summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    /// <summary>
    /// GitHub Issue 情報
    /// </summary>
    public class Issue
    {
        /// <summary>Issue 番号</summary>
        [JsonPropertyName("number")]
        public int Number { get; set; }

        /// <summary>Issue タイトル</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Issue 本文</summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        /// <summary>Issue 状態（open/closed）</summary>
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        /// <summary>Issue ページ URL</summary>
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        /// <summary>Issue 作成者</summary>
        [JsonPropertyName("user")]
        public User User { get; set; } = new();

        /// <summary>ドラフトかどうか</summary>
        [JsonPropertyName("draft")]
        public bool? Draft { get; set; }

        /// <summary>関連するプルリクエスト参照（PR から作成された Issue の場合）</summary>
        [JsonPropertyName("pull_request")]
        public IssuePullRequestRef? PullRequest { get; set; }
    }

    /// <summary>
    /// コメント情報
    /// </summary>
    public class Comment
    {
        /// <summary>コメント ID</summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>コメント本文</summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        /// <summary>コメントページ URL</summary>
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        /// <summary>コメント投稿者</summary>
        [JsonPropertyName("user")]
        public User User { get; set; } = new();
    }

    /// <summary>
    /// ラベル情報
    /// </summary>
    public class Label
    {
        /// <summary>ラベル名</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>ラベルカラー（16進数）</summary>
        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }

    /// <summary>
    /// マイルストーン情報
    /// </summary>
    public class Milestone
    {
        /// <summary>マイルストーンタイトル</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>マイルストーン番号</summary>
        [JsonPropertyName("number")]
        public int Number { get; set; }
    }

    /// <summary>
    /// プルリクエストのブランチ参照情報
    /// </summary>
    public class PullRequestRef
    {
        /// <summary>ブランチ名</summary>
        [JsonPropertyName("ref")]
        public string Ref { get; set; } = string.Empty;

        /// <summary>コミット SHA</summary>
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;

        /// <summary>リポジトリ情報</summary>
        [JsonPropertyName("repo")]
        public Repository? Repo { get; set; }
    }

    /// <summary>
    /// プルリクエスト情報
    /// </summary>
    public class PullRequest
    {
        /// <summary>プルリクエスト番号</summary>
        [JsonPropertyName("number")]
        public int Number { get; set; }

        /// <summary>プルリクエストタイトル</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>プルリクエスト本文</summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        /// <summary>状態（open/closed）</summary>
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        /// <summary>プルリクエストページ URL</summary>
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        /// <summary>プルリクエスト作成者</summary>
        [JsonPropertyName("user")]
        public User User { get; set; } = new();

        /// <summary>ドラフトかどうか</summary>
        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        /// <summary>マージ済みかどうか</summary>
        [JsonPropertyName("merged")]
        public bool? Merged { get; set; }

        /// <summary>マージコミット SHA</summary>
        [JsonPropertyName("merge_commit_sha")]
        public string? MergeCommitSha { get; set; }

        /// <summary>マージ元ブランチ参照</summary>
        [JsonPropertyName("head")]
        public PullRequestRef Head { get; set; } = new();

        /// <summary>マージ先ブランチ参照</summary>
        [JsonPropertyName("base")]
        public PullRequestRef Base { get; set; } = new();

        /// <summary>追加行数</summary>
        [JsonPropertyName("additions")]
        public int? Additions { get; set; }

        /// <summary>削除行数</summary>
        [JsonPropertyName("deletions")]
        public int? Deletions { get; set; }

        /// <summary>変更ファイル数</summary>
        [JsonPropertyName("changed_files")]
        public int? ChangedFiles { get; set; }

        /// <summary>アサインされたユーザー一覧</summary>
        [JsonPropertyName("assignees")]
        public List<User>? Assignees { get; set; }

        /// <summary>レビュアーとして指定されたユーザー一覧</summary>
        [JsonPropertyName("requested_reviewers")]
        public List<User>? RequestedReviewers { get; set; }
    }

    /// <summary>
    /// プルリクエストレビュー情報
    /// </summary>
    public class Review
    {
        /// <summary>レビュー ID</summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>レビュー本文</summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        /// <summary>レビュー状態（APPROVED/CHANGES_REQUESTED/COMMENTED）</summary>
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        /// <summary>レビューページ URL</summary>
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        /// <summary>レビュー投稿者</summary>
        [JsonPropertyName("user")]
        public User User { get; set; } = new();

        /// <summary>レビュー送信日時</summary>
        [JsonPropertyName("submitted_at")]
        public string? SubmittedAt { get; set; }
    }

    /// <summary>
    /// プルリクエストレビューコメント情報
    /// </summary>
    public class ReviewComment
    {
        /// <summary>コメント ID</summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>コメント本文</summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        /// <summary>コメントページ URL</summary>
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        /// <summary>コメント投稿者</summary>
        [JsonPropertyName("user")]
        public User User { get; set; } = new();

        /// <summary>差分のハンク</summary>
        [JsonPropertyName("diff_hunk")]
        public string? DiffHunk { get; set; }

        /// <summary>コメント対象ファイルパス</summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        /// <summary>関連するプルリクエスト URL</summary>
        [JsonPropertyName("pull_request_url")]
        public string? PullRequestUrl { get; set; }
    }

    /// <summary>
    /// プルリクエストレビュースレッド情報
    /// </summary>
    public class ReviewThread
    {
        /// <summary>スレッドのノード ID</summary>
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = string.Empty;

        /// <summary>スレッドが解決済みかどうか</summary>
        [JsonPropertyName("resolved")]
        public bool Resolved { get; set; }
    }

    /// <summary>
    /// ディスカッションカテゴリ情報
    /// </summary>
    public class DiscussionCategory
    {
        /// <summary>カテゴリ名</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>回答可能かどうか</summary>
        [JsonPropertyName("is_answerable")]
        public bool IsAnswerable { get; set; }
    }

    /// <summary>
    /// ディスカッション情報
    /// </summary>
    public class Discussion
    {
        /// <summary>ディスカッション ID</summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>ディスカッション番号</summary>
        [JsonPropertyName("number")]
        public int Number { get; set; }

        /// <summary>ディスカッションタイトル</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>ディスカッション本文</summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        /// <summary>ディスカッションページ URL</summary>
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        /// <summary>ディスカッション作成者</summary>
        [JsonPropertyName("user")]
        public User User { get; set; } = new();

        /// <summary>ディスカッション状態</summary>
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        /// <summary>ディスカッションカテゴリ</summary>
        [JsonPropertyName("category")]
        public DiscussionCategory? Category { get; set; }
    }

    /// <summary>
    /// ディスカッションコメント情報
    /// </summary>
    public class DiscussionComment
    {
        /// <summary>コメント ID</summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>コメント本文</summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; }

        /// <summary>コメントページ URL</summary>
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        /// <summary>コメント投稿者</summary>
        [JsonPropertyName("user")]
        public User User { get; set; } = new();
    }
}
