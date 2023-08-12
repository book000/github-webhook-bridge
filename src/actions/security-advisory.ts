import { SecurityAdvisoryEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class SecurityAdvisoryAction extends BaseAction<SecurityAdvisoryEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
