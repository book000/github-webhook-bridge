import { TeamEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class TeamAction extends BaseAction<TeamEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
