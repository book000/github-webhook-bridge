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
import { BranchProtectionRuleAction } from './branch-protection-rule'
import { CheckRunAction } from './check-run'
import { CheckSuiteAction } from './check-suite'
import { CodeScanningAlertAction } from './code-scanning-alert'
import { CommitCommentAction } from './commit-comment'
import { CreateAction } from './create'
import { DeleteAction } from './delete'
import { DependabotAlertAction } from './dependabot-alert'
import { DeployKeyAction } from './deploy-key'
import { DeploymentAction } from './deployment'
import { DeploymentReviewAction } from './deployment-review'
import { DeploymentStatusAction } from './deployment-status'
import { DiscussionAction } from './discussion'
import { DiscussionCommentAction } from './discussion-comment'
import { ForkAction } from './fork'
import { GithubAppAuthorizationAction } from './github-app-authorization'
import { GollumAction } from './gollum'
import { InstallationAction } from './installation'
import { InstallationRepositoriesAction } from './installation-repositories'
import { IssueCommentAction } from './issue-comment'
import { IssuesAction } from './issues'
import { LabelAction } from './label'
import { MarketplacePurchaseAction } from './marketplace-purchase'
import { MemberAction } from './member'
import { MembershipAction } from './membership'
import { MergeGroupAction } from './merge-group'
import { MetaAction } from './meta'
import { MilestoneAction } from './milestone'
import { OrganizationAction } from './organization'
import { OrgBlockAction } from './org-block'
import { PackageAction } from './package'
import { PageBuildAction } from './page-build'
import { PingAction } from './ping'
import { ProjectAction } from './project'
import { ProjectCardAction } from './project-card'
import { ProjectColumnAction } from './project-column'
import { ProjectsV2ItemAction } from './projects-v2-item'
import { PublicAction } from './public'
import { PullRequestAction } from './pull-request'
import { PullRequestReviewAction } from './pull-request-review'
import { PullRequestReviewCommentAction } from './pull-request-review-comment'
import { PullRequestReviewThreadAction } from './pull-request-review-thread'
import { PushAction } from './push'
import { ReleaseAction } from './release'
import { RepositoryDispatchAction } from './repository-dispatch'
import { RepositoryAction } from './repository'
import { RepositoryImportAction } from './repository-import'
import { RepositoryVulnerabilityAlertAction } from './repository-vulnerability-alert'
import { SecurityAdvisoryAction } from './security-advisory'
import { SponsorshipAction } from './sponsorship'
import { StarAction } from './star'
import { StatusAction } from './status'
import { TeamAction } from './team'
import { TeamAddAction } from './team-add'
import { WatchAction } from './watch'
import { WorkflowDispatchAction } from './workflow-dispatch'
import { WorkflowJobAction } from './workflow-job'
import { WorkflowRunAction } from './workflow-run'

export abstract class BaseAction<T extends Schema> {
  protected readonly event: T

  public constructor(event: T) {
    this.event = event
  }

  public abstract run(): Promise<void>
}

export function getAction(eventName: string, event: Schema) {
  switch (eventName) {
    case 'branch_protection_rule': {
      return new BranchProtectionRuleAction(event as BranchProtectionRuleEvent)
    }
    case 'check_run': {
      return new CheckRunAction(event as CheckRunEvent)
    }
    case 'check_suite': {
      return new CheckSuiteAction(event as CheckSuiteEvent)
    }
    case 'code_scanning_alert': {
      return new CodeScanningAlertAction(event as CodeScanningAlertEvent)
    }
    case 'commit_comment': {
      return new CommitCommentAction(event as CommitCommentEvent)
    }
    case 'create': {
      return new CreateAction(event as CreateEvent)
    }
    case 'delete': {
      return new DeleteAction(event as DeleteEvent)
    }
    case 'dependabot_alert': {
      return new DependabotAlertAction(event as DependabotAlertEvent)
    }
    case 'deploy_key': {
      return new DeployKeyAction(event as DeployKeyEvent)
    }
    case 'deployment_review': {
      return new DeploymentReviewAction(event as DeploymentReviewEvent)
    }
    case 'deployment_status': {
      return new DeploymentStatusAction(event as DeploymentStatusEvent)
    }
    case 'deployment': {
      return new DeploymentAction(event as DeploymentEvent)
    }
    case 'discussion_comment': {
      return new DiscussionCommentAction(event as DiscussionCommentEvent)
    }
    case 'discussion': {
      return new DiscussionAction(event as DiscussionEvent)
    }
    case 'fork': {
      return new ForkAction(event as ForkEvent)
    }
    case 'github_app_authorization': {
      return new GithubAppAuthorizationAction(
        event as GithubAppAuthorizationEvent
      )
    }
    case 'gollum': {
      return new GollumAction(event as GollumEvent)
    }
    case 'installation_repositories': {
      return new InstallationRepositoriesAction(
        event as InstallationRepositoriesEvent
      )
    }
    case 'installation': {
      return new InstallationAction(event as InstallationEvent)
    }
    case 'issue_comment': {
      return new IssueCommentAction(event as IssueCommentEvent)
    }
    case 'issues': {
      return new IssuesAction(event as IssuesEvent)
    }
    case 'label': {
      return new LabelAction(event as LabelEvent)
    }
    case 'marketplace_purchase': {
      return new MarketplacePurchaseAction(event as MarketplacePurchaseEvent)
    }
    case 'member': {
      return new MemberAction(event as MemberEvent)
    }
    case 'membership': {
      return new MembershipAction(event as MembershipEvent)
    }
    case 'merge_group': {
      return new MergeGroupAction(event as MergeGroupEvent)
    }
    case 'meta': {
      return new MetaAction(event as MetaEvent)
    }
    case 'milestone': {
      return new MilestoneAction(event as MilestoneEvent)
    }
    case 'org_block': {
      return new OrgBlockAction(event as OrgBlockEvent)
    }
    case 'organization': {
      return new OrganizationAction(event as OrganizationEvent)
    }
    case 'package': {
      return new PackageAction(event as PackageEvent)
    }
    case 'page_build': {
      return new PageBuildAction(event as PageBuildEvent)
    }
    case 'ping': {
      return new PingAction(event as PingEvent)
    }
    case 'project_card': {
      return new ProjectCardAction(event as ProjectCardEvent)
    }
    case 'project_column': {
      return new ProjectColumnAction(event as ProjectColumnEvent)
    }
    case 'project': {
      return new ProjectAction(event as ProjectEvent)
    }
    case 'projects_v2_item': {
      return new ProjectsV2ItemAction(event as ProjectsV2ItemEvent)
    }
    case 'public': {
      return new PublicAction(event as PublicEvent)
    }
    case 'pull_request_review_comment': {
      return new PullRequestReviewCommentAction(
        event as PullRequestReviewCommentEvent
      )
    }
    case 'pull_request_review_thread': {
      return new PullRequestReviewThreadAction(
        event as PullRequestReviewThreadEvent
      )
    }
    case 'pull_request_review': {
      return new PullRequestReviewAction(event as PullRequestReviewEvent)
    }
    case 'pull_request': {
      return new PullRequestAction(event as PullRequestEvent)
    }
    case 'push': {
      return new PushAction(event as PushEvent)
    }
    case 'release': {
      return new ReleaseAction(event as ReleaseEvent)
    }
    case 'repository_dispatch': {
      return new RepositoryDispatchAction(event as RepositoryDispatchEvent)
    }
    case 'repository_import': {
      return new RepositoryImportAction(event as RepositoryImportEvent)
    }
    case 'repository_vulnerability_alert': {
      return new RepositoryVulnerabilityAlertAction(
        event as RepositoryVulnerabilityAlertEvent
      )
    }
    case 'repository': {
      return new RepositoryAction(event as RepositoryEvent)
    }
    case 'security_advisory': {
      return new SecurityAdvisoryAction(event as SecurityAdvisoryEvent)
    }
    case 'sponsorship': {
      return new SponsorshipAction(event as SponsorshipEvent)
    }
    case 'star': {
      return new StarAction(event as StarEvent)
    }
    case 'status': {
      return new StatusAction(event as StatusEvent)
    }
    case 'team_add': {
      return new TeamAddAction(event as TeamAddEvent)
    }
    case 'team': {
      return new TeamAction(event as TeamEvent)
    }
    case 'watch': {
      return new WatchAction(event as WatchEvent)
    }
    case 'workflow_dispatch': {
      return new WorkflowDispatchAction(event as WorkflowDispatchEvent)
    }
    case 'workflow_job': {
      return new WorkflowJobAction(event as WorkflowJobEvent)
    }
    case 'workflow_run': {
      return new WorkflowRunAction(event as WorkflowRunEvent)
    }
    default: {
      throw new Error(`Unsupported event: ${eventName}`)
    }
  }
}
