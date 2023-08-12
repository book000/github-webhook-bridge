import { TeamAddEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class TeamAddAction extends BaseAction<TeamAddEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
