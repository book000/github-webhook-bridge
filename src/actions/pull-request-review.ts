import {
  PullRequestReview,
  PullRequestReviewDismissedEvent,
  PullRequestReviewEditedEvent,
  PullRequestReviewEvent,
  PullRequestReviewSubmittedEvent,
} from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed, getUsersMentions } from '../utils'
import { DiscordEmbedAuthor, DiscordEmbedField } from '@book000/node-utils'
import { EmbedColors } from '../embed-colors'
import { createPatch } from 'diff'

export class PullRequestReviewAction extends BaseAction<PullRequestReviewEvent> {
  public async run(): Promise<void> {
    const action = this.event.action

    const methodMap: Record<
      PullRequestReviewEvent['action'],
      () => Promise<void>
    > = {
      submitted: async () => {
        await this.onSubmitted(this.event as PullRequestReviewSubmittedEvent)
      },
      edited: async () => {
        await this.onEdited(this.event as PullRequestReviewEditedEvent)
      },
      dismissed: async () => {
        await this.onDismissed(this.event as PullRequestReviewDismissedEvent)
      },
    }

    await methodMap[action]()
  }

  /**
   * プルリクエストのレビューが送信されたときの処理
   *
   * @param event プルリクエストのレビューが送信されたときのイベント
   */
  private async onSubmitted(
    event: PullRequestReviewSubmittedEvent
  ): Promise<void> {
    const review = event.review
    const state = review.state
    if (state === 'commented') {
      // コメントの場合は何もしない
      return
    }

    const pullRequest = event.pull_request

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      description: this.getBody(),
      url: review.html_url,
      author: this.getAuthor(),
    })

    const mentions = await getUsersMentions([event.pull_request.user])
    // レビューで変更がリクエストされた場合はメンションする
    const mentionsForChangesRequested =
      state === 'changes_requested' ? mentions : ''

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review-${this.event.action}-${this.event.review.id}`
    return this.sendMessage(key, {
      content: mentionsForChangesRequested,
      embeds: [embed],
    })
  }

  /**
   * プルリクエストのレビューが編集されたときの処理
   *
   * @param event プルリクエストのレビューが編集されたときのイベント
   */
  private async onEdited(event: PullRequestReviewEditedEvent): Promise<void> {
    const review = event.review
    const pullRequest = event.pull_request
    const changes = event.changes

    const fields: DiscordEmbedField[] = []
    if ('body' in changes && changes.body) {
      const bodyDiff = createPatch(
        'body',
        changes.body.from,
        pullRequest.body ?? '',
        'Previous Body',
        'Current Body'
      )
      fields.push({
        name: 'Body',
        value: '```diff\n' + bodyDiff + '\n```',
        inline: true,
      })
    }

    if (fields.length === 0) return

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: review.html_url,
      author: this.getAuthor(),
      fields,
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review-${this.event.action}-${this.event.review.id}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * プルリクエストのレビューが却下されたときの処理
   *
   * @param event プルリクエストのレビューが却下されたときのイベント
   */
  private async onDismissed(
    event: PullRequestReviewDismissedEvent
  ): Promise<void> {
    const review = event.review
    const pullRequest = event.pull_request

    const embed = createEmbed(this.eventName, this.getColor(), {
      title: this.getTitle(),
      url: review.html_url,
      author: this.getAuthor(),
    })

    const key = `${this.event.repository.full_name}#${pullRequest.number}-review-${this.event.action}-${this.event.review.id}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }

  /**
   * Embedのタイトルを取得する
   */
  private getTitle(): string {
    const { action, pull_request: pullRequest, repository, review } = this.event
    const actionWithState =
      action === 'submitted' ? `${action} (${review.state})` : action
    return `[${repository.full_name}] Pull Request Review ${actionWithState}: #${pullRequest.number} ${pullRequest.title}`
  }

  /**
   * Embedの本文を取得する
   */
  private getBody(): string {
    const { review } = this.event

    const stateMap: Record<PullRequestReview['state'], string> = {
      approved: 'approved',
      changes_requested: 'requested changes',
      dismissed: 'dismissed',
      commented: 'commented',
    }

    return `The pull request was ${stateMap[review.state]} by ${review.user.login}`
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
    const { action, review } = this.event
    const state = review.state

    if (action === 'submitted') {
      const colorMap: Record<
        PullRequestReview['state'],
        (typeof EmbedColors)[keyof typeof EmbedColors]
      > = {
        approved: EmbedColors.PullRequestReviewApproved,
        changes_requested: EmbedColors.PullRequestReviewChangesRequested,
        dismissed: EmbedColors.PullRequestReviewDismissed,
        commented: EmbedColors.Unknown,
      }

      return colorMap[state]
    }

    if (action === 'edited') {
      return EmbedColors.PullRequestReviewEdited
    }

    return EmbedColors.PullRequestReviewDismissed
  }
}
