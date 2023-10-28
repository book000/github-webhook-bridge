import { ProjectsV2ItemEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class ProjectsV2ItemAction extends BaseAction<ProjectsV2ItemEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
