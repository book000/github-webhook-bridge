import { BranchProtectionRuleEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class BranchProtectionRuleAction extends BaseAction<BranchProtectionRuleEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
