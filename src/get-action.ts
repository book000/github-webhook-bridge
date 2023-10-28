import {
  BranchProtectionRuleEvent,
  CheckRunEvent,
  CheckSuiteEvent,
  CodeScanningAlertEvent,
  CommitCommentEvent,
  CreateEvent,
  DeleteEvent,
  DependabotAlertEvent,
  DeployKeyEvent,
  DeploymentEvent,
  DeploymentReviewEvent,
  DeploymentStatusEvent,
  DiscussionCommentEvent,
  DiscussionEvent,
  ForkEvent,
  GithubAppAuthorizationEvent,
  GollumEvent,
  InstallationEvent,
  InstallationRepositoriesEvent,
  IssueCommentEvent,
  IssuesEvent,
  LabelEvent,
  MarketplacePurchaseEvent,
  MemberEvent,
  MembershipEvent,
  MergeGroupEvent,
  MetaEvent,
  MilestoneEvent,
  OrgBlockEvent,
  OrganizationEvent,
  PackageEvent,
  PageBuildEvent,
  PingEvent,
  ProjectCardEvent,
  ProjectColumnEvent,
  ProjectEvent,
  ProjectsV2ItemEvent,
  PublicEvent,
  PullRequestEvent,
  PullRequestReviewCommentEvent,
  PullRequestReviewEvent,
  PullRequestReviewThreadEvent,
  PushEvent,
  ReleaseEvent,
  RepositoryDispatchEvent,
  RepositoryEvent,
  RepositoryImportEvent,
  RepositoryVulnerabilityAlertEvent,
  Schema,
  SecurityAdvisoryEvent,
  SponsorshipEvent,
  StarEvent,
  StatusEvent,
  TeamAddEvent,
  TeamEvent,
  WatchEvent,
  WorkflowDispatchEvent,
  WorkflowJobEvent,
  WorkflowRunEvent,
} from '@octokit/webhooks-types'
import { BranchProtectionRuleAction } from './actions/branch-protection-rule'
import { CheckRunAction } from './actions/check-run'
import { CheckSuiteAction } from './actions/check-suite'
import { CodeScanningAlertAction } from './actions/code-scanning-alert'
import { CommitCommentAction } from './actions/commit-comment'
import { CreateAction } from './actions/create'
import { DeleteAction } from './actions/delete'
import { DependabotAlertAction } from './actions/dependabot-alert'
import { DeployKeyAction } from './actions/deploy-key'
import { DeploymentAction } from './actions/deployment'
import { DeploymentReviewAction } from './actions/deployment-review'
import { DeploymentStatusAction } from './actions/deployment-status'
import { DiscussionAction } from './actions/discussion'
import { DiscussionCommentAction } from './actions/discussion-comment'
import { ForkAction } from './actions/fork'
import { GithubAppAuthorizationAction } from './actions/github-app-authorization'
import { GollumAction } from './actions/gollum'
import { InstallationAction } from './actions/installation'
import { InstallationRepositoriesAction } from './actions/installation-repositories'
import { IssueCommentAction } from './actions/issue-comment'
import { IssuesAction } from './actions/issues'
import { LabelAction } from './actions/label'
import { MarketplacePurchaseAction } from './actions/marketplace-purchase'
import { MemberAction } from './actions/member'
import { MembershipAction } from './actions/membership'
import { MergeGroupAction } from './actions/merge-group'
import { MetaAction } from './actions/meta'
import { MilestoneAction } from './actions/milestone'
import { OrganizationAction } from './actions/organization'
import { OrgBlockAction } from './actions/org-block'
import { PackageAction } from './actions/package'
import { PageBuildAction } from './actions/page-build'
import { PingAction } from './actions/ping'
import { ProjectAction } from './actions/project'
import { ProjectCardAction } from './actions/project-card'
import { ProjectColumnAction } from './actions/project-column'
import { ProjectsV2ItemAction } from './actions/projects-v2-item'
import { PublicAction } from './actions/public'
import { PullRequestAction } from './actions/pull-request'
import { PullRequestReviewAction } from './actions/pull-request-review'
import { PullRequestReviewCommentAction } from './actions/pull-request-review-comment'
import { PullRequestReviewThreadAction } from './actions/pull-request-review-thread'
import { PushAction } from './actions/push'
import { ReleaseAction } from './actions/release'
import { RepositoryDispatchAction } from './actions/repository-dispatch'
import { RepositoryAction } from './actions/repository'
import { RepositoryImportAction } from './actions/repository-import'
import { RepositoryVulnerabilityAlertAction } from './actions/repository-vulnerability-alert'
import { SecurityAdvisoryAction } from './actions/security-advisory'
import { SponsorshipAction } from './actions/sponsorship'
import { StarAction } from './actions/star'
import { StatusAction } from './actions/status'
import { TeamAction } from './actions/team'
import { TeamAddAction } from './actions/team-add'
import { WatchAction } from './actions/watch'
import { WorkflowDispatchAction } from './actions/workflow-dispatch'
import { WorkflowJobAction } from './actions/workflow-job'
import { WorkflowRunAction } from './actions/workflow-run'
import { Discord } from '@book000/node-utils'

export function getAction(discord: Discord, eventName: string, event: Schema) {
  switch (eventName) {
    case 'branch_protection_rule': {
      return new BranchProtectionRuleAction(
        discord,
        eventName,
        event as BranchProtectionRuleEvent
      )
    }
    case 'check_run': {
      return new CheckRunAction(discord, eventName, event as CheckRunEvent)
    }
    case 'check_suite': {
      return new CheckSuiteAction(discord, eventName, event as CheckSuiteEvent)
    }
    case 'code_scanning_alert': {
      return new CodeScanningAlertAction(
        discord,
        eventName,
        event as CodeScanningAlertEvent
      )
    }
    case 'commit_comment': {
      return new CommitCommentAction(
        discord,
        eventName,
        event as CommitCommentEvent
      )
    }
    case 'create': {
      return new CreateAction(discord, eventName, event as CreateEvent)
    }
    case 'delete': {
      return new DeleteAction(discord, eventName, event as DeleteEvent)
    }
    case 'dependabot_alert': {
      return new DependabotAlertAction(
        discord,
        eventName,
        event as DependabotAlertEvent
      )
    }
    case 'deploy_key': {
      return new DeployKeyAction(discord, eventName, event as DeployKeyEvent)
    }
    case 'deployment_review': {
      return new DeploymentReviewAction(
        discord,
        eventName,
        event as DeploymentReviewEvent
      )
    }
    case 'deployment_status': {
      return new DeploymentStatusAction(
        discord,
        eventName,
        event as DeploymentStatusEvent
      )
    }
    case 'deployment': {
      return new DeploymentAction(discord, eventName, event as DeploymentEvent)
    }
    case 'discussion_comment': {
      return new DiscussionCommentAction(
        discord,
        eventName,
        event as DiscussionCommentEvent
      )
    }
    case 'discussion': {
      return new DiscussionAction(discord, eventName, event as DiscussionEvent)
    }
    case 'fork': {
      return new ForkAction(discord, eventName, event as ForkEvent)
    }
    case 'github_app_authorization': {
      return new GithubAppAuthorizationAction(
        discord,
        eventName,
        event as GithubAppAuthorizationEvent
      )
    }
    case 'gollum': {
      return new GollumAction(discord, eventName, event as GollumEvent)
    }
    case 'installation_repositories': {
      return new InstallationRepositoriesAction(
        discord,
        eventName,
        event as InstallationRepositoriesEvent
      )
    }
    case 'installation': {
      return new InstallationAction(
        discord,
        eventName,
        event as InstallationEvent
      )
    }
    case 'issue_comment': {
      return new IssueCommentAction(
        discord,
        eventName,
        event as IssueCommentEvent
      )
    }
    case 'issues': {
      return new IssuesAction(discord, eventName, event as IssuesEvent)
    }
    case 'label': {
      return new LabelAction(discord, eventName, event as LabelEvent)
    }
    case 'marketplace_purchase': {
      return new MarketplacePurchaseAction(
        discord,
        eventName,
        event as MarketplacePurchaseEvent
      )
    }
    case 'member': {
      return new MemberAction(discord, eventName, event as MemberEvent)
    }
    case 'membership': {
      return new MembershipAction(discord, eventName, event as MembershipEvent)
    }
    case 'merge_group': {
      return new MergeGroupAction(discord, eventName, event as MergeGroupEvent)
    }
    case 'meta': {
      return new MetaAction(discord, eventName, event as MetaEvent)
    }
    case 'milestone': {
      return new MilestoneAction(discord, eventName, event as MilestoneEvent)
    }
    case 'org_block': {
      return new OrgBlockAction(discord, eventName, event as OrgBlockEvent)
    }
    case 'organization': {
      return new OrganizationAction(
        discord,
        eventName,
        event as OrganizationEvent
      )
    }
    case 'package': {
      return new PackageAction(discord, eventName, event as PackageEvent)
    }
    case 'page_build': {
      return new PageBuildAction(discord, eventName, event as PageBuildEvent)
    }
    case 'ping': {
      return new PingAction(discord, eventName, event as PingEvent)
    }
    case 'project_card': {
      return new ProjectCardAction(
        discord,
        eventName,
        event as ProjectCardEvent
      )
    }
    case 'project_column': {
      return new ProjectColumnAction(
        discord,
        eventName,
        event as ProjectColumnEvent
      )
    }
    case 'project': {
      return new ProjectAction(discord, eventName, event as ProjectEvent)
    }
    case 'projects_v2_item': {
      return new ProjectsV2ItemAction(
        discord,
        eventName,
        event as ProjectsV2ItemEvent
      )
    }
    case 'public': {
      return new PublicAction(discord, eventName, event as PublicEvent)
    }
    case 'pull_request_review_comment': {
      return new PullRequestReviewCommentAction(
        discord,
        eventName,
        event as PullRequestReviewCommentEvent
      )
    }
    case 'pull_request_review_thread': {
      return new PullRequestReviewThreadAction(
        discord,
        eventName,
        event as PullRequestReviewThreadEvent
      )
    }
    case 'pull_request_review': {
      return new PullRequestReviewAction(
        discord,
        eventName,
        event as PullRequestReviewEvent
      )
    }
    case 'pull_request': {
      return new PullRequestAction(
        discord,
        eventName,
        event as PullRequestEvent
      )
    }
    case 'push': {
      return new PushAction(discord, eventName, event as PushEvent)
    }
    case 'release': {
      return new ReleaseAction(discord, eventName, event as ReleaseEvent)
    }
    case 'repository_dispatch': {
      return new RepositoryDispatchAction(
        discord,
        eventName,
        event as RepositoryDispatchEvent
      )
    }
    case 'repository_import': {
      return new RepositoryImportAction(
        discord,
        eventName,
        event as RepositoryImportEvent
      )
    }
    case 'repository_vulnerability_alert': {
      return new RepositoryVulnerabilityAlertAction(
        discord,
        eventName,
        event as RepositoryVulnerabilityAlertEvent
      )
    }
    case 'repository': {
      return new RepositoryAction(discord, eventName, event as RepositoryEvent)
    }
    case 'security_advisory': {
      return new SecurityAdvisoryAction(
        discord,
        eventName,
        event as SecurityAdvisoryEvent
      )
    }
    case 'sponsorship': {
      return new SponsorshipAction(
        discord,
        eventName,
        event as SponsorshipEvent
      )
    }
    case 'star': {
      return new StarAction(discord, eventName, event as StarEvent)
    }
    case 'status': {
      return new StatusAction(discord, eventName, event as StatusEvent)
    }
    case 'team_add': {
      return new TeamAddAction(discord, eventName, event as TeamAddEvent)
    }
    case 'team': {
      return new TeamAction(discord, eventName, event as TeamEvent)
    }
    case 'watch': {
      return new WatchAction(discord, eventName, event as WatchEvent)
    }
    case 'workflow_dispatch': {
      return new WorkflowDispatchAction(
        discord,
        eventName,
        event as WorkflowDispatchEvent
      )
    }
    case 'workflow_job': {
      return new WorkflowJobAction(
        discord,
        eventName,
        event as WorkflowJobEvent
      )
    }
    case 'workflow_run': {
      return new WorkflowRunAction(
        discord,
        eventName,
        event as WorkflowRunEvent
      )
    }
    default: {
      throw new Error(`Unsupported event: ${eventName}`)
    }
  }
}
