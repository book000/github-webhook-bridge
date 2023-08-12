import { WorkflowRunEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class WorkflowRunAction extends BaseAction<WorkflowRunEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
