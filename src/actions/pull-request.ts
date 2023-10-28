import { PullRequestEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '@/utils'
import { GitHubUserMap } from '@/mapper/github-user'

export class PullRequestAction extends BaseAction<PullRequestEvent> {
  public run(): Promise<void> {
    const { action, pull_request: pullRequest, repository, sender } = this.event

    const reviewers = pullRequest.requested_reviewers

    const githubUserMap = new GitHubUserMap()
    const reviewersMentions = reviewers
      .map((reviewer) => {
        const discordUserId = githubUserMap.get(reviewer.id)
        if (discordUserId) {
          return `<@${discordUserId}>`
        }
        return null
      })
      .filter((mention) => mention !== null)
      .join(' ')

    const embed = createEmbed(this.eventName, {
      title: `[${repository.full_name}] Pull Request ${action}: #${pullRequest.number} ${pullRequest.title}`,
      description: pullRequest.body || '*No description provided*',
      author: {
        name: sender.login,
        icon_url: `https://avatars.githubusercontent.com/u/${sender.id}`,
      },
    })

    return this.discord.sendMessage({
      content: reviewersMentions,
      embeds: [embed],
    })
  }
}
