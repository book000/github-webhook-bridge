import { PushEvent, Commit } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '@/utils'
import { EmbedColors } from '@/embed-colors'

export class PushAction extends BaseAction<PushEvent> {
  public run(): Promise<void> {
    const { ref, commits, repository, sender } = this.event

    if (commits.length === 0) return Promise.resolve()

    // eslint-disable-next-line unicorn/prevent-abbreviations
    const shortRef = ref.replace('refs/heads/', '').replace('refs/tags/', '')

    const embed = createEmbed(this.eventName, EmbedColors.Push, {
      title: `[${repository.full_name}:${shortRef}] ${commits.length} new commit(s)`,
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
        const firstLine = message.includes('\n')
          ? message.split('\n')[0]
          : message
        const shortMessage =
          firstLine.length > 50 ? `${firstLine.slice(0, 50)}...` : firstLine

        const { name } = author
        return `[${id.slice(0, 7)}](${url}) ${shortMessage} - ${name}`
      })
      .join('\n')
  }
}
