import { InstallationEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class InstallationAction extends BaseAction<InstallationEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
