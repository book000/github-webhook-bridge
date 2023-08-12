import { PullRequestEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class PullRequestAction extends BaseAction<PullRequestEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
