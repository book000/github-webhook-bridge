using System.Text.Json;
using GitHubWebhookBridge.Utils;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// テスト用 Octokit モデル生成ヘルパー。
/// Octokit モデルは required プロパティを多数持つため、JSON デシリアライズで生成する。
/// </summary>
internal static class TestFixtures
{
    private static readonly JsonSerializerOptions Opts = OctokitJsonOptions.Value;

    /// <summary>User の最小 JSON（login・id をカスタマイズ可）。</summary>
    public static string UserJson(string login = "octocat", long id = 1, string? htmlUrl = null) =>
        $$$"""{"login":"{{{login}}}","id":{{{id}}},"node_id":"U_{{{id}}}","avatar_url":"https://avatars.github.com/u/{{{id}}}","gravatar_id":"","url":"https://api.github.com/users/{{{login}}}","html_url":"{{{htmlUrl ?? $"https://github.com/{login}"}}}","followers_url":"","following_url":"","gists_url":"","starred_url":"","subscriptions_url":"","organizations_url":"","repos_url":"","events_url":"","received_events_url":"","type":"User","site_admin":false}""";

    /// <summary>Repository の最小 JSON。</summary>
    public static string RepoJson(string fullName = "owner/repo", string? htmlUrl = null)
    {
        var parts = fullName.Split('/', 2);
        var name = parts.Length > 1 ? parts[1] : fullName;
        var repoUrl = htmlUrl ?? $"https://github.com/{fullName}";
        var ownerJson = UserJson("owner");
        return $$$"""{"id":1,"node_id":"R_1","name":"{{{name}}}","full_name":"{{{fullName}}}","private":false,"owner":{{{ownerJson}}},"html_url":"{{{repoUrl}}}","fork":false,"url":"","forks_url":"","keys_url":"","collaborators_url":"","teams_url":"","hooks_url":"","issue_events_url":"","events_url":"","assignees_url":"","branches_url":"","tags_url":"","blobs_url":"","git_tags_url":"","git_refs_url":"","trees_url":"","statuses_url":"","languages_url":"","stargazers_url":"","contributors_url":"","subscribers_url":"","subscription_url":"","commits_url":"","git_commits_url":"","comments_url":"","issue_comment_url":"","contents_url":"","compare_url":"","merges_url":"","archive_url":"","downloads_url":"","issues_url":"","pulls_url":"","milestones_url":"","notifications_url":"","labels_url":"","releases_url":"","deployments_url":"","updated_at":"2024-01-01T00:00:00Z","size":0,"stargazers_count":0,"watchers_count":0,"has_issues":true,"has_projects":true,"has_downloads":true,"has_wiki":true,"has_pages":false,"forks_count":0,"archived":false,"open_issues_count":0,"forks":0,"open_issues":0,"watchers":0,"default_branch":"main","is_template":false,"web_commit_signoff_required":false}""";
    }

    /// <summary>Issue の最小 JSON。</summary>
    public static string IssueJson(
        long number = 1, string title = "Test Issue", string state = "open",
        string? htmlUrl = null, string? body = null, bool isPr = false,
        string userLogin = "octocat", long userId = 1)
    {
        var userJson = UserJson(userLogin, userId);
        var prPart = isPr ? ",\"pull_request\":{\"url\":\"\",\"html_url\":\"https://github.com/owner/repo/pull/1\",\"diff_url\":\"\",\"patch_url\":\"\"}" : string.Empty;
        var bodyPart = body is null ? "null" : $"\"{body}\"";
        var url = htmlUrl ?? $"https://github.com/owner/repo/issues/{number}";
        return $$$"""{"url":"","repository_url":"","labels_url":"","comments_url":"","events_url":"","html_url":"{{{url}}}","id":{{{number}}},"node_id":"I_{{{number}}}","number":{{{number}}},"title":"{{{title}}}","user":{{{userJson}}},"labels":[],"state":"{{{state}}}","locked":false,"assignees":[],"comments":0,"created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","author_association":"NONE","body":{{{bodyPart}}}{{{prPart}}}}""";
    }

    /// <summary>SimplePullRequest の最小 JSON。</summary>
    public static string SimplePrJson(
        long number = 1, string title = "Test PR",
        string? htmlUrl = null, string userLogin = "prauthor", long userId = 100)
    {
        var userJson = UserJson(userLogin, userId);
        var repoJson = RepoJson();
        var url = htmlUrl ?? $"https://github.com/owner/repo/pull/{number}";
        return $$$"""{"url":"","id":{{{number}}},"node_id":"PR_{{{number}}}","html_url":"{{{url}}}","diff_url":"","patch_url":"","issue_url":"","number":{{{number}}},"state":"open","locked":false,"title":"{{{title}}}","user":{{{userJson}}},"body":null,"created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","assignees":[],"requested_reviewers":[],"requested_teams":[],"labels":[],"draft":false,"commits_url":"","review_comments_url":"","review_comment_url":"","comments_url":"","statuses_url":"","head":{"label":"feature","ref":"feature","sha":"abc","user":{{{userJson}}},"repo":{{{repoJson}}}},"base":{"label":"main","ref":"main","sha":"def","user":{{{userJson}}},"repo":{{{repoJson}}}},"_links":{"self":{"href":""},"html":{"href":""},"issue":{"href":""},"comments":{"href":""},"review_comments":{"href":""},"review_comment":{"href":""},"commits":{"href":""},"statuses":{"href":""}},"author_association":"OWNER","auto_merge":null,"active_lock_reason":null}""";
    }

    /// <summary>PullRequest (PullRequestEvent.PullRequest) の最小 JSON。</summary>
    public static string PullRequestJson(
        long number = 1, string title = "Test PR",
        string? htmlUrl = null, string? body = null,
        string userLogin = "prauthor", long userId = 100,
        bool draft = false, bool merged = false,
        string headRef = "feature", string baseRef = "main")
    {
        var userJson = UserJson(userLogin, userId);
        var defaultUserJson = UserJson();
        var repoJson = RepoJson();
        var url = htmlUrl ?? $"https://github.com/owner/repo/pull/{number}";
        var bodyPart = body is null ? "null" : $"\"{body}\"";
        var draftStr = draft ? "true" : "false";
        var mergedStr = merged ? "true" : "false";
        return $$$"""{"url":"","id":{{{number}}},"node_id":"PR_{{{number}}}","html_url":"{{{url}}}","diff_url":"","patch_url":"","issue_url":"","number":{{{number}}},"state":"open","locked":false,"title":"{{{title}}}","user":{{{userJson}}},"body":{{{bodyPart}}},"created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","assignees":[],"requested_reviewers":[],"requested_teams":[],"labels":[],"commits_url":"","review_comments_url":"","review_comment_url":"","comments_url":"","statuses_url":"","head":{"label":"{{{headRef}}}","ref":"{{{headRef}}}","sha":"abc","user":{{{userJson}}},"repo":{{{repoJson}}}},"base":{"label":"{{{baseRef}}}","ref":"{{{baseRef}}}","sha":"def","user":{{{defaultUserJson}}},"repo":{{{repoJson}}}},"_links":{"self":{"href":""},"html":{"href":""},"issue":{"href":""},"comments":{"href":""},"review_comments":{"href":""},"review_comment":{"href":""},"commits":{"href":""},"statuses":{"href":""}},"author_association":"OWNER","active_lock_reason":null,"draft":{{{draftStr}}},"merged":{{{mergedStr}}},"mergeable":null,"rebaseable":null,"mergeable_state":"","merged_by":null,"comments":0,"review_comments":0,"maintainer_can_modify":false,"commits":1,"additions":10,"deletions":5,"changed_files":2}""";
    }

    /// <summary>Discussion の最小 JSON。</summary>
    public static string DiscussionJson(
        long number = 1, string title = "Test Discussion",
        string? htmlUrl = null, string? body = null,
        string userLogin = "octocat", long userId = 1)
    {
        var userJson = UserJson(userLogin, userId);
        var url = htmlUrl ?? $"https://github.com/owner/repo/discussions/{number}";
        var bodyPart = body is null ? "null" : $"\"{body}\"";
        return $$$"""{"repository_url":"","category":{"id":1,"repository_id":1,"emoji":":speech_balloon:","name":"General","description":"","created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","slug":"general","is_answerable":false},"answer_html_url":null,"answer_chosen_at":null,"answer_chosen_by":null,"html_url":"{{{url}}}","id":{{{number}}},"node_id":"D_{{{number}}}","number":{{{number}}},"title":"{{{title}}}","user":{{{userJson}}},"state":"open","locked":false,"comments":0,"created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","author_association":"NONE","active_lock_reason":null,"body":{{{bodyPart}}},"reactions":{"url":"","total_count":0,"+1":0,"-1":0,"laugh":0,"hooray":0,"confused":0,"heart":0,"rocket":0,"eyes":0}}""";
    }

    /// <summary>Label の最小 JSON。</summary>
    public static string LabelJson(string name = "bug") =>
        $$$"""{"id":1,"node_id":"L_1","url":"","name":"{{{name}}}","description":"","color":"ee0701","default":false}""";

    /// <summary>Milestone の最小 JSON。</summary>
    public static string MilestoneJson(string title = "v1.0")
    {
        var userJson = UserJson();
        return $$$"""{"url":"","html_url":"","labels_url":"","id":1,"node_id":"M_1","number":1,"title":"{{{title}}}","description":"","creator":{{{userJson}}},"open_issues":0,"closed_issues":0,"state":"open","created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","due_on":null,"closed_at":null}""";
    }

    /// <summary>Review の最小 JSON。nodeId・body をカスタマイズ可能。</summary>
    public static string ReviewJson(long id = 1, string state = "approved", string? htmlUrl = null, string? nodeId = null, string? body = null)
    {
        var reviewerJson = UserJson("reviewer", 2);
        var url = htmlUrl ?? $"https://github.com/owner/repo/pull/1#pullrequestreview-{id}";
        var nid = nodeId ?? $"RV_{id}";
        var bodyStr = body is null ? "null" : $"\"{body}\"";
        // _links の末尾に }}} が連続するため文字列連結で組み立てる
        return "{\"id\":" + id + ",\"node_id\":\"" + nid + "\",\"user\":" + reviewerJson + ",\"body\":" + bodyStr + ",\"commit_id\":\"abc123\",\"submitted_at\":\"2024-01-01T00:00:00Z\",\"state\":\"" + state + "\",\"html_url\":\"" + url + "\",\"pull_request_url\":\"\",\"author_association\":\"COLLABORATOR\",\"_links\":{\"html\":{\"href\":\"\"},\"pull_request\":{\"href\":\"\"}}}";
    }

    /// <summary>PushEvent.Commit の最小 JSON を生成する。</summary>
    public static string CommitJson(string id = "abcdef1234567890", string message = "feat: add feature", string url = "https://github.com/test/repo/commit/abcdef1", string authorName = "octocat")
    {
        // メッセージ内の改行・特殊文字をエスケープする
        var escapedMsg = message.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        return "{\"id\":\"" + id + "\",\"tree_id\":\"abc\",\"distinct\":true,\"message\":\"" + escapedMsg + "\",\"timestamp\":\"2024-01-01T00:00:00Z\",\"url\":\"" + url + "\",\"author\":{\"name\":\"" + authorName + "\",\"email\":\"\",\"username\":\"\"},\"committer\":{\"name\":\"" + authorName + "\",\"email\":\"\",\"username\":\"\"},\"added\":[],\"modified\":[],\"removed\":[]}";
    }

    /// <summary>PingEvent の最小 JSON を生成する。</summary>
    public static string PingEventJson(
        string zen = "Non-blocking is better than blocking.",
        long hookId = 12345,
        string hookType = "Repository",
        string? repoFullName = null,
        string? senderLogin = null)
    {
        var repoStr = repoFullName is null ? "" : $",\"repository\":{RepoJson(repoFullName)}";
        var senderStr = senderLogin is null ? "" : $",\"sender\":{UserJson(senderLogin, 1)}";
        return "{\"zen\":\"" + zen + "\",\"hook_id\":" + hookId + ",\"hook\":{\"type\":\"" + hookType + "\",\"id\":1,\"name\":\"web\",\"active\":true,\"events\":[\"push\"],\"config\":{\"content_type\":\"json\",\"url\":\"\",\"insecure_ssl\":\"0\"},\"url\":\"\",\"ping_url\":\"\",\"deliveries_url\":\"\",\"updated_at\":\"2024-01-01T00:00:00Z\",\"created_at\":\"2024-01-01T00:00:00Z\"}" + repoStr + senderStr + "}";
    }

    /// <summary>
    /// pull_request_review_thread イベントの Thread（node_id・comments）の最小 JSON。
    /// Octokit には対応する強い型プロパティが無く、実装は AdditionalProperties 経由で読み取るため、
    /// 実際の GitHub ペイロード（"review" ではなく "thread"）と同じ形状で用意する
    /// </summary>
    public static string ThreadJson(string nodeId = "PRRT_1", string? commentJson = null) =>
        $$$"""{"node_id":"{{{nodeId}}}","comments":[{{{commentJson ?? ReviewCommentJson()}}}]}""";

    /// <summary>PullRequestReviewComment の最小 JSON。</summary>
    public static string ReviewCommentJson(long id = 1, string body = "Great!", string path = "src/file.cs", string? htmlUrl = null)
    {
        var reviewerJson = UserJson("reviewer", 2);
        var url = htmlUrl ?? $"https://github.com/owner/repo/pull/1#discussion_r{id}";
        return $$$"""{"url":"","pull_request_review_id":1,"id":{{{id}}},"node_id":"RC_{{{id}}}","diff_hunk":"@@ -1,5 +1,5 @@\n test","path":"{{{path}}}","position":1,"original_position":1,"commit_id":"abc","original_commit_id":"abc","user":{{{reviewerJson}}},"body":"{{{body}}}","created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","html_url":"{{{url}}}","pull_request_url":"","author_association":"COLLABORATOR","_links":{"self":{"href":""},"html":{"href":""},"pull_request":{"href":""}},"reactions":{"url":"","total_count":0,"+1":0,"-1":0,"laugh":0,"hooray":0,"confused":0,"heart":0,"rocket":0,"eyes":0},"original_line":1,"side":"RIGHT","original_side":"RIGHT","subject_type":"line"}""";
    }

    /// <summary>IssueComment の最小 JSON。</summary>
    public static string IssueCommentJson(long id = 1, string body = "Nice!", string? htmlUrl = null)
    {
        var userJson = UserJson();
        var url = htmlUrl ?? $"https://github.com/owner/repo/issues/1#issuecomment-{id}";
        return $$$"""{"url":"","html_url":"{{{url}}}","issue_url":"","id":{{{id}}},"node_id":"IC_{{{id}}}","user":{{{userJson}}},"created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","author_association":"NONE","body":"{{{body}}}","reactions":{"url":"","total_count":0,"+1":0,"-1":0,"laugh":0,"hooray":0,"confused":0,"heart":0,"rocket":0,"eyes":0}}""";
    }

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Opts)!;
}
