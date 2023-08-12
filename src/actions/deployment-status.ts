import { DeploymentStatusEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class DeploymentStatusAction extends BaseAction<DeploymentStatusEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
