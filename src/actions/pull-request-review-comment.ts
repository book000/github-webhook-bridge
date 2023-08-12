import { PullRequestReviewCommentEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class PullRequestReviewCommentAction extends BaseAction<PullRequestReviewCommentEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
