import {
  IssuesEvent,
  IssuesAssignedEvent,
  IssuesClosedEvent,
  IssuesDeletedEvent,
  IssuesDemilestonedEvent,
  IssuesEditedEvent,
  IssuesLabeledEvent,
  IssuesLockedEvent,
  IssuesMilestonedEvent,
  IssuesOpenedEvent,
  IssuesPinnedEvent,
  IssuesReopenedEvent,
  IssuesTransferredEvent,
  IssuesUnassignedEvent,
  IssuesUnlabeledEvent,
  IssuesUnlockedEvent,
  IssuesUnpinnedEvent,
  Team,
  User,
} from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { EmbedColors } from '../embed-colors'
import { DiscordEmbedAuthor, DiscordEmbedField } from '@book000/node-utils'
import { createEmbed, getUsersMentions } from '../utils'
import { createPatch } from 'diff'

export class IssuesAction extends BaseAction<IssuesEvent> {
  public run(): Promise<void> {
    const action = this.event.action

    const methodMap: Record<IssuesEvent['action'], () => Promise<void>> = {
      opened: () => this.onOpened(this.event as IssuesOpenedEvent),
      closed: () => this.onClosed(this.event as IssuesClosedEvent),
      reopened: () => this.onOpened(this.event as IssuesReopenedEvent),
      assigned: () => this.onAssigned(this.event as IssuesAssignedEvent),
      unassigned: () => this.onUnassigned(this.event as IssuesUnassignedEvent),
      labeled: () => this.onLabeled(this.event as IssuesLabeledEvent),
      unlabeled: () => this.onUnlabeled(this.event as IssuesUnlabeledEvent),
      edited: () => this.onEdited(this.event as IssuesEditedEvent),
      locked: () => this.onLocked(this.event as IssuesLockedEvent),
      unlocked: () => this.onUnlocked(this.event as IssuesUnlockedEvent),
      milestoned: () => this.onMilestoned(this.event as IssuesMilestonedEvent),
      demilestoned: () =>
        this.onDemilestoned(this.event as IssuesDemilestonedEvent),
      transferred: () =>
        this.onTransferred(this.event as IssuesTransferredEvent),
      pinned: () => this.onPinned(this.event as IssuesPinnedEvent),
      unpinned: () => this.onUnpinned(this.event as IssuesUnpinnedEvent),
      deleted: () => this.onDeleted(this.event as IssuesDeletedEvent),
    }

    return methodMap[action]()
  }

  /**
   * Issue がオープン・再オープンされたときの処理
   */
  private async onOpened(
    event: IssuesOpenedEvent | IssuesReopenedEvent
  ): Promise<void> {
    const issue = event.issue

    const mentions = await this.getMentions()

    const assigneesText = this.getUsersText(issue.assignees)
    const labelsText =
      issue.labels?.map((label) => label.name).join(' ') ?? '*No labels*'

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
      fields: [
        {
          name: 'Assignees',
          value: assigneesText,
          inline: true,
        },
        {
          name: 'Labels',
          value: labelsText,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${issue.number}-${this.event.action}`
    return this.sendMessage(key, {
      content: mentions,
      embeds: [embed],
    })
  }

  /**
   * Issue がクローズされたときの処理
   */
  private onClosed(event: IssuesClosedEvent): Promise<void> {
    const issue = event.issue

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${issue.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue がアサインされたときの処理
   */
  private async onAssigned(event: IssuesAssignedEvent): Promise<void> {
    const issue = event.issue

    const mentions = await getUsersMentions(event.sender, issue.assignees)
    const assigneesText = this.getUsersText(issue.assignees)

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
      fields: [
        {
          name: 'Assignees',
          value: assigneesText,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${issue.number}-assigned`
    return this.sendMessage(key, {
      content: mentions,
      embeds: [embed],
    })
  }

  /**
   * Issue のアサインが解除されたときの処理
   */
  private async onUnassigned(event: IssuesUnassignedEvent): Promise<void> {
    const issue = event.issue

    const mentions = await getUsersMentions(event.sender, issue.assignees)
    const assigneesText = this.getUsersText(issue.assignees)

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
      fields: [
        {
          name: 'Assignees',
          value: assigneesText,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${issue.number}-assigned`
    return this.sendMessage(key, {
      content: mentions,
      embeds: [embed],
    })
  }

  /**
   * Issue にラベルが追加されたときの処理
   */
  private onLabeled(event: IssuesLabeledEvent): Promise<void> {
    const issue = event.issue
    const labels = event.issue.labels
    if (!labels) return Promise.resolve()

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
      fields: [
        {
          name: 'Labels',
          value: labels.map((label) => label.name).join(', '),
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${issue.number}-label`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue からラベルが削除されたときの処理
   */
  private onUnlabeled(event: IssuesUnlabeledEvent): Promise<void> {
    const issue = event.issue
    const labels = event.issue.labels
    if (!labels) return Promise.resolve()

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
      fields: [
        {
          name: 'Labels',
          value: labels.map((label) => label.name).join(', '),
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${issue.number}-label`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue が編集されたときの処理
   */
  private onEdited(event: IssuesEditedEvent): Promise<void> {
    const issue = event.issue

    const changes = event.changes

    const fields: DiscordEmbedField[] = []
    if ('title' in changes && changes.title) {
      const titleDiff = createPatch(
        'title',
        changes.title.from,
        issue.title,
        'Previous Title',
        'Current Title'
      )
      fields.push({
        name: 'Title',
        value: '```diff\n' + titleDiff + '\n```',
        inline: true,
      })
    }
    if ('body' in changes && changes.body) {
      const bodyDiff = createPatch(
        'body',
        changes.body.from,
        issue.body ?? '',
        'Previous Body',
        'Current Body'
      )
      fields.push({
        name: 'Body',
        value: '```diff\n' + bodyDiff + '\n```',
        inline: true,
      })
    }

    if (fields.length === 0) return Promise.resolve()

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
      fields,
    })

    const key = `${this.event.repository.full_name}#${issue.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue がロックされたときの処理
   */
  private onLocked(event: IssuesLockedEvent): Promise<void> {
    const issue = event.issue

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${issue.number}-locked`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue のロックが解除されたときの処理
   */
  private onUnlocked(event: IssuesUnlockedEvent): Promise<void> {
    const issue = event.issue

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${issue.number}-locked`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue がマイルストーンに追加されたときの処理
   */
  private onMilestoned(event: IssuesMilestonedEvent): Promise<void> {
    const issue = event.issue
    const milestone = event.issue.milestone

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
      fields: [
        {
          name: 'Milestone',
          value: milestone.title,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${issue.number}-milestoneed`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue がマイルストーンから削除されたときの処理
   */
  private onDemilestoned(event: IssuesDemilestonedEvent): Promise<void> {
    const issue = event.issue

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${issue.number}-milestoneed`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue が別のリポジトリに移動されたときの処理
   */
  private onTransferred(event: IssuesTransferredEvent): Promise<void> {
    const issue = event.issue
    const repository = event.repository
    const newRepository = event.changes.new_repository

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
      fields: [
        {
          name: 'Before Repository',
          value: repository.full_name,
          inline: true,
        },
        {
          name: 'After Repository',
          value: newRepository.full_name,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${issue.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue がピン留めされたときの処理
   */
  private onPinned(event: IssuesPinnedEvent): Promise<void> {
    const issue = event.issue

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${issue.number}-pinned`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue のピン留めが解除されたときの処理
   */
  private onUnpinned(event: IssuesUnpinnedEvent): Promise<void> {
    const issue = event.issue

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${issue.number}-pinned`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Issue が削除されたときの処理
   */
  private onDeleted(event: IssuesDeletedEvent): Promise<void> {
    const issue = event.issue

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: issue.html_url,
      description: this.getBody(),
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${issue.number}-pinned`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Embedのタイトルを取得する
   */
  private getTitle(): string {
    const { action, issue, repository } = this.event
    return `[${repository.full_name}] Issue ${action}: #${issue.number} ${issue.title}`
  }

  /**
   * Embedの本文を取得する
   */
  private getBody(): string {
    const { action, issue } = this.event
    switch (action) {
      case 'opened': {
        return issue.body?.slice(0, 500) ?? '*No description provided*'
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
    const { action, issue, sender } = this.event

    // アサイン処理
    // プルリク作成時、再オープン時、アサイン時にメンションを付ける
    const assignees = issue.assignees

    const isNeedAssigneeMention =
      action === 'opened' || action === 'reopened' || action === 'assigned'

    const assigneesMentions = isNeedAssigneeMention
      ? await getUsersMentions(sender, assignees)
      : ''

    return assigneesMentions
  }

  /**
   * GitHubのユーザーからDiscordのユーザーに変換し、ユーザー一覧を作成する
   *
   * @param userOrTeams User と Team の配列
   * @returns Discordのユーザー一覧
   */
  private getUsersText(userOrTeams: (User | Team)[]): string {
    if (userOrTeams.length === 0) {
      return ''
    }

    return userOrTeams
      .map((reviewer) => {
        if ('login' in reviewer) {
          return `@${reviewer.login}`
        }
        return reviewer.name
      })
      .join(' ')
  }

  /**
   * Actionから色を取得する
   *
   * @returns 色
   */
  private getColor(): (typeof EmbedColors)[keyof typeof EmbedColors] {
    const { action } = this.event

    const colorMap: Record<
      IssuesEvent['action'],
      (typeof EmbedColors)[keyof typeof EmbedColors]
    > = {
      opened: EmbedColors.IssueOpened,
      closed: EmbedColors.IssueClosed,
      reopened: EmbedColors.IssueReopened,
      assigned: EmbedColors.IssueAssigned,
      unassigned: EmbedColors.IssueUnassigned,
      labeled: EmbedColors.IssueLabeled,
      unlabeled: EmbedColors.IssueUnlabeled,
      edited: EmbedColors.IssueEdited,
      locked: EmbedColors.IssueLocked,
      unlocked: EmbedColors.IssueUnlocked,
      milestoned: EmbedColors.IssueMilestoned,
      demilestoned: EmbedColors.IssueDemilestoned,
      transferred: EmbedColors.IssueTransferred,
      pinned: EmbedColors.IssuePinned,
      unpinned: EmbedColors.IssueUnpinned,
      deleted: EmbedColors.IssueDeleted,
    }

    return colorMap[action]
  }
}
