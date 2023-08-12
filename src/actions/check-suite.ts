import { CheckSuiteEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class CheckSuiteAction extends BaseAction<CheckSuiteEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
