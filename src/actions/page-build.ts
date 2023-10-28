import { PageBuildEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class PageBuildAction extends BaseAction<PageBuildEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
