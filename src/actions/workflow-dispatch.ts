import { WorkflowDispatchEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class WorkflowDispatchAction extends BaseAction<WorkflowDispatchEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
