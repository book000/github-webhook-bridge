import { MembershipEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class MembershipAction extends BaseAction<MembershipEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
