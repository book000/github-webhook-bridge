import { PullRequestReviewCommentEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '../utils'
import { DiscordEmbedAuthor } from '@book000/node-utils'
import { EmbedColors } from '../embed-colors'

export class PullRequestReviewCommentAction extends BaseAction<PullRequestReviewCommentEvent> {
  public run(): Promise<void> {
    const action = this.event.action

    const methodMap: Record<
      PullRequestReviewCommentEvent['action'],
      () => Promise<void>
    > = {
      created: async () => {
        await this.onCreated(this.event)
      },
      edited: async () => {
        await this.onEdited(this.event)
      },
      deleted: async () => {
        await this.onDeleted(this.event)
      },
    }

    return methodMap[action]()
  }

  /**
   * プルリクエストのレビューコメントが作成されたときの処理
   *
   * @param event プルリクエストのレビューコメントが作成されたときのイベント
   */
  private async onCreated(event: PullRequestReviewCommentEvent): Promise<void> {
    const pullRequest = event.pull_request
    const comment = event.comment

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: `\`\`\`\n${comment.body}\n\`\`\``,
      url: comment.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review-${this.event.action}-${this.event.comment.id}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストのレビューコメントが編集されたときの処理
   *
   * @param event プルリクエストのレビューコメントが編集されたときのイベント
   */
  private async onEdited(event: PullRequestReviewCommentEvent): Promise<void> {
    const pullRequest = event.pull_request
    const comment = event.comment

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: `\`\`\`\n${comment.body}\n\`\`\``,
      url: comment.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review-${this.event.action}-${this.event.comment.id}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストのレビューコメントが削除されたときの処理
   *
   * @param event プルリクエストのレビューコメントが削除されたときのイベント
   */
  private async onDeleted(event: PullRequestReviewCommentEvent): Promise<void> {
    const pullRequest = event.pull_request
    const comment = event.comment

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: `\`\`\`\n${comment.body}\n\`\`\``,
      url: comment.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review-${this.event.action}-${this.event.comment.id}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Embedのタイトルを取得する
   */
  private getTitle(): string {
    const { action, pull_request: pullRequest, repository } = this.event
    return `[${repository.full_name}] Pull Request Review comment ${action}: #${pullRequest.number} ${pullRequest.title}`
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
      PullRequestReviewCommentEvent['action'],
      (typeof EmbedColors)[keyof typeof EmbedColors]
    > = {
      created: EmbedColors.PullRequestReviewCommentCreated,
      edited: EmbedColors.PullRequestReviewCommentEdited,
      deleted: EmbedColors.PullRequestReviewCommentDeleted,
    }

    return colorMap[action]
  }
}
