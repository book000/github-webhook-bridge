import { DeployKeyEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class DeployKeyAction extends BaseAction<DeployKeyEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
