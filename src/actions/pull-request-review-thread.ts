import { PullRequestReviewThreadEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class PullRequestReviewThreadAction extends BaseAction<PullRequestReviewThreadEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
