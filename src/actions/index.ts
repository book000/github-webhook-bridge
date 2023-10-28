import { Discord } from '@book000/node-utils'
import { Schema } from '@octokit/webhooks-types'

export abstract class BaseAction<T extends Schema> {
  protected readonly discord: Discord
  protected readonly eventName: string
  protected readonly event: T

  public constructor(discord: Discord, eventName: string, event: T) {
    this.discord = discord
    this.eventName = eventName
    this.event = event
  }

  public abstract run(): Promise<void>
}
