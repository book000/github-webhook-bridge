import { DeploymentEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class DeploymentAction extends BaseAction<DeploymentEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
