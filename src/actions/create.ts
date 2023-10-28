import { CreateEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class CreateAction extends BaseAction<CreateEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
