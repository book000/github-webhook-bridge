import { GollumEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class GollumAction extends BaseAction<GollumEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
