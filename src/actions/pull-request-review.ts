import { PullRequestReviewEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class PullRequestReviewAction extends BaseAction<PullRequestReviewEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
