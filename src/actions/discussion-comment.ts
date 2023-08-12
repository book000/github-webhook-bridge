import { DiscussionCommentEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class DiscussionCommentAction extends BaseAction<DiscussionCommentEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
