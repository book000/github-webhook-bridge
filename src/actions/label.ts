import { LabelEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class LabelAction extends BaseAction<LabelEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
