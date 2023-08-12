import { RepositoryDispatchEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class RepositoryDispatchAction extends BaseAction<RepositoryDispatchEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
