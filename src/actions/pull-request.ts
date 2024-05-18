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
import { createEmbed, getUsersMentions } from '../utils'
import { EmbedColors } from '../embed-colors'
import { DiscordEmbedAuthor, DiscordEmbedField } from '@book000/node-utils'
import jsdiff from 'diff'

export class PullRequestAction extends BaseAction<PullRequestEvent> {
  public async run(): Promise<void> {
    const action = this.event.action

    // synchronizeは無視
    if (action === 'synchronize') {
      return
    }

    const methodMap: Record<
      Exclude<PullRequestEvent['action'], 'synchronize'>,
      () => Promise<void>
    > = {
      opened: async () => {
        await this.onOpened(this.event as PullRequestOpenedEvent)
      },
      closed: async () => {
        await this.onClosed(this.event as PullRequestClosedEvent)
      },
      reopened: async () => {
        await this.onOpened(this.event as PullRequestReopenedEvent)
      },
      assigned: async () => {
        await this.onAssigned(this.event as PullRequestAssignedEvent)
      },
      unassigned: async () => {
        await this.onUnassigned(this.event as PullRequestUnassignedEvent)
      },
      review_requested: async () => {
        await this.onReviewRequested(
          this.event as PullRequestReviewRequestedEvent
        )
      },
      review_request_removed: async () => {
        await this.onReviewRequestRemoved(
          this.event as PullRequestReviewRequestRemovedEvent
        )
      },
      labeled: async () => {
        await this.onLabeled(this.event as PullRequestLabeledEvent)
      },
      unlabeled: async () => {
        await this.onUnlabeled(this.event as PullRequestUnlabeledEvent)
      },
      edited: async () => {
        await this.onEdited(this.event as PullRequestEditedEvent)
      },
      ready_for_review: async () => {
        await this.onReadyForReview(
          this.event as PullRequestReadyForReviewEvent
        )
      },
      locked: async () => {
        await this.onLocked(this.event as PullRequestLockedEvent)
      },
      unlocked: async () => {
        await this.onUnlocked(this.event as PullRequestUnlockedEvent)
      },
      auto_merge_enabled: async () => {
        await this.onAutoMergeEnabled(
          this.event as PullRequestAutoMergeEnabledEvent
        )
      },
      auto_merge_disabled: async () => {
        await this.onAutoMergeDisabled(
          this.event as PullRequestAutoMergeDisabledEvent
        )
      },
      converted_to_draft: async () => {
        await this.onConvertedToDraft(
          this.event as PullRequestConvertedToDraftEvent
        )
      },
      milestoned: async () => {
        await this.onMilestoned(this.event as PullRequestMilestonedEvent)
      },
      demilestoned: async () => {
        await this.onDemilestoned(this.event as PullRequestDemilestonedEvent)
      },
      enqueued: async () => {
        await this.onEnqueued(this.event as PullRequestEnqueuedEvent)
      },
      dequeued: async () => {
        await this.onDequeued(this.event as PullRequestDequeuedEvent)
      },
    }

    await methodMap[action]()
  }

  /**
   * プルリクエストがオープン・再オープンされたときの処理
   */
  private async onOpened(
    event: PullRequestOpenedEvent | PullRequestReopenedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request

    const mentions = await this.getMentions()

    const reviewersText = this.getUsersText(pullRequest.requested_reviewers)
    const assigneesText = this.getUsersText(pullRequest.assignees)

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onClosed(event: PullRequestClosedEvent): Promise<void> {
    const pullRequest = event.pull_request

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onAssigned(event: PullRequestAssignedEvent): Promise<void> {
    const pullRequest = event.pull_request

    const mentions = await getUsersMentions([event.assignee])
    const assigneesText = this.getUsersText(pullRequest.assignees)

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onUnassigned(event: PullRequestUnassignedEvent): Promise<void> {
    const pullRequest = event.pull_request

    const assigneesText = this.getUsersText(pullRequest.assignees)

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onReviewRequested(
    event: PullRequestReviewRequestedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request

    const requested =
      'requested_reviewer' in event
        ? event.requested_reviewer
        : event.requested_team

    const mentions = await getUsersMentions([requested])
    const reviewersText = this.getUsersText(pullRequest.requested_reviewers)

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onReviewRequestRemoved(
    event: PullRequestReviewRequestRemovedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request

    const reviewersText = this.getUsersText(pullRequest.requested_reviewers)

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onLabeled(event: PullRequestLabeledEvent): Promise<void> {
    const pullRequest = event.pull_request

    const labels = pullRequest.labels

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onUnlabeled(event: PullRequestUnlabeledEvent): Promise<void> {
    const pullRequest = event.pull_request

    const labels = pullRequest.labels

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onEdited(event: PullRequestEditedEvent): Promise<void> {
    const pullRequest = event.pull_request

    const changes = event.changes

    const fields: DiscordEmbedField[] = []
    if ('title' in changes && changes.title) {
      const titleDiff = jsdiff.createPatch(
        'title',
        changes.title.from,
        pullRequest.title,
        'Previous Title',
        'Current Title'
      )
      fields.push({
        name: 'Title',
        value: '```diff\n' + titleDiff + '\n```',
        inline: true,
      })
    }
    if ('body' in changes && changes.body && pullRequest.body) {
      const bodyDiff = jsdiff.createPatch(
        'body',
        changes.body.from,
        pullRequest.body,
        'Previous Body',
        'Current Body'
      )
      fields.push({
        name: 'Body',
        value: '```diff\n' + bodyDiff + '\n```',
        inline: true,
      })
    }
    if (
      'base' in changes &&
      changes.base &&
      changes.base.ref.from !== pullRequest.base.ref
    ) {
      const baseDiff = jsdiff.createPatch(
        'base',
        changes.base.ref.from,
        pullRequest.base.ref,
        'Previous Base',
        'Current Base'
      )
      fields.push({
        name: 'Base',
        value: '```diff\n' + baseDiff + '\n```',
        inline: true,
      })
    }

    if (fields.length === 0) {
      return
    }

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onReadyForReview(
    event: PullRequestReadyForReviewEvent
  ): Promise<void> {
    const pullRequest = event.pull_request

    const mentions = await getUsersMentions(
      pullRequest.requested_reviewers
    )

    const reviewersText = this.getUsersText(pullRequest.requested_reviewers)

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onLocked(event: PullRequestLockedEvent): Promise<void> {
    const pullRequest = event.pull_request

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onUnlocked(event: PullRequestUnlockedEvent): Promise<void> {
    const pullRequest = event.pull_request

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onAutoMergeEnabled(
    event: PullRequestAutoMergeEnabledEvent
  ): Promise<void> {
    const pullRequest = event.pull_request

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onAutoMergeDisabled(
    event: PullRequestAutoMergeDisabledEvent
  ): Promise<void> {
    const pullRequest = event.pull_request

    const { reason } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onConvertedToDraft(
    event: PullRequestConvertedToDraftEvent
  ): Promise<void> {
    const pullRequest = event.pull_request

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onMilestoned(event: PullRequestMilestonedEvent): Promise<void> {
    const pullRequest = event.pull_request

    const { milestone } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Milestone',
          value: milestone.title || '*No milestone provided*',
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
  private async onDemilestoned(
    event: PullRequestDemilestonedEvent
  ): Promise<void> {
    const pullRequest = event.pull_request

    const { milestone } = event

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: this.getBody(),
      url: pullRequest.html_url,
      author: this.getAuthor(),
      fields: [
        {
          name: 'Milestone',
          value: milestone.title || '*No milestone provided*',
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
  private async onEnqueued(event: PullRequestEnqueuedEvent): Promise<void> {
    const pullRequest = event.pull_request

    const embed = createEmbed(this.eventName, this.getColor(), {
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
  private async onDequeued(event: PullRequestDequeuedEvent): Promise<void> {
    const pullRequest = event.pull_request

    const embed = createEmbed(this.eventName, this.getColor(), {
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
      case 'opened': {
        return pullRequest.body?.slice(0, 500) ?? '*No description provided*'
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
      ? await getUsersMentions(reviewers)
      : ''

    // アサイン処理
    // プルリク作成時、再オープン時、アサイン時にメンションを付ける
    const assignees = pullRequest.assignees

    const isNeedAssigneeMention =
      action === 'opened' || action === 'reopened' || action === 'assigned'

    const assigneesMentions = isNeedAssigneeMention
      ? await getUsersMentions(assignees)
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
