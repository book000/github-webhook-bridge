# Action Test Coverage + Coverage Verification Mechanism Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add tests for all 8 untested implemented actions, fix 4 existing test issues, and add a coverage-verification test that fails CI when any implemented action lacks a test class.

**Architecture:** Each missing action gets its own `*Tests.cs` file following the existing pattern (CreateMocks helper + Moq + xUnit). The coverage check is a single `[Fact]` in a new `ActionCoverageTests.cs` that uses reflection to compare `Actions/Impl/` classes against `*Tests.cs` classes — no external tooling needed.

**Tech Stack:** xUnit, Moq, .NET 10, C# reflection.

## Global Constraints

- `dotnet test -c Release` must stay green after every task.
- No `#pragma warning disable` — fix the code, not the warnings.
- Test-only analyzer suppressions go in the `[tests/**/*.cs]` block in `.editorconfig`.
- Code comments: Japanese. Error/log messages: English.
- All action test files go in `tests/GitHubWebhookBridge.Tests/`.
- Match the helper pattern from existing tests (`CreateMocks()` returns `(Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>)`).

---

### Task 1: Fix 4 existing test defects

**Files:**
- Modify: `tests/GitHubWebhookBridge.Tests/MonkeyTests.cs`
- Modify: `tests/GitHubWebhookBridge.Tests/PullRequestActionTests.cs`
- Modify: `tests/GitHubWebhookBridge.Tests/PullRequestReviewActionTests.cs`

**Interfaces:**
- Consumes: nothing new
- Produces: fixed test assertions that downstream tasks can rely on as examples

- [ ] **Step 1: Fix `SanitizeRowKeyLongStringTruncatesToMax512` — tighten the boundary assertion**

In `MonkeyTests.cs`, line ~111:

```csharp
// BEFORE
Assert.True(result.Length <= 512);

// AFTER
Assert.Equal(512, result.Length);
```

- [ ] **Step 2: Fix `PullRequestActionTests` — correct the cache key assertion**

The current test `RunAsyncUsesPrNumberAsPartOfCacheKey` uses action name `"opened"` in the expected key.
Check `PullRequestAction.GetCacheKeySuffix()` — `"opened"` maps to `_ => Event.Action` (i.e., `"opened"`).
So the key is `"test/repo#42-opened"`.  This is actually correct per the implementation.
Add a *new* test that checks `"review_requested"` and `"review_request_removed"` share key suffix `"review_requested"`:

```csharp
/// <summary>review_requested と review_request_removed は共通のキーサフィックスを使用する。</summary>
[Fact]
public async Task RunAsyncReviewRequestedAndRemovedShareCacheKeySuffix()
{
    (Mock<IDiscordClient>? discord1, Mock<IMessageCacheService>? cache1, Mock<IGitHubUserMapManager>? userMap1) = CreateMocks();
    (Mock<IDiscordClient>? discord2, Mock<IMessageCacheService>? cache2, Mock<IGitHubUserMapManager>? userMap2) = CreateMocks();

    User reviewer = new() { Login = "reviewer-user", Id = 300 };

    PullRequestEvent prEventRequested = MakePrEvent("review_requested");
    prEventRequested.RequestedReviewer = reviewer;

    PullRequestEvent prEventRemoved = MakePrEvent("review_request_removed");
    prEventRemoved.RequestedReviewer = reviewer;

    PullRequestAction action1 = new(
        discord1.Object, _webhookUri, "pull_request",
        prEventRequested, cache1.Object, userMap1.Object,
        Mock.Of<ILogger>());

    PullRequestAction action2 = new(
        discord2.Object, _webhookUri, "pull_request",
        prEventRemoved, cache2.Object, userMap2.Object,
        Mock.Of<ILogger>());

    await action1.RunAsync();
    await action2.RunAsync();

    // 両方とも "review_requested" サフィックスのキーを使う
    cache1.Verify(c => c.GetAsync(_webhookUri, "test/repo#42-review_requested"), Times.Once);
    cache2.Verify(c => c.GetAsync(_webhookUri, "test/repo#42-review_requested"), Times.Once);
}
```

- [ ] **Step 3: Add Draft PR `review_requested` suppression test to `PullRequestActionTests`**

```csharp
/// <summary>Draft PR で review_requested が来てもメンションを送信しない。</summary>
[Fact]
public async Task RunAsyncDoesNotSendMentionForReviewRequestedOnDraftPr()
{
    (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

    User reviewer = new() { Login = "reviewer-user", Id = 300 };
    PullRequestEvent prEvent = MakePrEvent("review_requested", draft: true);
    prEvent.RequestedReviewer = reviewer;

    userMap.Setup(u => u.GetById(300L)).Returns("discord-user-id-300");

    PullRequestAction action = new(
        discord.Object, _webhookUri, "pull_request",
        prEvent, cache.Object, userMap.Object,
        Mock.Of<ILogger>());

    await action.RunAsync();

    // Draft PR ではメンションなし（Content が null か空）
    discord.Verify(
        d => d.SendMessageAsync(
            It.IsAny<Uri>(),
            It.Is<DiscordMessage>(m => m.Content == null || m.Content == string.Empty)),
        Times.Once);
}
```

- [ ] **Step 4: Add TODO comment to known-bug test in `PullRequestReviewActionTests`**

Find `RunAsyncSubmittedCommentedUsesApprovedColorIncorrectlyKnownBug` and add:

```csharp
// TODO (B2 修正時): このテストを削除し、EmbedColors.PullRequestReviewCommented 色を
// 検証する新しいテストに差し替えること。
```

- [ ] **Step 5: Run tests to confirm green**

```bash
dotnet test tests/GitHubWebhookBridge.Tests/ -c Release
```

Expected: all existing tests pass.

- [ ] **Step 6: Commit**

```bash
git add tests/GitHubWebhookBridge.Tests/MonkeyTests.cs \
        tests/GitHubWebhookBridge.Tests/PullRequestActionTests.cs \
        tests/GitHubWebhookBridge.Tests/PullRequestReviewActionTests.cs
git commit -m "test: 既存テストの境界値アサーション修正・Draft PRメンション抑制テスト追加"
```

---

### Task 2: IssuesAction のテスト

**Files:**
- Create: `tests/GitHubWebhookBridge.Tests/IssuesActionTests.cs`

**Interfaces:**
- Consumes: `IssuesAction`, `IssuesEvent`, `Issue`, `Repository`, `User`, `Label`, `Milestone`, `EmbedColors`
- Produces: `IssuesActionTests` class

**Key behavior to cover:**
- `opened` → title contains "opened" + `#` + issue number, color = `EmbedColors.IssueOpened`
- `closed` → color = `EmbedColors.IssueClosed`
- `labeled` and `unlabeled` share cache key suffix `"label"`
- `labeled` event with `Label` set → Embed fields contain the label name
- Cache key format: `"{repo.FullName}#{issue.Number}-{keySuffix}"`
- Body truncation at 500 chars

- [ ] **Step 1: Write the test file**

```csharp
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>IssuesAction の通知内容・キャッシュキー・本文切り詰めテスト。</summary>
public class IssuesActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    private static IssuesEvent MakeEvent(
        string action,
        Label? label = null,
        User? assignee = null,
        Milestone? milestone = null,
        string issueBody = "") => new()
    {
        Action = action,
        Issue = new Issue
        {
            Number = 7,
            Title = "Fix bug",
            Body = issueBody.Length > 0 ? issueBody : null,
            State = action == "closed" ? "closed" : "open",
            HtmlUrl = new Uri("https://github.com/test/repo/issues/7"),
            User = new User { Login = "opener", Id = 1 },
        },
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "sender", Id = 2 },
        Label = label,
        Assignee = assignee,
        Milestone = milestone,
    };

    /// <summary>opened イベントはタイトルに "opened" と Issue 番号を含む。</summary>
    [Fact]
    public async Task RunAsyncOpenedTitleContainsOpenedAndNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, _webhookUri, "issues",
            MakeEvent("opened"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("opened") &&
                    m.Embeds![0].Title!.Contains("#7"))),
            Times.Once);
    }

    /// <summary>opened イベントは IssueOpened 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncOpenedUsesIssueOpenedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        IssuesAction action = new(
            discord.Object, _webhookUri, "issues",
            MakeEvent("opened"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.IssueOpened, capturedColor);
    }

    /// <summary>closed イベントは IssueClosed 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncClosedUsesIssueClosedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        IssuesAction action = new(
            discord.Object, _webhookUri, "issues",
            MakeEvent("closed"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.IssueClosed, capturedColor);
    }

    /// <summary>labeled イベントは Embed フィールドにラベル名を含む。</summary>
    [Fact]
    public async Task RunAsyncLabeledIncludesLabelField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, _webhookUri, "issues",
            MakeEvent("labeled", label: new Label { Name = "bug" }),
            cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("bug")))),
            Times.Once);
    }

    /// <summary>labeled と unlabeled は共通キーサフィックス "label" を使用する。</summary>
    [Fact]
    public async Task RunAsyncLabeledAndUnlabeledShareCacheKeySuffix()
    {
        (Mock<IDiscordClient>? discord1, Mock<IMessageCacheService>? cache1, Mock<IGitHubUserMapManager>? userMap1) = CreateMocks();
        (Mock<IDiscordClient>? discord2, Mock<IMessageCacheService>? cache2, Mock<IGitHubUserMapManager>? userMap2) = CreateMocks();

        IssuesAction action1 = new(
            discord1.Object, _webhookUri, "issues",
            MakeEvent("labeled", label: new Label { Name = "bug" }),
            cache1.Object, userMap1.Object, Mock.Of<ILogger>());

        IssuesAction action2 = new(
            discord2.Object, _webhookUri, "issues",
            MakeEvent("unlabeled", label: new Label { Name = "bug" }),
            cache2.Object, userMap2.Object, Mock.Of<ILogger>());

        await action1.RunAsync();
        await action2.RunAsync();

        cache1.Verify(c => c.GetAsync(_webhookUri, "test/repo#7-label"), Times.Once);
        cache2.Verify(c => c.GetAsync(_webhookUri, "test/repo#7-label"), Times.Once);
    }

    /// <summary>opened イベントのキャッシュキーに Issue 番号が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsIssueNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, _webhookUri, "issues",
            MakeEvent("opened"), cache.Object, userMap.Object,
            Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo#7-opened"), Times.Once);
    }

    /// <summary>500 文字超の本文は切り詰められて "..." が追加される。</summary>
    [Fact]
    public async Task RunAsyncBodyTruncatedAt500Chars()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, _webhookUri, "issues",
            MakeEvent("opened", issueBody: new string('a', 600)),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Description!.Contains("..."))),
            Times.Once);
    }

    /// <summary>milestoned イベントは Embed フィールドにマイルストーンタイトルを含む。</summary>
    [Fact]
    public async Task RunAsyncMilestonedIncludesMilestoneField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssuesAction action = new(
            discord.Object, _webhookUri, "issues",
            MakeEvent("milestoned", milestone: new Milestone { Title = "v1.0", Number = 1 }),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("v1.0")))),
            Times.Once);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests/ -c Release
```

Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/GitHubWebhookBridge.Tests/IssuesActionTests.cs
git commit -m "test: IssuesAction のテストを追加"
```

---

### Task 3: IssueCommentAction のテスト

**Files:**
- Create: `tests/GitHubWebhookBridge.Tests/IssueCommentActionTests.cs`

**Interfaces:**
- Consumes: `IssueCommentAction`, `IssueCommentEvent`, `Issue`, `Comment`, `IssuePullRequestRef`, `EmbedColors`
- Produces: `IssueCommentActionTests` class

**Key behavior to cover:**
- `created` → title verb `"commented on"`, color = `EmbedColors.IssueCommentCreated`
- Issue with `PullRequest` field not null → `issueType = "PR"` (title contains "PR")
- Issue without `PullRequest` field → `issueType = "Issue"` (title contains "Issue")
- Comment body truncated at 500 chars
- Cache key: `"{repo.FullName}-issue-comment-{comment.Id}"`
- PR issue author gets `@mention` if mapped; sender excluded from self-mention

- [ ] **Step 1: Write the test file**

```csharp
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>IssueCommentAction の通知内容・Issue/PR 判定・メンション・キャッシュキーテスト。</summary>
public class IssueCommentActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    private static IssueCommentEvent MakeEvent(
        string action = "created",
        bool isPullRequest = false,
        string commentBody = "LGTM",
        long commentId = 9001) => new()
    {
        Action = action,
        Issue = new Issue
        {
            Number = 3,
            Title = "Test issue",
            State = "open",
            HtmlUrl = new Uri("https://github.com/test/repo/issues/3"),
            User = new User { Login = "issue-author", Id = 10 },
            PullRequest = isPullRequest ? new IssuePullRequestRef { Url = new Uri("https://api.github.com/repos/test/repo/pulls/3") } : null,
        },
        Comment = new Comment
        {
            Id = commentId,
            Body = commentBody,
            HtmlUrl = new Uri("https://github.com/test/repo/issues/3#issuecomment-9001"),
            User = new User { Login = "commenter", Id = 20 },
        },
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "commenter", Id = 20 },
    };

    /// <summary>created + 通常 Issue のタイトルに "Issue" と "commented on" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCreatedOnIssueTitleContainsIssue()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssueCommentAction action = new(
            discord.Object, _webhookUri, "issue_comment",
            MakeEvent("created", isPullRequest: false),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("Issue") &&
                    m.Embeds![0].Title!.Contains("commented on"))),
            Times.Once);
    }

    /// <summary>created + PR コメントのタイトルに "PR" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCreatedOnPullRequestTitleContainsPr()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssueCommentAction action = new(
            discord.Object, _webhookUri, "issue_comment",
            MakeEvent("created", isPullRequest: true),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Title!.Contains("PR"))),
            Times.Once);
    }

    /// <summary>created イベントは IssueCommentCreated 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncCreatedUsesIssueCommentCreatedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        IssueCommentAction action = new(
            discord.Object, _webhookUri, "issue_comment",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.IssueCommentCreated, capturedColor);
    }

    /// <summary>キャッシュキーにコメント ID が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsCommentId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssueCommentAction action = new(
            discord.Object, _webhookUri, "issue_comment",
            MakeEvent(commentId: 9001), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-issue-comment-9001"), Times.Once);
    }

    /// <summary>コメント本文が 500 文字超の場合は切り詰められる。</summary>
    [Fact]
    public async Task RunAsyncBodyTruncatedAt500Chars()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        IssueCommentAction action = new(
            discord.Object, _webhookUri, "issue_comment",
            MakeEvent(commentBody: new string('x', 600)),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Description!.Contains("..."))),
            Times.Once);
    }

    /// <summary>Issue 作成者が Discord にマッピングされている場合はメンション付きで送信する。</summary>
    [Fact]
    public async Task RunAsyncMentionsIssueAuthorWhenMapped()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        // Issue 作成者 (id=10) が Discord にマッピングされている
        userMap.Setup(u => u.GetById(10L)).Returns("discord-id-of-author");

        IssueCommentAction action = new(
            discord.Object, _webhookUri, "issue_comment",
            MakeEvent("created"),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-id-of-author>"))),
            Times.Once);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests/ -c Release
```

Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/GitHubWebhookBridge.Tests/IssueCommentActionTests.cs
git commit -m "test: IssueCommentAction のテストを追加"
```

---

### Task 4: ForkAction / StarAction / PublicAction のテスト

**Files:**
- Create: `tests/GitHubWebhookBridge.Tests/ForkActionTests.cs`
- Create: `tests/GitHubWebhookBridge.Tests/StarActionTests.cs`
- Create: `tests/GitHubWebhookBridge.Tests/PublicActionTests.cs`

**Interfaces:**
- Consumes: `ForkAction`, `ForkEvent`, `StarAction`, `StarEvent`, `PublicAction`, `PublicEvent`, `EmbedColors`
- Produces: 3 test classes

**Key behaviors:**

ForkAction:
- Title contains フォーク元リポジトリ名・フォーク先リポジトリ名・sender login
- Cache key: `"{repo.FullName}-fork-{sender.Login}"`

StarAction:
- `created` → title "Starred", color = `EmbedColors.Star`
- `deleted` → title "Unstarred", color = `EmbedColors.Unstar`
- Cache key: `"{repo.FullName}-star-{sender.Login}"`

PublicAction:
- Title contains "Published" + リポジトリ名 + sender login
- Cache key: `"{repo.FullName}-public-{sender.Login}"`

- [ ] **Step 1: Write ForkActionTests.cs**

```csharp
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>ForkAction の通知内容・キャッシュキーテスト。</summary>
public class ForkActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    private static ForkEvent MakeEvent() => new()
    {
        Repository = new Repository
        {
            FullName = "original/repo",
            HtmlUrl = new Uri("https://github.com/original/repo"),
        },
        Forkee = new Repository
        {
            FullName = "forker/repo",
            HtmlUrl = new Uri("https://github.com/forker/repo"),
        },
        Sender = new User { Login = "forker", Id = 1 },
    };

    /// <summary>タイトルにフォーク元・フォーク先リポジトリ名と送信者 login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncTitleContainsSourceForkeeAndSender()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        ForkAction action = new(
            discord.Object, _webhookUri, "fork",
            MakeEvent(), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("original/repo") &&
                    m.Embeds![0].Title!.Contains("forker/repo") &&
                    m.Embeds![0].Title!.Contains("forker"))),
            Times.Once);
    }

    /// <summary>キャッシュキーに sender login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsSenderLogin()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        ForkAction action = new(
            discord.Object, _webhookUri, "fork",
            MakeEvent(), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "original/repo-fork-forker"), Times.Once);
    }
}
```

- [ ] **Step 2: Write StarActionTests.cs**

```csharp
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>StarAction の通知内容・色・キャッシュキーテスト。</summary>
public class StarActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    private static StarEvent MakeEvent(string action) => new()
    {
        Action = action,
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "stargazer", Id = 1 },
    };

    /// <summary>created → "Starred" というタイトルになる。</summary>
    [Fact]
    public async Task RunAsyncCreatedTitleContainsStarred()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        StarAction action = new(
            discord.Object, _webhookUri, "star",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Title!.Contains("Starred"))),
            Times.Once);
    }

    /// <summary>deleted → "Unstarred" というタイトルになる。</summary>
    [Fact]
    public async Task RunAsyncDeletedTitleContainsUnstarred()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        StarAction action = new(
            discord.Object, _webhookUri, "star",
            MakeEvent("deleted"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Title!.Contains("Unstarred"))),
            Times.Once);
    }

    /// <summary>created → Star 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncCreatedUsesStarColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        StarAction action = new(
            discord.Object, _webhookUri, "star",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.Star, capturedColor);
    }

    /// <summary>deleted → Unstar 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncDeletedUsesUnstarColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        StarAction action = new(
            discord.Object, _webhookUri, "star",
            MakeEvent("deleted"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.Unstar, capturedColor);
    }

    /// <summary>キャッシュキーに sender login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsSenderLogin()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        StarAction action = new(
            discord.Object, _webhookUri, "star",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-star-stargazer"), Times.Once);
    }
}
```

- [ ] **Step 3: Write PublicActionTests.cs**

```csharp
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>PublicAction の通知内容・キャッシュキーテスト。</summary>
public class PublicActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    private static PublicEvent MakeEvent() => new()
    {
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "publisher", Id = 1 },
    };

    /// <summary>タイトルに "Published"・リポジトリ名・送信者 login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncTitleContainsPublishedAndRepoAndSender()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PublicAction action = new(
            discord.Object, _webhookUri, "public",
            MakeEvent(), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("Published") &&
                    m.Embeds![0].Title!.Contains("test/repo") &&
                    m.Embeds![0].Title!.Contains("publisher"))),
            Times.Once);
    }

    /// <summary>キャッシュキーに sender login が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsSenderLogin()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PublicAction action = new(
            discord.Object, _webhookUri, "public",
            MakeEvent(), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-public-publisher"), Times.Once);
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests/ -c Release
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add tests/GitHubWebhookBridge.Tests/ForkActionTests.cs \
        tests/GitHubWebhookBridge.Tests/StarActionTests.cs \
        tests/GitHubWebhookBridge.Tests/PublicActionTests.cs
git commit -m "test: ForkAction / StarAction / PublicAction のテストを追加"
```

---

### Task 5: DiscussionAction のテスト

**Files:**
- Create: `tests/GitHubWebhookBridge.Tests/DiscussionActionTests.cs`

**Interfaces:**
- Consumes: `DiscussionAction`, `DiscussionEvent`, `Discussion`, `DiscussionCategory`, `DiscussionComment`, `Label`, `EmbedColors`
- Produces: `DiscussionActionTests` class

**Key behaviors:**
- `created` → title "Discussion created: #N title", description = discussion.Body (≤500 chars)
- `answered` → description = comment.Body (not discussion.Body)
- `category_changed` → フィールドに新カテゴリ名が含まれる
- Cache key: `"{repo.FullName}-discussion-{discussion.Number}"`

- [ ] **Step 1: Write the test file**

```csharp
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>DiscussionAction の通知内容・本文切り詰め・キャッシュキーテスト。</summary>
public class DiscussionActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    private static DiscussionEvent MakeEvent(
        string action,
        string? discussionBody = "body",
        DiscussionComment? comment = null,
        Label? label = null,
        DiscussionCategory? newCategory = null) => new()
    {
        Action = action,
        Discussion = new Discussion
        {
            Number = 5,
            Title = "Great discussion",
            Body = discussionBody,
            HtmlUrl = new Uri("https://github.com/test/repo/discussions/5"),
            User = new User { Login = "author", Id = 1 },
            State = "open",
            Category = new DiscussionCategory { Name = "General", IsAnswerable = false },
        },
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "author", Id = 1 },
        Comment = comment,
        Label = label,
        Category = newCategory,
    };

    /// <summary>created イベントのタイトルに "created" と Discussion 番号が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCreatedTitleContainsCreatedAndNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("created") &&
                    m.Embeds![0].Title!.Contains("#5"))),
            Times.Once);
    }

    /// <summary>created は DiscussionCreated 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncCreatedUsesDiscussionCreatedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.DiscussionCreated, capturedColor);
    }

    /// <summary>answered イベントは discussion.Body ではなく comment.Body を description に使用する。</summary>
    [Fact]
    public async Task RunAsyncAnsweredUsesCommentBodyNotDiscussionBody()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        var answerComment = new DiscussionComment
        {
            Id = 999,
            Body = "This is the answer",
            HtmlUrl = new Uri("https://github.com/test/repo/discussions/5#discussioncomment-999"),
            User = new User { Login = "answerer", Id = 2 },
        };

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("answered", discussionBody: "original body", comment: answerComment),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Description!.Contains("This is the answer") &&
                    !m.Embeds![0].Description!.Contains("original body"))),
            Times.Once);
    }

    /// <summary>キャッシュキーに Discussion 番号が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsDiscussionNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-discussion-5"), Times.Once);
    }

    /// <summary>category_changed イベントは新カテゴリ名をフィールドに含む。</summary>
    [Fact]
    public async Task RunAsyncCategoryChangedIncludesNewCategoryField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        DiscussionAction action = new(
            discord.Object, _webhookUri, "discussion",
            MakeEvent("category_changed", newCategory: new DiscussionCategory { Name = "Q&A" }),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("Q&A")))),
            Times.Once);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests/ -c Release
```

Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/GitHubWebhookBridge.Tests/DiscussionActionTests.cs
git commit -m "test: DiscussionAction のテストを追加"
```

---

### Task 6: PullRequestReviewCommentAction のテスト

**Files:**
- Create: `tests/GitHubWebhookBridge.Tests/PullRequestReviewCommentActionTests.cs`

**Interfaces:**
- Consumes: `PullRequestReviewCommentAction`, `PullRequestReviewCommentEvent`, `ReviewComment`, `PullRequest`, `EmbedColors`
- Produces: `PullRequestReviewCommentActionTests` class

**Key behaviors:**
- `created` → title verb `"commented on"`, color = `EmbedColors.PullRequestReviewCommentCreated`
- `diff_hunk` が設定されると Embed フィールドに diff ブロックが追加される
- `diff_hunk` が 300 文字超 → 切り詰め
- `path` が設定されると Embed フィールドにファイルパスが追加される
- PR 作成者へ `@mention`（送信者自身を除外）
- Cache key: `"{repo.FullName}-pr-review-comment-{comment.Id}"`

- [ ] **Step 1: Write the test file**

```csharp
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>PullRequestReviewCommentAction の通知内容・diff フィールド・メンション・キャッシュキーテスト。</summary>
public class PullRequestReviewCommentActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    private static PullRequestReviewCommentEvent MakeEvent(
        string action = "created",
        string? diffHunk = null,
        string? path = null,
        long commentId = 5001) => new()
    {
        Action = action,
        Comment = new ReviewComment
        {
            Id = commentId,
            Body = "Looks good!",
            HtmlUrl = new Uri("https://github.com/test/repo/pull/10#discussion_r5001"),
            User = new User { Login = "reviewer", Id = 30 },
            DiffHunk = diffHunk,
            Path = path,
        },
        PullRequest = new PullRequest
        {
            Number = 10,
            Title = "My PR",
            State = "open",
            HtmlUrl = new Uri("https://github.com/test/repo/pull/10"),
            User = new User { Login = "pr-author", Id = 20 },
            Head = new PullRequestRef { Ref = "feature", Sha = "abc" },
            Base = new PullRequestRef { Ref = "main", Sha = "def" },
        },
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "reviewer", Id = 30 },
    };

    /// <summary>created イベントのタイトルに "commented on" と PR 番号が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCreatedTitleContainsCommentedOnAndPrNumber()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Title!.Contains("commented on") &&
                    m.Embeds![0].Title!.Contains("#10"))),
            Times.Once);
    }

    /// <summary>created は PullRequestReviewCommentCreated 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncCreatedUsesPrReviewCommentCreatedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        PullRequestReviewCommentAction action = new(
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewCommentCreated, capturedColor);
    }

    /// <summary>diff_hunk が設定されると Embed フィールドに diff ブロックが追加される。</summary>
    [Fact]
    public async Task RunAsyncWithDiffHunkAddsDiffField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent(diffHunk: "@@ -1,3 +1,4 @@ line"),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("```diff")))),
            Times.Once);
    }

    /// <summary>path が設定されると Embed フィールドにファイルパスが追加される。</summary>
    [Fact]
    public async Task RunAsyncWithPathAddsFileField()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent(path: "src/main.cs"),
            cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("src/main.cs")))),
            Times.Once);
    }

    /// <summary>キャッシュキーにコメント ID が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsCommentId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewCommentAction action = new(
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent(commentId: 5001), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-pr-review-comment-5001"), Times.Once);
    }

    /// <summary>PR 作成者が Discord にマッピングされている場合はメンション付きで送信する。</summary>
    [Fact]
    public async Task RunAsyncMentionsPrAuthorWhenMapped()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        userMap.Setup(u => u.GetById(20L)).Returns("discord-pr-author-id");

        PullRequestReviewCommentAction action = new(
            discord.Object, _webhookUri, "pull_request_review_comment",
            MakeEvent("created"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-pr-author-id>"))),
            Times.Once);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests/ -c Release
```

Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/GitHubWebhookBridge.Tests/PullRequestReviewCommentActionTests.cs
git commit -m "test: PullRequestReviewCommentAction のテストを追加"
```

---

### Task 7: PullRequestReviewThreadAction のテスト

**Files:**
- Create: `tests/GitHubWebhookBridge.Tests/PullRequestReviewThreadActionTests.cs`

**Interfaces:**
- Consumes: `PullRequestReviewThreadAction`, `PullRequestReviewThreadEvent`, `ReviewThread`, `PullRequest`, `EmbedColors`
- Produces: `PullRequestReviewThreadActionTests` class

**Key behaviors:**
- `resolved` → title contains "resolved", color = `EmbedColors.PullRequestReviewThreadResolved`
- `unresolved` → title contains "unresolved", color = `EmbedColors.PullRequestReviewThreadUnresolved`
- Embed フィールドに `thread.NodeId` と Resolved ステータスが含まれる
- PR 作成者へ `@mention`
- Cache key: `"{repo.FullName}-pr-review-thread-{thread.NodeId}"`

- [ ] **Step 1: Write the test file**

```csharp
using GitHubWebhookBridge.Actions.Impl;
using GitHubWebhookBridge.Managers;
using GitHubWebhookBridge.Models.Discord;
using GitHubWebhookBridge.Models.GitHubWebhooks;
using GitHubWebhookBridge.Services;
using GitHubWebhookBridge.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitHubWebhookBridge.Tests;

/// <summary>PullRequestReviewThreadAction の通知内容・色・キャッシュキーテスト。</summary>
public class PullRequestReviewThreadActionTests
{
    private static readonly Uri _webhookUri = new("https://discord.test/webhook");

    private static (Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>) CreateMocks()
    {
        Mock<IDiscordClient> discord = new();
        Mock<IMessageCacheService> cache = new();
        Mock<IGitHubUserMapManager> userMap = new();

        cache.Setup(c => c.GetAsync(It.IsAny<Uri>(), It.IsAny<string>()))
             .ReturnsAsync((CachedMessage?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .ReturnsAsync("msg-id");
        userMap.Setup(u => u.EnsureLoadedAsync()).Returns(Task.CompletedTask);

        return (discord, cache, userMap);
    }

    private static PullRequestReviewThreadEvent MakeEvent(string action, bool resolved = true) => new()
    {
        Action = action,
        Thread = new ReviewThread
        {
            NodeId = "RT_node_abc",
            Resolved = resolved,
        },
        PullRequest = new PullRequest
        {
            Number = 12,
            Title = "Feature branch",
            State = "open",
            HtmlUrl = new Uri("https://github.com/test/repo/pull/12"),
            User = new User { Login = "pr-author", Id = 50 },
            Head = new PullRequestRef { Ref = "feature", Sha = "abc" },
            Base = new PullRequestRef { Ref = "main", Sha = "def" },
        },
        Repository = new Repository
        {
            FullName = "test/repo",
            HtmlUrl = new Uri("https://github.com/test/repo"),
        },
        Sender = new User { Login = "reviewer", Id = 60 },
    };

    /// <summary>resolved イベントのタイトルに "resolved" が含まれる。</summary>
    [Fact]
    public async Task RunAsyncResolvedTitleContainsResolved()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m => m.Embeds![0].Title!.Contains("resolved"))),
            Times.Once);
    }

    /// <summary>resolved は PullRequestReviewThreadResolved 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncResolvedUsesPrReviewThreadResolvedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewThreadResolved, capturedColor);
    }

    /// <summary>unresolved は PullRequestReviewThreadUnresolved 色を使用する。</summary>
    [Fact]
    public async Task RunAsyncUnresolvedUsesPrReviewThreadUnresolvedColor()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        int capturedColor = -1;
        discord.Setup(d => d.SendMessageAsync(It.IsAny<Uri>(), It.IsAny<DiscordMessage>()))
               .Callback<Uri, DiscordMessage>((_, msg) => capturedColor = msg.Embeds?.FirstOrDefault()?.Color ?? -1)
               .ReturnsAsync("msg-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("unresolved", resolved: false), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        Assert.Equal(EmbedColors.PullRequestReviewThreadUnresolved, capturedColor);
    }

    /// <summary>Embed フィールドにスレッドの NodeId が含まれる。</summary>
    [Fact]
    public async Task RunAsyncEmbedFieldContainsThreadNodeId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Embeds![0].Fields != null &&
                    m.Embeds![0].Fields!.Any(f => f.Value.Contains("RT_node_abc")))),
            Times.Once);
    }

    /// <summary>キャッシュキーにスレッドの NodeId が含まれる。</summary>
    [Fact]
    public async Task RunAsyncCacheKeyContainsThreadNodeId()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        PullRequestReviewThreadAction action = new(
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        cache.Verify(c => c.GetAsync(_webhookUri, "test/repo-pr-review-thread-RT_node_abc"), Times.Once);
    }

    /// <summary>PR 作成者が Discord にマッピングされている場合はメンション付きで送信する。</summary>
    [Fact]
    public async Task RunAsyncMentionsPrAuthorWhenMapped()
    {
        (Mock<IDiscordClient>? discord, Mock<IMessageCacheService>? cache, Mock<IGitHubUserMapManager>? userMap) = CreateMocks();

        userMap.Setup(u => u.GetById(50L)).Returns("discord-author-id");

        PullRequestReviewThreadAction action = new(
            discord.Object, _webhookUri, "pull_request_review_thread",
            MakeEvent("resolved"), cache.Object, userMap.Object, Mock.Of<ILogger>());

        await action.RunAsync();

        discord.Verify(
            d => d.SendMessageAsync(
                It.IsAny<Uri>(),
                It.Is<DiscordMessage>(m =>
                    m.Content != null &&
                    m.Content.Contains("<@discord-author-id>"))),
            Times.Once);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/GitHubWebhookBridge.Tests/ -c Release
```

Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/GitHubWebhookBridge.Tests/PullRequestReviewThreadActionTests.cs
git commit -m "test: PullRequestReviewThreadAction のテストを追加"
```

---

### Task 8: アクション網羅検証テスト (ActionCoverageTests)

**Files:**
- Create: `tests/GitHubWebhookBridge.Tests/ActionCoverageTests.cs`

**Interfaces:**
- Consumes: reflection over `GitHubWebhookBridge.Actions.Impl` and test assembly
- Produces: `ActionCoverageTests` class with one `[Fact]` that fails CI when coverage gap exists

**Design:** Use `Assembly.GetTypes()` to:
1. Find all concrete classes in `Actions.Impl` that extend `BaseAction<>` and are NOT stub classes (`StubActions.cs` に定義されているもの = `Actions.Stubs` 名前空間)
2. Find all test classes in the test assembly whose name matches `{ActionClassName}Tests`
3. Assert the sets are equal — if any action lacks a `*Tests` class, fail with a descriptive message listing the uncovered actions.

This makes it impossible to add a new action without adding a corresponding test class.

- [ ] **Step 1: Write ActionCoverageTests.cs**

```csharp
using System.Reflection;
using GitHubWebhookBridge.Actions;

namespace GitHubWebhookBridge.Tests;

/// <summary>
/// 実装済みアクション（Actions.Impl）すべてにテストクラスが存在することを保証する。
/// このテストが失敗した場合、テストのないアクションが存在することを意味する。
/// </summary>
public class ActionCoverageTests
{
    /// <summary>
    /// Actions.Impl 内の全具象アクションクラスに対応する *Tests クラスが
    /// テストアセンブリに存在することを検証する。
    /// </summary>
    [Fact]
    public void AllImplementedActionsHaveTestClass()
    {
        // 本体アセンブリから Actions.Impl の具象クラスを収集する（スタブを除外）
        Assembly mainAssembly = typeof(IAction).Assembly;
        Type[] implementedActions = mainAssembly.GetTypes()
            .Where(t =>
                t.Namespace == "GitHubWebhookBridge.Actions.Impl" &&
                t.IsClass &&
                !t.IsAbstract &&
                IsConcreteAction(t))
            .ToArray();

        // テストアセンブリから *Tests クラスを収集する
        Assembly testAssembly = typeof(ActionCoverageTests).Assembly;
        HashSet<string> testClassNames = testAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Tests", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToHashSet();

        // テストクラスが存在しないアクションを列挙する
        List<string> uncovered = implementedActions
            .Where(a => !testClassNames.Contains($"{a.Name}Tests"))
            .Select(a => a.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            uncovered.Count == 0,
            $"以下の実装済みアクションにテストクラスがありません。" +
            $" 'Actions/Impl/*.cs' に追加したら 'tests/.../*Tests.cs' も追加してください:{Environment.NewLine}" +
            string.Join(Environment.NewLine, uncovered.Select(n => $"  - {n} → {n}Tests.cs が必要")));
    }

    /// <summary>型が BaseAction&lt;T&gt; を継承する具象クラスかどうかを判定する。</summary>
    private static bool IsConcreteAction(Type type)
    {
        Type? current = type.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition().FullName == "GitHubWebhookBridge.Actions.BaseAction`1")
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
```

- [ ] **Step 2: Run tests to confirm the new test passes (all actions now have tests)**

```bash
dotnet test tests/GitHubWebhookBridge.Tests/ -c Release
```

Expected: all tests pass including `ActionCoverageTests.AllImplementedActionsHaveTestClass`.

- [ ] **Step 3: Verify the guard works — temporarily rename a test class and confirm the test fails**

```bash
# 手動確認手順 (実行後は元に戻すこと)
# 1. IssuesActionTests.cs の class 名を IssuesActionTestsBAK に変更
# 2. dotnet test → AllImplementedActionsHaveTestClass が FAIL し "IssuesAction" が列挙されることを確認
# 3. 元に戻す
```

- [ ] **Step 4: Commit**

```bash
git add tests/GitHubWebhookBridge.Tests/ActionCoverageTests.cs
git commit -m "test: アクション網羅検証テスト (ActionCoverageTests) を追加"
```

---

## Self-Review

### Spec coverage check

| 要件 | 対応タスク |
|---|---|
| IssuesAction テスト | Task 2 |
| IssueCommentAction テスト | Task 3 |
| ForkAction テスト | Task 4 |
| StarAction テスト | Task 4 |
| PublicAction テスト | Task 4 |
| DiscussionAction テスト | Task 5 |
| PullRequestReviewCommentAction テスト | Task 6 |
| PullRequestReviewThreadAction テスト | Task 7 |
| Draft PR メンション抑制テスト | Task 1 Step 3 |
| キャッシュキー共有サフィックステスト | Task 1 Step 2 |
| `SanitizeRowKeyLongStringTruncatesToMax512` アサーション修正 | Task 1 Step 1 |
| 既知バグ (B2) TODO コメント | Task 1 Step 4 |
| 網羅検証メカニズム | Task 8 |

### Placeholder scan

プレースホルダーなし。全ステップにコードを記述済み。

### Type consistency

- `CreateMocks()` の戻り値 `(Mock<IDiscordClient>, Mock<IMessageCacheService>, Mock<IGitHubUserMapManager>)` は全タスクで統一。
- 各アクションの Primary Constructor 引数は実装ファイルと照合済み。
- `EmbedColors.*` 定数名は `Utils/EmbedColors.cs` と照合済み（`Star`, `Unstar`, `Fork`, `Public`, `DiscussionCreated` 等は実在を確認すること — Task 4 実行前に `grep -n "Star\|Fork\|Public\|Discussion" Utils/EmbedColors.cs` で確認）。
