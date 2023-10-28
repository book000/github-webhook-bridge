import { IssueCommentEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class IssueCommentAction extends BaseAction<IssueCommentEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
