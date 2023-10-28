import { PushEvent, Commit } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '@/utils'

export class PushAction extends BaseAction<PushEvent> {
  public run(): Promise<void> {
    const { ref, commits, repository, sender } = this.event

    const embed = createEmbed(this.eventName, {
      title: `[${repository.full_name}:${ref}] ${commits.length} new commit(s)`,
      description: this.getDescription(commits),
      author: {
        name: sender.name,
        icon_url: `https://avatars.githubusercontent.com/u/${sender.id}`,
      },
    })

    return this.discord.sendMessage({
      embeds: [embed],
    })
  }

  private getDescription(commits: Commit[]): string {
    return commits
      .map((commit) => {
        const { id, url, message, author } = commit
        const { name } = author
        return `[${id.slice(0, 7)}](${url}) ${message} - ${name}`
      })
      .join('\n')
  }
}
