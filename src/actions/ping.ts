import { PingEvent } from '@octokit/webhooks-types'
import { BaseAction } from '.'
import { createEmbed } from '@/utils'

export class PingAction extends BaseAction<PingEvent> {
  public run(): Promise<void> {
    const { zen, repository, hook, sender, organization } = this.event

    const embed = createEmbed(this.eventName, {
      title: 'Received a ping event',
      fields: [
        {
          name: 'Zen',
          value: zen,
        },
        {
          name: 'Hook Type',
          value: hook.type,
        },
        {
          name: 'Repository',
          value: repository?.full_name ?? 'N/A',
        },
        {
          name: 'Sender',
          value: sender?.login ?? 'N/A',
        },
        {
          name: 'Organization',
          value: organization?.login ?? 'N/A',
        },
      ],
    })

    return this.discord.sendMessage({
      embeds: [embed],
    })
  }
}
