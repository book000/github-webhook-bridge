import { GithubAppAuthorizationEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class GithubAppAuthorizationAction extends BaseAction<GithubAppAuthorizationEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
