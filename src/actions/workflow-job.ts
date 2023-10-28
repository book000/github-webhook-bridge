import { WorkflowJobEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class WorkflowJobAction extends BaseAction<WorkflowJobEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
