import { MilestoneEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class MilestoneAction extends BaseAction<MilestoneEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
