import { ProjectCardEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class ProjectCardAction extends BaseAction<ProjectCardEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
