import { SponsorshipEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class SponsorshipAction extends BaseAction<SponsorshipEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
