import { PingEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '../utils'
import { EmbedColors } from '../embed-colors'

export class PingAction extends BaseAction<PingEvent> {
  public run(): Promise<void> {
    const { zen, repository, hook, sender, organization } = this.event

    const embed = createEmbed(this.eventName, EmbedColors.Ping, {
      title: 'Received a ping event',
      description: zen,
      fields: [
        {
          name: 'Hook Type',
          value: hook.type,
          inline: true,
        },
        {
          name: 'Repository',
          value: repository?.full_name ?? 'N/A',
          inline: true,
        },
        {
          name: 'Sender',
          value: sender?.login ?? 'N/A',
          inline: true,
        },
        {
          name: 'Organization',
          value: organization?.login ?? 'N/A',
          inline: true,
        },
      ],
    })

    const key = [
      repository?.full_name ?? 'N/A',
      sender?.login ?? 'N/A',
      organization?.login ?? 'N/A',
      hook.type,
    ].join(':')
    return this.sendMessage(key, {
      embeds: [embed],
    })
  }
}
