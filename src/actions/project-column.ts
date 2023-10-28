import { ProjectColumnEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class ProjectColumnAction extends BaseAction<ProjectColumnEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
