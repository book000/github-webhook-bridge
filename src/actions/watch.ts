import { WatchEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class WatchAction extends BaseAction<WatchEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
