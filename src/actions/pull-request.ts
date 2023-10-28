import { PullRequestEvent, User, Team } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '@/utils'
import { GitHubUserMap } from '@/mapper/github-user'
import { EmbedColors } from '@/embed-colors'
import { DiscordEmbedField } from '@book000/node-utils'

export class PullRequestAction extends BaseAction<PullRequestEvent> {
  public run(): Promise<void> {
    const { action, pull_request: pullRequest, repository, sender } = this.event

    const reviewers = pullRequest.requested_reviewers

    const isOpenedOrReopened = action === 'opened' || action === 'reopened'
    const reviewersText = isOpenedOrReopened
      ? this.getReviewersText(reviewers)
      : ''
    const reviewersMentions = isOpenedOrReopened
      ? this.getReviewerMentions(reviewers)
      : ''
    const color = this.getColor(action)

    const fields: DiscordEmbedField[] = [
      {
        name: 'Branch',
        value: pullRequest.head.ref,
        inline: true,
      },
      {
        name: 'Reviewers',
        value: reviewersText,
        inline: true,
      },
    ]
    if (action === 'closed') {
      fields.push({
        name: 'Merged',
        value: pullRequest.merged ? 'Yes' : 'No',
        inline: true,
      })
    }

    const embed = createEmbed(this.eventName, color, {
      title: `[${repository.full_name}] Pull Request ${action}: #${pullRequest.number} ${pullRequest.title}`,
      description: pullRequest.body || '*No description provided*',
      author: {
        name: sender.login,
        url: sender.html_url,
        icon_url: sender.avatar_url,
      },
      fields,
    })

    const key = `${repository.full_name}#${pullRequest.number}-${action}`
    return this.sendMessage(key, {
      content: reviewersMentions,
      embeds: [embed],
    })
  }

  private getReviewersText(reviewers: (User | Team)[]): string {
    if (reviewers.length === 0) {
      return '*No reviewers provided*'
    }

    return reviewers
      .map((reviewer) => {
        if ('login' in reviewer) {
          return `@${reviewer.login}`
        }
        return reviewer.name
      })
      .filter((mention) => mention !== null)
      .join(' ')
  }

  private getReviewerMentions(reviewers: (User | Team)[]): string {
    const githubUserMap = new GitHubUserMap()
    return reviewers
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

  private getColor(
    action: PullRequestEvent['action']
  ): (typeof EmbedColors)[keyof typeof EmbedColors] {
    const colorMap: Record<
      PullRequestEvent['action'],
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
      synchronize: EmbedColors.PullRequestSynchronize,
      unassigned: EmbedColors.PullRequestUnassigned,
      unlabeled: EmbedColors.PullRequestUnlabeled,
      unlocked: EmbedColors.PullRequestUnlocked,
    }

    return colorMap[action]
  }
}
