import { PublicEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class PublicAction extends BaseAction<PublicEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
