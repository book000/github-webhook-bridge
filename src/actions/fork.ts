import { ForkEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class ForkAction extends BaseAction<ForkEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
