import { InstallationRepositoriesEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class InstallationRepositoriesAction extends BaseAction<InstallationRepositoriesEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
