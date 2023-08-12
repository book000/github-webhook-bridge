import { IssuesEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class IssuesAction extends BaseAction<IssuesEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
