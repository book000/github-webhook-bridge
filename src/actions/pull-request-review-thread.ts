import { PullRequestReviewThreadEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '../utils'
import { DiscordEmbedAuthor } from '@book000/node-utils'
import { EmbedColors } from '../embed-colors'

export class PullRequestReviewThreadAction extends BaseAction<PullRequestReviewThreadEvent> {
  public run(): Promise<void> {
    const action = this.event.action

    const methodMap: Record<
      PullRequestReviewThreadEvent['action'],
      () => Promise<void>
    > = {
      resolved: async () => {
        await this.onResolved(this.event)
      },
      unresolved: async () => {
        await this.onUnresolved(this.event)
      },
    }

    return methodMap[action]()
  }

  /**
   * プルリクエストのレビュースレッドが解決されたときの処理
   *
   * @param event プルリクエストのレビュースレッドが解決されたときのイベント
   */
  private async onResolved(event: PullRequestReviewThreadEvent): Promise<void> {
    const pullRequest = event.pull_request
    const thread = event.thread
    const firstComment = thread.comments[0]

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: firstComment.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review-thread-${this.event.action}-${firstComment.id}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストのレビュースレッドが解決されたときの処理
   *
   * @param event プルリクエストのレビュースレッドが解決されたときのイベント
   */
  private async onUnresolved(
    event: PullRequestReviewThreadEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const thread = event.thread
    const firstComment = thread.comments[0]

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: firstComment.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review-thread-${this.event.action}-${firstComment.id}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Embedのタイトルを取得する
   */
  private getTitle(): string {
    const { action, pull_request: pullRequest, repository } = this.event
    return `[${repository.full_name}] Pull Request Review thread ${action}: #${pullRequest.number} ${pullRequest.title}`
  }

  /**
   * EmbedのAuthorを取得する
   */
  private getAuthor(): DiscordEmbedAuthor {
    const { sender } = this.event
    return {
      name: sender.login,
      url: sender.html_url,
      icon_url: sender.avatar_url,
    }
  }

  /**
   * Actionから色を取得する
   *
   * @returns 色
   */
  private getColor(): (typeof EmbedColors)[keyof typeof EmbedColors] {
    const { action } = this.event

    const colorMap: Record<
      PullRequestReviewThreadEvent['action'],
      (typeof EmbedColors)[keyof typeof EmbedColors]
    > = {
      resolved: EmbedColors.PullRequestReviewThreadResolved,
      unresolved: EmbedColors.PullRequestReviewThreadUnresolved,
    }

    return colorMap[action]
  }
}
