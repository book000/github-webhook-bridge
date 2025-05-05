import {
  DiscussionEvent,
  DiscussionCreatedEvent,
  DiscussionEditedEvent,
  DiscussionDeletedEvent,
  DiscussionPinnedEvent,
  DiscussionUnpinnedEvent,
  DiscussionLabeledEvent,
  DiscussionUnlabeledEvent,
  DiscussionTransferredEvent,
  DiscussionCategoryChangedEvent,
  DiscussionAnsweredEvent,
  DiscussionUnansweredEvent,
  DiscussionLockedEvent,
  DiscussionUnlockedEvent,
} from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '../utils'
import { EmbedColors } from '../embed-colors'
import { DiscordEmbedAuthor, DiscordEmbedField } from '@book000/node-utils'
import { createPatch } from 'diff'

export class DiscussionAction extends BaseAction<DiscussionEvent> {
  public async run(): Promise<void> {
    const action = this.event.action

    const methodMap: Record<DiscussionEvent['action'], () => Promise<void>> = {
      created: async () => {
        await this.onCreated(this.event as DiscussionCreatedEvent)
      },
      edited: async () => {
        await this.onEdited(this.event as DiscussionEditedEvent)
      },
      deleted: async () => {
        await this.onDeleted(this.event as DiscussionDeletedEvent)
      },
      pinned: async () => {
        await this.onPinned(this.event as DiscussionPinnedEvent)
      },
      unpinned: async () => {
        await this.onUnpinned(this.event as DiscussionUnpinnedEvent)
      },
      labeled: async () => {
        await this.onLabeled(this.event as DiscussionLabeledEvent)
      },
      unlabeled: async () => {
        await this.onUnlabeled(this.event as DiscussionUnlabeledEvent)
      },
      transferred: async () => {
        await this.onTransferred(this.event as DiscussionTransferredEvent)
      },
      category_changed: async () => {
        await this.onCategoryChanged(
          this.event as DiscussionCategoryChangedEvent
        )
      },
      answered: async () => {
        await this.onAnswered(this.event as DiscussionAnsweredEvent)
      },
      unanswered: async () => {
        await this.onUnanswered(this.event as DiscussionUnansweredEvent)
      },
      locked: async () => {
        await this.onLocked(this.event as DiscussionLockedEvent)
      },
      unlocked: async () => {
        await this.onUnlocked(this.event as DiscussionUnlockedEvent)
      },
    }

    await methodMap[action]()
  }

  /**
   * ディスカッションが作成されたときの処理
   */
  private async onCreated(event: DiscussionCreatedEvent): Promise<void> {
    const { discussion } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: this.getBody(),
      url: discussion.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Category',
          value: discussion.category.name,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションが編集されたときの処理
   */
  private async onEdited(event: DiscussionEditedEvent): Promise<void> {
    const { discussion, changes } = event

    const fields: DiscordEmbedField[] = []
    if (changes && 'title' in changes && changes.title) {
      const titleDiff = createPatch(
        'title',
        changes.title.from,
        discussion.title,
        'Previous Title',
        'Current Title'
      )
      fields.push({
        name: 'Title',
        value: '```diff\n' + titleDiff + '\n```',
        inline: false,
      })
    }
    if (changes && 'body' in changes && changes.body && discussion.body) {
      const bodyDiff = createPatch(
        'body',
        changes.body.from,
        discussion.body,
        'Previous Body',
        'Current Body'
      )
      fields.push({
        name: 'Body',
        value:
          '```diff\n' +
          bodyDiff.slice(0, 1000) +
          (bodyDiff.length > 1000 ? '...' : '') +
          '\n```',
        inline: false,
      })
    }

    if (fields.length === 0) {
      return
    }

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
      fields,
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションが削除されたときの処理
   */
  private async onDeleted(event: DiscussionDeletedEvent): Promise<void> {
    const { discussion } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションがピン留めされたときの処理
   */
  private async onPinned(event: DiscussionPinnedEvent): Promise<void> {
    const { discussion } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-pinned`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションのピン留めが解除されたときの処理
   */
  private async onUnpinned(event: DiscussionUnpinnedEvent): Promise<void> {
    const { discussion } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-pinned`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションにラベルが付与されたときの処理
   */
  private async onLabeled(event: DiscussionLabeledEvent): Promise<void> {
    const { discussion, label } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Label',
          value: label.name,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-label`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションのラベルが削除されたときの処理
   */
  private async onUnlabeled(event: DiscussionUnlabeledEvent): Promise<void> {
    const { discussion, label } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Removed Label',
          value: label.name,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-label`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションが転送されたときの処理
   */
  private async onTransferred(
    event: DiscussionTransferredEvent
  ): Promise<void> {
    const { discussion } = event
    // changes.new_repository にアクセスして新しいリポジトリ情報を取得
    const toRepo =
      event.changes.new_repository.full_name || this.event.repository.full_name

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'To Repository',
          value: toRepo,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションのカテゴリが変更されたときの処理
   */
  private async onCategoryChanged(
    event: DiscussionCategoryChangedEvent
  ): Promise<void> {
    const { discussion, changes } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'From Category',
          value: changes.category.from.name || '*Unknown*',
          inline: true,
        },
        {
          name: 'To Category',
          value: discussion.category.name,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションが回答済みになったときの処理
   */
  private async onAnswered(event: DiscussionAnsweredEvent): Promise<void> {
    const { discussion, answer } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Answer',
          value:
            answer.body.slice(0, 500) + (answer.body.length > 500 ? '...' : ''),
          inline: false,
        },
        {
          name: 'Answer By',
          value: answer.user.login,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションの回答が解除されたときの処理
   */
  private async onUnanswered(event: DiscussionUnansweredEvent): Promise<void> {
    const { discussion } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-answered`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションがロックされたときの処理
   */
  private async onLocked(event: DiscussionLockedEvent): Promise<void> {
    const { discussion } = event

    // lock_reasonがない場合があるため、条件付きでフィールドを作成
    const fields: DiscordEmbedField[] = []
    if ('reason' in event) {
      const lockReason = (event as { reason: string }).reason
      fields.push({
        name: 'Lock Reason',
        value: lockReason,
        inline: true,
      })
    }

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
      fields,
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-locked`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * ディスカッションのロックが解除されたときの処理
   */
  private async onUnlocked(event: DiscussionUnlockedEvent): Promise<void> {
    const { discussion } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: discussion.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}-discussion-${discussion.number}-locked`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Embedのタイトルを取得する
   */
  private getTitle(): string {
    const { action, discussion, repository } = this.event
    const formattedAction =
      action === 'category_changed' ? 'category changed' : action
    return `[${repository.full_name}] Discussion ${formattedAction}: ${discussion.title}`
  }

  /**
   * Embedの本文を取得する
   */
  private getBody(): string {
    const { action, discussion } = this.event
    switch (action) {
      case 'created': {
        return discussion.body
          ? discussion.body.slice(0, 500) +
              (discussion.body.length > 500 ? '...' : '')
          : '*No description provided*'
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
   * Actionから色を取得する
   *
   * @returns 色
   */
  private getColor(): (typeof EmbedColors)[keyof typeof EmbedColors] {
    const { action } = this.event

    const colorMap: Record<
      DiscussionEvent['action'],
      (typeof EmbedColors)[keyof typeof EmbedColors]
    > = {
      created: EmbedColors.DiscussionCreated,
      edited: EmbedColors.DiscussionEdited,
      deleted: EmbedColors.DiscussionDeleted,
      pinned: EmbedColors.DiscussionPinned,
      unpinned: EmbedColors.DiscussionUnpinned,
      labeled: EmbedColors.DiscussionLabeled,
      unlabeled: EmbedColors.DiscussionUnlabeled,
      transferred: EmbedColors.DiscussionTransferred,
      category_changed: EmbedColors.DiscussionCategoryChanged,
      answered: EmbedColors.DiscussionAnswered,
      unanswered: EmbedColors.DiscussionUnanswered,
      locked: EmbedColors.DiscussionLocked,
      unlocked: EmbedColors.DiscussionUnlocked,
    }

    return colorMap[action]
  }
}
