import { StarEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '../utils'
import { EmbedColors } from '../embed-colors'

export class StarAction extends BaseAction<StarEvent> {
  public run(): Promise<void> {
    const { action } = this.event

    const titlePrefix = action === 'created' ? 'Starred' : 'Unstarred'
    const color = action === 'created' ? EmbedColors.Star : EmbedColors.Unstar

    const embed = createEmbed(this.eventName, color, {
      title: `${titlePrefix} ${this.event.repository.full_name} by ${this.event.sender.login}`,
      url: this.event.repository.html_url,
      author: {
        name: this.event.sender.login,
        icon_url: this.event.sender.avatar_url,
        url: this.event.sender.html_url,
      },
    })

    const key = `${this.event.repository.full_name}-star-${this.event.sender.login}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }
}
