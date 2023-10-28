import { DeleteEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class DeleteAction extends BaseAction<DeleteEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
