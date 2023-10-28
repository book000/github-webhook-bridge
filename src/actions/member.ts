import { MemberEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class MemberAction extends BaseAction<MemberEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
