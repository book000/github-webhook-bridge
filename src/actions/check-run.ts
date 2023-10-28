import { CheckRunEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class CheckRunAction extends BaseAction<CheckRunEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
