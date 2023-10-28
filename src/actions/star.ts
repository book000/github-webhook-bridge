import { StarEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class StarAction extends BaseAction<StarEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
