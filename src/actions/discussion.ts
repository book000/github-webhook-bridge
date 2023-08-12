import { DiscussionEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class DiscussionAction extends BaseAction<DiscussionEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
