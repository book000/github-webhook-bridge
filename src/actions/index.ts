import {
  Discord,
  DiscordMessage,
  DiscordMessageFlag,
} from '@book000/node-utils'
import { Schema } from '@octokit/webhooks-types'

export abstract class BaseAction<T extends Schema> {
  private readonly discord: Discord
  protected readonly eventName: string
  protected readonly event: T

  private readonly messageCache: {
    [key: string]: {
      messageId: string
      timestamp: number
    }
  } = {}

  public constructor(discord: Discord, eventName: string, event: T) {
    this.discord = discord
    this.eventName = eventName
    this.event = event
  }

  public abstract run(): Promise<void>

  protected async sendMessage(
    key: string,
    message: DiscordMessage
  ): Promise<void> {
    message = {
      ...message,
      flags: DiscordMessageFlag.SuppressNotifications,
    }

    // メッセージキャッシュを整理する
    const cacheKeys = Object.keys(this.messageCache)
    for (const cacheKey of cacheKeys) {
      const { timestamp } = this.messageCache[cacheKey]
      if (Date.now() - timestamp > 5 * 60 * 1000) {
        delete this.messageCache[cacheKey]
      }
    }

    // メッセージキャッシュがあって、5分以内に同じキーでメッセージを送信していたら、編集する
    if (key in this.messageCache) {
      const { messageId: cachedMessage, timestamp } = this.messageCache[key]
      if (Date.now() - timestamp < 5 * 60 * 1000) {
        this.discord.editMessage(cachedMessage, message)
        return
      }
    }

    // それ以外の場合は新規に送信する
    const sentMessageId = await this.discord.sendMessage(message)
    this.messageCache[key] = {
      messageId: sentMessageId,
      timestamp: Date.now(),
    }
  }
}
