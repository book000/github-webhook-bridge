import { PushEvent, Commit } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '@/utils'
import { EmbedColors } from '@/embed-colors'

export class PushAction extends BaseAction<PushEvent> {
  public run(): Promise<void> {
    const { ref, commits, repository, sender } = this.event

    const embed = createEmbed(this.eventName, EmbedColors.Push, {
      title: `[${repository.full_name}:${ref}] ${commits.length} new commit(s)`,
      description: this.getDescription(commits),
      author: {
        name: sender.name,
        url: sender.html_url,
        icon_url: sender.avatar_url,
      },
    })

    const key = `${repository.full_name}:${ref}`
    return this.sendMessage(key, {
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
