import { StatusEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class StatusAction extends BaseAction<StatusEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
