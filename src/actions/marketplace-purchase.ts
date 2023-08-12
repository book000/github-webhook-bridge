import { MarketplacePurchaseEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class MarketplacePurchaseAction extends BaseAction<MarketplacePurchaseEvent> {
  public run(): Promise<void> {
    throw new Error('Method not implemented.')
  }
}
