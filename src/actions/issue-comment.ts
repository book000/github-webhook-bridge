import {
  IssueCommentCreatedEvent,
  IssueCommentDeletedEvent,
  IssueCommentEditedEvent,
  IssueCommentEvent,
} from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { DiscordEmbedAuthor, DiscordEmbedField } from '@book000/node-utils'
import { EmbedColors } from '../embed-colors'
import { GitHubUserMapManager } from '../manager/github-user'
import { createEmbed } from '../utils'
import { createPatch } from 'diff'

export class IssueCommentAction extends BaseAction<IssueCommentEvent> {
  public run(): Promise<void> {
    const action = this.event.action

    const methodMap: Record<IssueCommentEvent['action'], () => Promise<void>> =
      {
        created: () => this.onCreated(this.event as IssueCommentCreatedEvent),
        edited: () => this.onEdited(this.event as IssueCommentEditedEvent),
        deleted: () => this.onDeleted(this.event as IssueCommentDeletedEvent),
      }

    return methodMap[action]()
  }

  private async onCreated(event: IssueCommentCreatedEvent): Promise<void> {
    const comment = event.comment

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: this.getBody(),
      url: comment.html_url,
      author: this.getAuthor(),
    })

    const mentions = await this.getMentions()

    const key = `${this.event.repository.full_name}#${this.event.issue.number}-comment-${comment.id}`
    return this.sendMessage(key, {
      embeds: [embed],
      content: mentions,
    })
  }

  private async onEdited(event: IssueCommentEditedEvent): Promise<void> {
    const comment = event.comment
    const changes = event.changes

    const fields: DiscordEmbedField[] = []
    if ('body' in changes && changes.body) {
      const bodyDiff = createPatch(
        'body',
        changes.body.from,
        comment.body,
        'Previous Body',
        'Current Body'
      )
      fields.push({
        name: 'Body',
        value: '```diff\n' + bodyDiff + '\n```',
        inline: true,
      })
    }

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: this.getBody(),
      url: comment.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${this.event.issue.number}-comment-${comment.id}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  private async onDeleted(event: IssueCommentDeletedEvent): Promise<void> {
    const comment = event.comment

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: this.getBody(),
      url: comment.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${this.event.issue.number}-comment-${comment.id}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Embedのタイトルを取得する
   */
  private getTitle(): string {
    const { action, issue, repository } = this.event
    return `[${repository.full_name}] Issue comment ${action}: #${issue.number} ${issue.title}`
  }

  /**
   * Embedの本文を取得する
   */
  private getBody(): string {
    const { action, comment } = this.event
    switch (action) {
      case 'created':
      case 'edited': {
        return comment.body.slice(0, 500) || '*No description provided*'
      }
      default: {
        return ''
      }
    }
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
   * メンション先を取得する
   *
   * プルリク作成時、再オープン時、レビュー依頼時、レビュー待ち時、アサイン時にメンションを付ける
   *
   * @returns メンション先
   */
  private async getMentions(): Promise<Promise<string>> {
    const { action, comment } = this.event

    const mentions = comment.body.match(/@([\da-z](?:-?[\da-z]){0,38})/gi) ?? []

    if (action !== 'created') {
      return ''
    }

    const githubUserMap = new GitHubUserMapManager()
    await githubUserMap.load()

    const discordUserIds = await Promise.all(
      mentions.map(async (mention) => {
        const githubUserId = await githubUserMap.getFromUsername(mention)
        if (!githubUserId) {
          return null
        }

        return `<@${githubUserId}>`
      })
    )
    const filteredDiscordUserIds = discordUserIds.filter(
      (discordUserId): discordUserId is string => discordUserId !== null
    )

    return filteredDiscordUserIds.join(' ')
  }

  /**
   * Actionから色を取得する
   *
   * @returns 色
   */
  private getColor(): (typeof EmbedColors)[keyof typeof EmbedColors] {
    const { action } = this.event

    const colorMap: Record<
      IssueCommentEvent['action'],
      (typeof EmbedColors)[keyof typeof EmbedColors]
    > = {
      created: EmbedColors.IssueCommentCreated,
      edited: EmbedColors.IssueCommentEdited,
      deleted: EmbedColors.IssueCommentDeleted,
    }

    return colorMap[action]
  }
}
