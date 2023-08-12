import { MetaEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class MetaAction extends BaseAction<MetaEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
