import { CodeScanningAlertEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class CodeScanningAlertAction extends BaseAction<CodeScanningAlertEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
