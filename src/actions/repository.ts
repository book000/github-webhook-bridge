import { RepositoryEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class RepositoryAction extends BaseAction<RepositoryEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
