import { CommitCommentEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class CommitCommentAction extends BaseAction<CommitCommentEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
