import { PingEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class PingAction extends BaseAction<PingEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
