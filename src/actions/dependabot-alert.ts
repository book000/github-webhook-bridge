import { DependabotAlertEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class DependabotAlertAction extends BaseAction<DependabotAlertEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
