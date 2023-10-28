import { MergeGroupEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class MergeGroupAction extends BaseAction<MergeGroupEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
