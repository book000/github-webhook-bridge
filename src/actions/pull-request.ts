import {
  PullRequestEvent,
  User,
  Team,
  PullRequestEditedEvent,
  PullRequestOpenedEvent,
  PullRequestClosedEvent,
  PullRequestReopenedEvent,
  PullRequestAssignedEvent,
  PullRequestUnassignedEvent,
  PullRequestReviewRequestedEvent,
  PullRequestReviewRequestRemovedEvent,
  PullRequestLabeledEvent,
  PullRequestUnlabeledEvent,
  PullRequestReadyForReviewEvent,
  PullRequestLockedEvent,
  PullRequestUnlockedEvent,
  PullRequestAutoMergeEnabledEvent,
  PullRequestAutoMergeDisabledEvent,
  PullRequestConvertedToDraftEvent,
  PullRequestDemilestonedEvent,
  PullRequestMilestonedEvent,
  PullRequestEnqueuedEvent,
  PullRequestDequeuedEvent,
} from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '@/utils'
import { GitHubUserMap } from '@/mapper/github-user'
import { EmbedColors } from '@/embed-colors'
import { DiscordEmbedAuthor, DiscordEmbedField } from '@book000/node-utils'

export class PullRequestAction extends BaseAction<PullRequestEvent> {
  public run(): Promise<void> {
    const action = this.event.action

    // synchronizeは無視
    if (action === 'synchronize') {
      return Promise.resolve()
    }

    const methodMap: Record<
      Exclude<PullRequestEvent['action'], 'synchronize'>,
      () => Promise<void>
    > = {
      opened: () => this.processOpened(this.event as PullRequestOpenedEvent),
      closed: () => this.processClosed(this.event as PullRequestClosedEvent),
      reopened: () =>
        this.processOpened(this.event as PullRequestReopenedEvent),
      assigned: () =>
        this.processAssigned(this.event as PullRequestAssignedEvent),
      unassigned: () =>
        this.processUnassigned(this.event as PullRequestUnassignedEvent),
      review_requested: () =>
        this.processReviewRequested(
          this.event as PullRequestReviewRequestedEvent
        ),
      review_request_removed: () =>
        this.processReviewRequestRemoved(
          this.event as PullRequestReviewRequestRemovedEvent
        ),
      labeled: () => this.processLabeled(this.event as PullRequestLabeledEvent),
      unlabeled: () =>
        this.processUnlabeled(this.event as PullRequestUnlabeledEvent),
      edited: () => this.processEdited(this.event as PullRequestEditedEvent),
      ready_for_review: () =>
        this.processReadyForReview(
          this.event as PullRequestReadyForReviewEvent
        ),
      locked: () => this.processLocked(this.event as PullRequestLockedEvent),
      unlocked: () =>
        this.processUnlocked(this.event as PullRequestUnlockedEvent),
      auto_merge_enabled: () =>
        this.processAutoMergeEnabled(
          this.event as PullRequestAutoMergeEnabledEvent
        ),
      auto_merge_disabled: () =>
        this.processAutoMergeDisabled(
          this.event as PullRequestAutoMergeDisabledEvent
        ),
      converted_to_draft: () =>
        this.processConvertedToDraft(
          this.event as PullRequestConvertedToDraftEvent
        ),
      milestoned: () =>
        this.processMilestoned(this.event as PullRequestMilestonedEvent),
      demilestoned: () =>
        this.processDemilestoned(this.event as PullRequestDemilestonedEvent),
      enqueued: () =>
        this.processEnqueued(this.event as PullRequestEnqueuedEvent),
      dequeued: () =>
        this.processDequeued(this.event as PullRequestDequeuedEvent),
    }

    return methodMap[action]()
  }

  /**
   * プルリクエストがオープン・再オープンされたときの処理
   */
  private async processOpened(
    event: PullRequestOpenedEvent | PullRequestReopenedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const mentions = this.getMentions()

    const reviewersText = this.getUsersText(pullRequest.requested_reviewers)
    const assigneesText = this.getUsersText(pullRequest.assignees)

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Branch',
          value: pullRequest.head.ref,
          inline: true,
        },
        {
          name: 'Reviewers',
          value: reviewersText || '*No reviewers provided*',
          inline: true,
        },
        {
          name: 'Assignees',
          value: assigneesText || '*No assignees provided*',
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-${this.event.action}`
    return this.sendMessage(key, {
      content: mentions,
      embeds: [embed],
    })
  }

  /**
   * プルリクエストがクローズされたときの処理
   */
  private async processClosed(event: PullRequestClosedEvent): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Branch',
          value: pullRequest.head.ref,
          inline: true,
        },
        {
          name: 'Merged',
          value: pullRequest.merged ? 'Yes' : 'No',
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストがアサインされたときの処理
   */
  private async processAssigned(
    event: PullRequestAssignedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const mentions = this.getUsersMentions([event.assignee])

    const assigneesText = this.getUsersText(pullRequest.assignees)

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Branch',
          value: pullRequest.head.ref,
          inline: true,
        },
        {
          name: 'Assignees',
          value: assigneesText || '*No assignees provided*',
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-assigned`
    return this.sendMessage(key, {
      content: mentions,
      embeds: [embed],
    })
  }

  /**
   * プルリクエストのアサインが解除されたときの処理
   */
  private async processUnassigned(
    event: PullRequestUnassignedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const assigneesText = this.getUsersText(pullRequest.assignees)

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Branch',
          value: pullRequest.head.ref,
          inline: true,
        },
        {
          name: 'Assignees',
          value: assigneesText || '*No assignees provided*',
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-assigned`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストのレビュー依頼がされたときの処理
   */
  private async processReviewRequested(
    event: PullRequestReviewRequestedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const requested =
      'requested_reviewer' in event
        ? event.requested_reviewer
        : event.requested_team

    const mentions = this.getUsersMentions([requested])
    const reviewersText = this.getUsersText(pullRequest.requested_reviewers)

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Branch',
          value: pullRequest.head.ref,
          inline: true,
        },
        {
          name: 'Reviewers',
          value: reviewersText || '*No reviewers provided*',
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review_requested`
    return this.sendMessage(key, {
      content: mentions,
      embeds: [embed],
    })
  }

  /**
   * プルリクエストのレビュー依頼が解除されたときの処理
   */
  private async processReviewRequestRemoved(
    event: PullRequestReviewRequestRemovedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const reviewersText = this.getUsersText(pullRequest.requested_reviewers)

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Branch',
          value: pullRequest.head.ref,
          inline: true,
        },
        {
          name: 'Reviewers',
          value: reviewersText || '*No reviewers provided*',
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review_requested`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストにラベルが付与されたときの処理
   */
  private async processLabeled(event: PullRequestLabeledEvent): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const labels = pullRequest.labels

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Labels',
          value: labels.map((label) => label.name).join(', '),
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-label`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストのラベルが削除されたときの処理
   */
  private async processUnlabeled(
    event: PullRequestUnlabeledEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const labels = pullRequest.labels

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Labels',
          value: labels.map((label) => label.name).join(', '),
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-label`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストが編集されたときの処理
   */
  private async processEdited(event: PullRequestEditedEvent): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const changes = event.changes

    const fields: DiscordEmbedField[] = []
    if ('title' in changes && changes.title) {
      fields.push(
        {
          name: 'Previous Title',
          value: changes.title.from,
          inline: true,
        },
        {
          name: 'Current Title',
          value: pullRequest.title,
          inline: true,
        }
      )
    }
    if ('body' in changes && changes.body) {
      fields.push(
        {
          name: 'Previous Body',
          value: changes.body.from.slice(0, 500),
          inline: true,
        },
        {
          name: 'Current Body',
          value: pullRequest.body?.slice(0, 500) || '*No description provided*',
          inline: true,
        }
      )
    }
    if (
      'base' in changes &&
      changes.base &&
      changes.base.ref.from !== pullRequest.base.ref
    ) {
      fields.push(
        {
          name: 'Previous Base',
          value: changes.base.ref.from,
          inline: true,
        },
        {
          name: 'Current Base',
          value: pullRequest.base.ref,
          inline: true,
        }
      )
    }

    if (fields.length === 0) {
      return
    }

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields,
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストがレビュー待ちになったときの処理
   */
  private async processReadyForReview(
    event: PullRequestReadyForReviewEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const mentions = this.getUsersMentions(pullRequest.requested_reviewers)

    const reviewersText = this.getUsersText(pullRequest.requested_reviewers)

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Branch',
          value: pullRequest.head.ref,
          inline: true,
        },
        {
          name: 'Reviewers',
          value: reviewersText || '*No reviewers provided*',
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-${this.event.action}`
    return this.sendMessage(key, {
      content: mentions,
      embeds: [embed],
    })
  }

  /**
   * プルリクエストがロックされたときの処理
   */
  private async processLocked(event: PullRequestLockedEvent): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-locked`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストがロック解除されたときの処理
   */
  private async processUnlocked(
    event: PullRequestUnlockedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-locked`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * 自動マージが有効になったときの処理
   */
  private async processAutoMergeEnabled(
    event: PullRequestAutoMergeEnabledEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-auto_merge`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * 自動マージが無効になったときの処理
   */
  private async processAutoMergeDisabled(
    event: PullRequestAutoMergeDisabledEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const { reason } = event

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Reason',
          value: reason,
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-auto_merge`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストが下書きに変換されたときの処理
   */
  private async processConvertedToDraft(
    event: PullRequestConvertedToDraftEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-${this.event.action}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストがマイルストーンに追加されたときの処理
   */
  private async processMilestoned(
    event: PullRequestMilestonedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const { milestone } = event

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Milestone',
          value: milestone?.title || '*No milestone provided*',
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-milestoned`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストがマイルストーンから外されたときの処理
   */
  private async processDemilestoned(
    event: PullRequestDemilestonedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const { milestone } = event

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Milestone',
          value: milestone?.title || '*No milestone provided*',
          inline: true,
        },
      ],
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-milestoned`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストがキューに追加されたときの処理
   */
  private async processEnqueued(
    event: PullRequestEnqueuedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-enqueued`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストがキューから外されたときの処理
   */
  private async processDequeued(
    event: PullRequestDequeuedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request
    const color = this.getColor()

    const embed = createEmbed(this.eventName, color, {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-enqueued`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Embedのタイトルを取得する
   */
  private getTitle(): string {
    const { action, pull_request: pullRequest, repository } = this.event
    return `[${repository.full_name}] Pull Request ${action}: #${pullRequest.number} ${pullRequest.title}`
  }

  /**
   * Embedの本文を取得する
   */
  private getBody(): string {
    const { action, pull_request: pullRequest } = this.event
    switch (action) {
      case 'opened':
      case 'closed': {
        return pullRequest.body?.slice(0, 500) || '*No description provided*'
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
  private getMentions(): string {
    const { action, pull_request: pullRequest } = this.event

    // レビュアー処理
    // プルリク作成時、再オープン時、レビュー依頼時、レビュー待ち時にメンションを付ける
    const reviewers = pullRequest.requested_reviewers

    const isNeedReviewerMention =
      action === 'opened' ||
      action === 'reopened' ||
      action === 'review_requested' ||
      action === 'ready_for_review'
    const reviewersMentions = isNeedReviewerMention
      ? this.getUsersMentions(reviewers)
      : ''

    // アサイン処理
    // プルリク作成時、再オープン時、アサイン時にメンションを付ける
    const assignees = pullRequest.assignees

    const isNeedAssigneeMention =
      action === 'opened' || action === 'reopened' || action === 'assigned'

    const assigneesMentions = isNeedAssigneeMention
      ? this.getUsersMentions(assignees)
      : ''

    return reviewersMentions + ' ' + assigneesMentions
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
      .filter((mention) => mention !== null)
      .join(' ')
  }

  /**
   * GitHubのユーザーからDiscordのユーザーに変換し、メンション一覧を作成する
   *
   * @param userOrTeams User と Team の配列
   * @returns Discordのメンション一覧
   */
  private getUsersMentions(userOrTeams: (User | Team)[]): string {
    const githubUserMap = new GitHubUserMap()
    return userOrTeams
      .map((reviewer) => {
        if (!('login' in reviewer)) {
          return null
        }

        const discordUserId = githubUserMap.get(reviewer.id)
        if (!discordUserId) {
          return null
        }

        return `<@${discordUserId}>`
      })
      .filter((mention) => mention !== null)
      .join(' ')
  }

  /**
   * Actionから色を取得する
   *
   * @returns 色
   */
  private getColor(): (typeof EmbedColors)[keyof typeof EmbedColors] {
    const { action } = this.event
    if (action === 'synchronize') {
      return EmbedColors.Unknown
    }

    const colorMap: Record<
      // PullRequestEvent['action']には'synchronize'が含まれるが、これは除外する
      Exclude<PullRequestEvent['action'], 'synchronize'>,
      (typeof EmbedColors)[keyof typeof EmbedColors]
    > = {
      opened: EmbedColors.PullRequestOpened,
      closed: EmbedColors.PullRequestClosed,
      reopened: EmbedColors.PullRequestReopened,
      assigned: EmbedColors.PullRequestAssigned,
      auto_merge_disabled: EmbedColors.PullRequestAutoMergeDisabled,
      auto_merge_enabled: EmbedColors.PullRequestAutoMergeEnabled,
      converted_to_draft: EmbedColors.PullRequestConvertedToDraft,
      demilestoned: EmbedColors.PullRequestDemilestoned,
      dequeued: EmbedColors.PullRequestDequeued,
      edited: EmbedColors.PullRequestEdited,
      enqueued: EmbedColors.PullRequestEnqueued,
      labeled: EmbedColors.PullRequestLabeled,
      locked: EmbedColors.PullRequestLocked,
      milestoned: EmbedColors.PullRequestMilestoned,
      ready_for_review: EmbedColors.PullRequestReadyForReview,
      review_request_removed: EmbedColors.PullRequestReviewRequestRemoved,
      review_requested: EmbedColors.PullRequestReviewRequested,
      unassigned: EmbedColors.PullRequestUnassigned,
      unlabeled: EmbedColors.PullRequestUnlabeled,
      unlocked: EmbedColors.PullRequestUnlocked,
    }

    return colorMap[action]
  }
}
