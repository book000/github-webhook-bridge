import { OrgBlockEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class OrgBlockAction extends BaseAction<OrgBlockEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
