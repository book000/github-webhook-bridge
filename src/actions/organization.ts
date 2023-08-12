import { OrganizationEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class OrganizationAction extends BaseAction<OrganizationEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
