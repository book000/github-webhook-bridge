import { PackageEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class PackageAction extends BaseAction<PackageEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
