import { ProjectEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class ProjectAction extends BaseAction<ProjectEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
