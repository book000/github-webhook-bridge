import { RepositoryImportEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class RepositoryImportAction extends BaseAction<RepositoryImportEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
