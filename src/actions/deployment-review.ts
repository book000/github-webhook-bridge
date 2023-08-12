import { DeploymentReviewEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class DeploymentReviewAction extends BaseAction<DeploymentReviewEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
