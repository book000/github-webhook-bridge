import { PublicEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '../utils'
import { EmbedColors } from '../embed-colors'

export class PublicAction extends BaseAction<PublicEvent> {
  public run(): Promise<void> {
    const embed = createEmbed(this.eventName, EmbedColors.Public, {
      title: `Published ${this.event.repository.full_name} by ${this.event.sender.login}`,
      url: this.event.repository.html_url,
      author: {
        name: this.event.sender.login,
        icon_url: this.event.sender.avatar_url,
        url: this.event.sender.html_url,
      },
    })

    const key = `${this.event.repository.full_name}-public-${this.event.sender.login}`
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }
}
