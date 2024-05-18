import { ForkEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '../utils'
import { EmbedColors } from 'src/embed-colors'

export class ForkAction extends BaseAction<ForkEvent> {
  public run(): Promise<void> {
    const embed = createEmbed(this.eventName, EmbedColors.Fork, {
      title: `Forked ${this.event.repository.full_name} by ${this.event.sender.login} to ${this.event.forkee.full_name}`,
      url: this.event.forkee.html_url,
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
