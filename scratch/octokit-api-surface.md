# Octokit.Webhooks API Surface (auto-filled from Task 1)

## Package version
Octokit.Webhooks 4.0.4

## Level 2 feasibility
<!-- Does PullRequestEvent have a [WebhookEvent("pull_request")] or similar class-level attribute? -->
Level2Available: false
Level2AttributeType: Octokit.Webhooks.WebhookActionTypeAttribute
Level2PropertyName: N/A

<!-- Note: PullRequestAssignedEvent has 4 custom attributes:
     NullableContextAttribute, NullableAttribute, RequiredMemberAttribute,
     WebhookActionTypeAttribute — none carry the GitHub event name string.
     The event-name→type mapping must be done manually via switch/dictionary. -->

## WebhookEventType constants
<!-- Do constants like WebhookEventType.PullRequest = "pull_request" exist? -->
WebhookEventTypeExists: true

<!-- Sample:
  BranchProtectionConfiguration = "branch_protection_configuration"
  BranchProtectionRule = "branch_protection_rule"
  CheckRun = "check_run"
  CheckSuite = "check_suite"
  CodeScanningAlert = "code_scanning_alert"
  CommitComment = "commit_comment"
  ContentReference = "content_reference"
  Create = "create"
  CustomProperty = "custom_property"
  CustomPropertyPromotedToEnterprise = "custom_property_promoted_to_enterprise"
-->

## JsonSerializerOptions
<!-- Does Octokit expose its own options? -->
OctokitOptionsType: N/A
OctokitOptionsProperty: N/A

<!-- Note: No type whose name contains "Options", "Defaults", or "Serializer"
     was found in the Octokit.Webhooks assembly. Callers must supply their own
     JsonSerializerOptions or use System.Text.Json defaults. -->

## Action property type
<!-- Is the "action" field on event types a string or enum? -->
ActionType: string
ActionEnumNamespace: N/A

<!-- Note: All sampled events (BranchProtectionConfigurationDisabledEvent, etc.)
     expose Action as System.String (IsEnum=False). -->

## HtmlUrl property type
<!-- Is HtmlUrl a System.Uri or string? -->
HtmlUrlType: string

<!-- Confirmed on PullRequest model: HtmlUrl (String) → json:"html_url" -->

## Sender.Id type
<!-- Is sender.id long or int? -->
SenderIdType: long

<!-- Confirmed: Sender (User) and User.Id is System.Int64 -->

## PullRequestEvent key properties (from PullRequestAssignedEvent)
<!-- Representative properties verified by reflection -->
Action (String) → json:"action"
Number (Int64) → json:"number"
PullRequest (PullRequest) → json:"pull_request"
Repository (Repository) → json:"repository"
Sender (User) → json:"sender"

## PullRequest nested type key properties
Body (String) → json:"body"
HtmlUrl (String) → json:"html_url"
Number (Int64) → json:"number"
State (StringEnum`1) → json:"state"
Title (String) → json:"title"
User (User) → json:"user"
Head (PullRequestHead) → json:"head"
Base (PullRequestBase) → json:"base"
Draft (Boolean) → json:"draft"
Merged (Nullable`1) → json:"merged"

## Full event type list (271 types, copy all lines from discovery output)
<!-- One per line: TypeName (Namespace) -->
BranchProtectionConfigurationDisabledEvent (Octokit.Webhooks.Events.BranchProtectionConfiguration)
BranchProtectionConfigurationEnabledEvent (Octokit.Webhooks.Events.BranchProtectionConfiguration)
BranchProtectionRuleCreatedEvent (Octokit.Webhooks.Events.BranchProtectionRule)
BranchProtectionRuleDeletedEvent (Octokit.Webhooks.Events.BranchProtectionRule)
BranchProtectionRuleEditedEvent (Octokit.Webhooks.Events.BranchProtectionRule)
CheckRunCompletedEvent (Octokit.Webhooks.Events.CheckRun)
CheckRunCreatedEvent (Octokit.Webhooks.Events.CheckRun)
CheckRunRequestedActionEvent (Octokit.Webhooks.Events.CheckRun)
CheckRunRerequestedEvent (Octokit.Webhooks.Events.CheckRun)
CheckSuiteCompletedEvent (Octokit.Webhooks.Events.CheckSuite)
CheckSuiteRequestedEvent (Octokit.Webhooks.Events.CheckSuite)
CheckSuiteRerequestedEvent (Octokit.Webhooks.Events.CheckSuite)
CodeScanningAlertAppearedInBranchEvent (Octokit.Webhooks.Events.CodeScanningAlert)
CodeScanningAlertClosedByUserEvent (Octokit.Webhooks.Events.CodeScanningAlert)
CodeScanningAlertCreatedEvent (Octokit.Webhooks.Events.CodeScanningAlert)
CodeScanningAlertFixedEvent (Octokit.Webhooks.Events.CodeScanningAlert)
CodeScanningAlertReopenedByUserEvent (Octokit.Webhooks.Events.CodeScanningAlert)
CodeScanningAlertReopenedEvent (Octokit.Webhooks.Events.CodeScanningAlert)
CodeScanningAlertUpdatedAssignmentEvent (Octokit.Webhooks.Events.CodeScanningAlert)
CommitCommentCreatedEvent (Octokit.Webhooks.Events.CommitComment)
ContentReferenceCreatedEvent (Octokit.Webhooks.Events.ContentReference)
CreateEvent (Octokit.Webhooks.Events)
CustomPropertyCreatedEvent (Octokit.Webhooks.Events.CustomProperty)
CustomPropertyDeletedEvent (Octokit.Webhooks.Events.CustomProperty)
CustomPropertyPromotedToEnterprisePromoteToEnterpriseEvent (Octokit.Webhooks.Events.CustomPropertyPromotedToEnterprise)
CustomPropertyUpdatedEvent (Octokit.Webhooks.Events.CustomProperty)
CustomPropertyValuesUpdatedEvent (Octokit.Webhooks.Events.CustomPropertyValues)
DeleteEvent (Octokit.Webhooks.Events)
DependabotAlertAssigneesChangedEvent (Octokit.Webhooks.Events.DependabotAlert)
DependabotAlertAutoDismissedEvent (Octokit.Webhooks.Events.DependabotAlert)
DependabotAlertAutoReopenedEvent (Octokit.Webhooks.Events.DependabotAlert)
DependabotAlertCreatedEvent (Octokit.Webhooks.Events.DependabotAlert)
DependabotAlertDismissedEvent (Octokit.Webhooks.Events.DependabotAlert)
DependabotAlertFixedEvent (Octokit.Webhooks.Events.DependabotAlert)
DependabotAlertReintroducedEvent (Octokit.Webhooks.Events.DependabotAlert)
DependabotAlertReopenedEvent (Octokit.Webhooks.Events.DependabotAlert)
DeployKeyCreatedEvent (Octokit.Webhooks.Events.DeployKey)
DeployKeyDeletedEvent (Octokit.Webhooks.Events.DeployKey)
DeploymentCreatedEvent (Octokit.Webhooks.Events.Deployment)
DeploymentProtectionRuleRequestedEvent (Octokit.Webhooks.Events.DeploymentProtectionRule)
DeploymentReviewApprovedEvent (Octokit.Webhooks.Events.DeploymentReview)
DeploymentReviewRejectedEvent (Octokit.Webhooks.Events.DeploymentReview)
DeploymentReviewRequestedEvent (Octokit.Webhooks.Events.DeploymentReview)
DeploymentStatusCreatedEvent (Octokit.Webhooks.Events.DeploymentStatus)
DiscussionAnsweredEvent (Octokit.Webhooks.Events.Discussion)
DiscussionCategoryChangedEvent (Octokit.Webhooks.Events.Discussion)
DiscussionClosedEvent (Octokit.Webhooks.Events.Discussion)
DiscussionCommentCreatedEvent (Octokit.Webhooks.Events.DiscussionComment)
DiscussionCommentDeletedEvent (Octokit.Webhooks.Events.DiscussionComment)
DiscussionCommentEditedEvent (Octokit.Webhooks.Events.DiscussionComment)
DiscussionCreatedEvent (Octokit.Webhooks.Events.Discussion)
DiscussionDeletedEvent (Octokit.Webhooks.Events.Discussion)
DiscussionEditedEvent (Octokit.Webhooks.Events.Discussion)
DiscussionLabeledEvent (Octokit.Webhooks.Events.Discussion)
DiscussionLockedEvent (Octokit.Webhooks.Events.Discussion)
DiscussionPinnedEvent (Octokit.Webhooks.Events.Discussion)
DiscussionReopenedEvent (Octokit.Webhooks.Events.Discussion)
DiscussionTransferredEvent (Octokit.Webhooks.Events.Discussion)
DiscussionUnansweredEvent (Octokit.Webhooks.Events.Discussion)
DiscussionUnlabeledEvent (Octokit.Webhooks.Events.Discussion)
DiscussionUnlockedEvent (Octokit.Webhooks.Events.Discussion)
DiscussionUnpinnedEvent (Octokit.Webhooks.Events.Discussion)
ForkEvent (Octokit.Webhooks.Events)
GithubAppAuthorizationRevokedEvent (Octokit.Webhooks.Events.GithubAppAuthorization)
GollumEvent (Octokit.Webhooks.Events)
InstallationCreatedEvent (Octokit.Webhooks.Events.Installation)
InstallationDeletedEvent (Octokit.Webhooks.Events.Installation)
InstallationNewPermissionsAcceptedEvent (Octokit.Webhooks.Events.Installation)
InstallationRepositoriesAddedEvent (Octokit.Webhooks.Events.InstallationRepositories)
InstallationRepositoriesRemovedEvent (Octokit.Webhooks.Events.InstallationRepositories)
InstallationSuspendEvent (Octokit.Webhooks.Events.Installation)
InstallationTargetRenamedEvent (Octokit.Webhooks.Events.InstallationTarget)
InstallationUnsuspendEvent (Octokit.Webhooks.Events.Installation)
IssueCommentCreatedEvent (Octokit.Webhooks.Events.IssueComment)
IssueCommentDeletedEvent (Octokit.Webhooks.Events.IssueComment)
IssueCommentEditedEvent (Octokit.Webhooks.Events.IssueComment)
IssueCommentPinnedEvent (Octokit.Webhooks.Events.IssueComment)
IssueCommentUnpinnedEvent (Octokit.Webhooks.Events.IssueComment)
IssueDependenciesBlockedByAddedEvent (Octokit.Webhooks.Events.IssueDependencies)
IssueDependenciesBlockedByRemovedEvent (Octokit.Webhooks.Events.IssueDependencies)
IssueDependenciesBlockingAddedEvent (Octokit.Webhooks.Events.IssueDependencies)
IssueDependenciesBlockingRemovedEvent (Octokit.Webhooks.Events.IssueDependencies)
IssuesAssignedEvent (Octokit.Webhooks.Events.Issues)
IssuesClosedEvent (Octokit.Webhooks.Events.Issues)
IssuesDeletedEvent (Octokit.Webhooks.Events.Issues)
IssuesDemilestonedEvent (Octokit.Webhooks.Events.Issues)
IssuesEditedEvent (Octokit.Webhooks.Events.Issues)
IssuesLabeledEvent (Octokit.Webhooks.Events.Issues)
IssuesLockedEvent (Octokit.Webhooks.Events.Issues)
IssuesMilestonedEvent (Octokit.Webhooks.Events.Issues)
IssuesOpenedEvent (Octokit.Webhooks.Events.Issues)
IssuesPinnedEvent (Octokit.Webhooks.Events.Issues)
IssuesReopenedEvent (Octokit.Webhooks.Events.Issues)
IssuesTransferredEvent (Octokit.Webhooks.Events.Issues)
IssuesTypedEvent (Octokit.Webhooks.Events.Issues)
IssuesUnassignedEvent (Octokit.Webhooks.Events.Issues)
IssuesUnlabeledEvent (Octokit.Webhooks.Events.Issues)
IssuesUnlockedEvent (Octokit.Webhooks.Events.Issues)
IssuesUnpinnedEvent (Octokit.Webhooks.Events.Issues)
IssuesUntypedEvent (Octokit.Webhooks.Events.Issues)
LabelCreatedEvent (Octokit.Webhooks.Events.Label)
LabelDeletedEvent (Octokit.Webhooks.Events.Label)
LabelEditedEvent (Octokit.Webhooks.Events.Label)
MarketplacePurchaseCancelledEvent (Octokit.Webhooks.Events.MarketplacePurchase)
MarketplacePurchaseChangedEvent (Octokit.Webhooks.Events.MarketplacePurchase)
MarketplacePurchasePendingChangeCancelledEvent (Octokit.Webhooks.Events.MarketplacePurchase)
MarketplacePurchasePendingChangeEvent (Octokit.Webhooks.Events.MarketplacePurchase)
MarketplacePurchasePurchasedEvent (Octokit.Webhooks.Events.MarketplacePurchase)
MemberAddedEvent (Octokit.Webhooks.Events.Member)
MemberEditedEvent (Octokit.Webhooks.Events.Member)
MemberRemovedEvent (Octokit.Webhooks.Events.Member)
MembershipAddedEvent (Octokit.Webhooks.Events.Membership)
MembershipRemovedEvent (Octokit.Webhooks.Events.Membership)
MergeGroupChecksRequestedEvent (Octokit.Webhooks.Events.MergeGroup)
MergeGroupDestroyedEvent (Octokit.Webhooks.Events.MergeGroup)
MergeQueueEntryCreatedEvent (Octokit.Webhooks.Events.MergeQueueEntry)
MergeQueueEntryDeletedEvent (Octokit.Webhooks.Events.MergeQueueEntry)
MetaDeletedEvent (Octokit.Webhooks.Events.Meta)
MilestoneClosedEvent (Octokit.Webhooks.Events.Milestone)
MilestoneCreatedEvent (Octokit.Webhooks.Events.Milestone)
MilestoneDeletedEvent (Octokit.Webhooks.Events.Milestone)
MilestoneEditedEvent (Octokit.Webhooks.Events.Milestone)
MilestoneOpenedEvent (Octokit.Webhooks.Events.Milestone)
OrganizationDeletedEvent (Octokit.Webhooks.Events.Organization)
OrganizationMemberAddedEvent (Octokit.Webhooks.Events.Organization)
OrganizationMemberInvitedEvent (Octokit.Webhooks.Events.Organization)
OrganizationMemberRemovedEvent (Octokit.Webhooks.Events.Organization)
OrganizationRenamedEvent (Octokit.Webhooks.Events.Organization)
OrgBlockBlockedEvent (Octokit.Webhooks.Events.OrgBlock)
OrgBlockUnblockedEvent (Octokit.Webhooks.Events.OrgBlock)
PackagePublishedEvent (Octokit.Webhooks.Events.Package)
PackageUpdatedEvent (Octokit.Webhooks.Events.Package)
PageBuildEvent (Octokit.Webhooks.Events)
PersonalAccessTokenRequestApprovedEvent (Octokit.Webhooks.Events.PersonalAccessTokenRequest)
PersonalAccessTokenRequestCancelledEvent (Octokit.Webhooks.Events.PersonalAccessTokenRequest)
PersonalAccessTokenRequestCreatedEvent (Octokit.Webhooks.Events.PersonalAccessTokenRequest)
PersonalAccessTokenRequestDeniedEvent (Octokit.Webhooks.Events.PersonalAccessTokenRequest)
PingEvent (Octokit.Webhooks.Events)
ProjectCardConvertedEvent (Octokit.Webhooks.Events.ProjectCard)
ProjectCardCreatedEvent (Octokit.Webhooks.Events.ProjectCard)
ProjectCardDeletedEvent (Octokit.Webhooks.Events.ProjectCard)
ProjectCardEditedEvent (Octokit.Webhooks.Events.ProjectCard)
ProjectCardMovedEvent (Octokit.Webhooks.Events.ProjectCard)
ProjectClosedEvent (Octokit.Webhooks.Events.Project)
ProjectColumnCreatedEvent (Octokit.Webhooks.Events.ProjectColumn)
ProjectColumnDeletedEvent (Octokit.Webhooks.Events.ProjectColumn)
ProjectColumnEditedEvent (Octokit.Webhooks.Events.ProjectColumn)
ProjectColumnMovedEvent (Octokit.Webhooks.Events.ProjectColumn)
ProjectCreatedEvent (Octokit.Webhooks.Events.Project)
ProjectDeletedEvent (Octokit.Webhooks.Events.Project)
ProjectEditedEvent (Octokit.Webhooks.Events.Project)
ProjectReopenedEvent (Octokit.Webhooks.Events.Project)
ProjectsV2ClosedEvent (Octokit.Webhooks.Events.ProjectsV2)
ProjectsV2CreatedEvent (Octokit.Webhooks.Events.ProjectsV2)
ProjectsV2DeletedEvent (Octokit.Webhooks.Events.ProjectsV2)
ProjectsV2EditedEvent (Octokit.Webhooks.Events.ProjectsV2)
ProjectsV2ItemArchivedEvent (Octokit.Webhooks.Events.ProjectsV2Item)
ProjectsV2ItemConvertedEvent (Octokit.Webhooks.Events.ProjectsV2Item)
ProjectsV2ItemCreatedEvent (Octokit.Webhooks.Events.ProjectsV2Item)
ProjectsV2ItemDeletedEvent (Octokit.Webhooks.Events.ProjectsV2Item)
ProjectsV2ItemEditedEvent (Octokit.Webhooks.Events.ProjectsV2Item)
ProjectsV2ItemReorderedEvent (Octokit.Webhooks.Events.ProjectsV2Item)
ProjectsV2ItemRestoredEvent (Octokit.Webhooks.Events.ProjectsV2Item)
ProjectsV2ReopenedEvent (Octokit.Webhooks.Events.ProjectsV2)
ProjectsV2StatusUpdateCreatedEvent (Octokit.Webhooks.Events.ProjectsV2StatusUpdate)
ProjectsV2StatusUpdateDeletedEvent (Octokit.Webhooks.Events.ProjectsV2StatusUpdate)
ProjectsV2StatusUpdateEditedEvent (Octokit.Webhooks.Events.ProjectsV2StatusUpdate)
PublicEvent (Octokit.Webhooks.Events)
PullRequestAssignedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestAutoMergeDisabledEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestAutoMergeEnabledEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestClosedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestConvertedToDraftEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestDemilestonedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestDequeuedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestEditedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestEnqueuedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestLabeledEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestLockedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestMilestonedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestOpenedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestReadyForReviewEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestReopenedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestReviewCommentCreatedEvent (Octokit.Webhooks.Events.PullRequestReviewComment)
PullRequestReviewCommentDeletedEvent (Octokit.Webhooks.Events.PullRequestReviewComment)
PullRequestReviewCommentEditedEvent (Octokit.Webhooks.Events.PullRequestReviewComment)
PullRequestReviewDismissedEvent (Octokit.Webhooks.Events.PullRequestReview)
PullRequestReviewEditedEvent (Octokit.Webhooks.Events.PullRequestReview)
PullRequestReviewRequestedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestReviewRequestRemovedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestReviewSubmittedEvent (Octokit.Webhooks.Events.PullRequestReview)
PullRequestReviewThreadResolvedEvent (Octokit.Webhooks.Events.PullRequestReviewThread)
PullRequestReviewThreadUnresolvedEvent (Octokit.Webhooks.Events.PullRequestReviewThread)
PullRequestSynchronizeEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestUnassignedEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestUnlabeledEvent (Octokit.Webhooks.Events.PullRequest)
PullRequestUnlockedEvent (Octokit.Webhooks.Events.PullRequest)
PushEvent (Octokit.Webhooks.Events)
RegistryPackagePublishedEvent (Octokit.Webhooks.Events.RegistryPackage)
RegistryPackageUpdatedEvent (Octokit.Webhooks.Events.RegistryPackage)
ReleaseCreatedEvent (Octokit.Webhooks.Events.Release)
ReleaseDeletedEvent (Octokit.Webhooks.Events.Release)
ReleaseEditedEvent (Octokit.Webhooks.Events.Release)
ReleasePrereleasedEvent (Octokit.Webhooks.Events.Release)
ReleasePublishedEvent (Octokit.Webhooks.Events.Release)
ReleaseReleasedEvent (Octokit.Webhooks.Events.Release)
ReleaseUnpublishedEvent (Octokit.Webhooks.Events.Release)
RepositoryAdvisoryPublishedEvent (Octokit.Webhooks.Events.RepositoryAdvisory)
RepositoryAdvisoryReportedEvent (Octokit.Webhooks.Events.RepositoryAdvisory)
RepositoryArchivedEvent (Octokit.Webhooks.Events.Repository)
RepositoryCreatedEvent (Octokit.Webhooks.Events.Repository)
RepositoryDeletedEvent (Octokit.Webhooks.Events.Repository)
RepositoryDispatchCustomEvent (Octokit.Webhooks.Events.RepositoryDispatch)
RepositoryDispatchOnDemandTestEvent (Octokit.Webhooks.Events.RepositoryDispatch)
RepositoryEditedEvent (Octokit.Webhooks.Events.Repository)
RepositoryImportEvent (Octokit.Webhooks.Events)
RepositoryPrivatizedEvent (Octokit.Webhooks.Events.Repository)
RepositoryPublicizedEvent (Octokit.Webhooks.Events.Repository)
RepositoryRenamedEvent (Octokit.Webhooks.Events.Repository)
RepositoryRulesetCreatedEvent (Octokit.Webhooks.Events.RepositoryRuleset)
RepositoryRulesetDeletedEvent (Octokit.Webhooks.Events.RepositoryRuleset)
RepositoryRulesetEditedEvent (Octokit.Webhooks.Events.RepositoryRuleset)
RepositoryTransferredEvent (Octokit.Webhooks.Events.Repository)
RepositoryUnarchivedEvent (Octokit.Webhooks.Events.Repository)
RepositoryVulnerabilityAlertCreateEvent (Octokit.Webhooks.Events.RepositoryVulnerabilityAlert)
RepositoryVulnerabilityAlertDismissEvent (Octokit.Webhooks.Events.RepositoryVulnerabilityAlert)
RepositoryVulnerabilityAlertReopenEvent (Octokit.Webhooks.Events.RepositoryVulnerabilityAlert)
RepositoryVulnerabilityAlertResolveEvent (Octokit.Webhooks.Events.RepositoryVulnerabilityAlert)
SecretScanningAlertAssignedEvent (Octokit.Webhooks.Events.SecretScanningAlert)
SecretScanningAlertCreatedEvent (Octokit.Webhooks.Events.SecretScanningAlert)
SecretScanningAlertLocationCreatedEvent (Octokit.Webhooks.Events.SecretScanningAlertLocation)
SecretScanningAlertPubliclyLeakedEvent (Octokit.Webhooks.Events.SecretScanningAlert)
SecretScanningAlertReopenedEvent (Octokit.Webhooks.Events.SecretScanningAlert)
SecretScanningAlertResolvedEvent (Octokit.Webhooks.Events.SecretScanningAlert)
SecretScanningAlertRevokedEvent (Octokit.Webhooks.Events.SecretScanningAlert)
SecretScanningAlertUnassignedEvent (Octokit.Webhooks.Events.SecretScanningAlert)
SecretScanningAlertValidatedEvent (Octokit.Webhooks.Events.SecretScanningAlert)
SecretScanningScanCompletedEvent (Octokit.Webhooks.Events.SecretScanningScan)
SecurityAdvisoryPerformedEvent (Octokit.Webhooks.Events.SecurityAdvisory)
SecurityAdvisoryPublishedEvent (Octokit.Webhooks.Events.SecurityAdvisory)
SecurityAdvisoryUpdatedEvent (Octokit.Webhooks.Events.SecurityAdvisory)
SecurityAdvisoryWithdrawnEvent (Octokit.Webhooks.Events.SecurityAdvisory)
SecurityAndAnalysisEvent (Octokit.Webhooks.Events)
SponsorshipCancelledEvent (Octokit.Webhooks.Events.Sponsorship)
SponsorshipCreatedEvent (Octokit.Webhooks.Events.Sponsorship)
SponsorshipEditedEvent (Octokit.Webhooks.Events.Sponsorship)
SponsorshipPendingCancellationEvent (Octokit.Webhooks.Events.Sponsorship)
SponsorshipPendingTierChangeEvent (Octokit.Webhooks.Events.Sponsorship)
SponsorshipTierChangedEvent (Octokit.Webhooks.Events.Sponsorship)
StarCreatedEvent (Octokit.Webhooks.Events.Star)
StarDeletedEvent (Octokit.Webhooks.Events.Star)
StatusEvent (Octokit.Webhooks.Events)
SubIssuesParentIssueAddedEvent (Octokit.Webhooks.Events.SubIssues)
SubIssuesParentIssueRemovedEvent (Octokit.Webhooks.Events.SubIssues)
SubIssuesSubIssueAddedEvent (Octokit.Webhooks.Events.SubIssues)
SubIssuesSubIssueRemovedEvent (Octokit.Webhooks.Events.SubIssues)
TeamAddedToRepositoryEvent (Octokit.Webhooks.Events.Team)
TeamAddEvent (Octokit.Webhooks.Events)
TeamCreatedEvent (Octokit.Webhooks.Events.Team)
TeamDeletedEvent (Octokit.Webhooks.Events.Team)
TeamEditedEvent (Octokit.Webhooks.Events.Team)
TeamRemovedFromRepositoryEvent (Octokit.Webhooks.Events.Team)
WatchStartedEvent (Octokit.Webhooks.Events.Watch)
WorkflowDispatchEvent (Octokit.Webhooks.Events)
WorkflowJobCompletedEvent (Octokit.Webhooks.Events.WorkflowJob)
WorkflowJobInProgressEvent (Octokit.Webhooks.Events.WorkflowJob)
WorkflowJobQueuedEvent (Octokit.Webhooks.Events.WorkflowJob)
WorkflowJobWaitingEvent (Octokit.Webhooks.Events.WorkflowJob)
WorkflowRunCompletedEvent (Octokit.Webhooks.Events.WorkflowRun)
WorkflowRunInProgressEvent (Octokit.Webhooks.Events.WorkflowRun)
WorkflowRunRequestedEvent (Octokit.Webhooks.Events.WorkflowRun)
