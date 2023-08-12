import { PushEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class PushAction extends BaseAction<PushEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
