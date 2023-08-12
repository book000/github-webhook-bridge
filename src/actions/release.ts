import { ReleaseEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class ReleaseAction extends BaseAction<ReleaseEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
