import { DiscordMessage } from '@book000/node-utils'
import { getApp } from '../main'
import fs from 'node:fs'
import { FastifyInstance } from 'fastify'
import WebhookDefinitions from '@octokit/webhooks-examples'
import { BaseAction } from '../actions'
import * as utils from '../utils'

describe('Get embed', () => {
  let app: FastifyInstance

  beforeAll(async () => {
    app = await getApp()
    await app.ready()

    process.env.GITHUB_WEBHOOK_SECRET = '1234567890'
    process.env.DISCORD_WEBHOOK_URL =
      'https://discord.com/api/webhooks/1234567890/1234567890'

    jest.spyOn(utils, 'isSignatureValid').mockReturnValue(true)

    jest.spyOn(utils, 'getUsersMentions').mockImplementation((userOrTeams) => {
      return Promise.resolve(
        userOrTeams
          .map((userOrTeam) => {
            return 'login' in userOrTeam ? `@${userOrTeam.login}` : null
          })
          .join(' ')
      )
    })
  })

  it.each(
    WebhookDefinitions.map((definition) => [definition.name, definition])
  )('should handle %s', async (_, definition) => {
    for (const [exampleId, example] of definition.examples.entries()) {
      const mockInstance = jest
        .spyOn(
          BaseAction.prototype as unknown as {
            sendMessage: (key: string, message: DiscordMessage) => Promise<void>
          },
          'sendMessage'
        )
        .mockImplementationOnce((_: string, message: DiscordMessage) => {
          const eventName = definition.name
          const action = 'action' in example ? example.action : ''
          const path = `data/debug/${eventName}/${action}/${exampleId}.json`
          const directory = path.split('/').slice(0, -1).join('/')
          if (!fs.existsSync(directory)) {
            fs.mkdirSync(directory, {
              recursive: true,
            })
          }
          fs.writeFileSync(path, JSON.stringify(message, null, 2))

          return Promise.resolve()
        })

      const response = await app.inject({
        method: 'POST',
        url: '/',
        headers: {
          'x-github-event': definition.name,
          'x-hub-signature': 'sha1=1234567890',
        },
        payload: example,
      })

      mockInstance.mockRestore()

      if (
        response.body.includes('Method not implemented') &&
        response.statusCode === 406
      ) {
        return
      }
      expect(response.statusCode).toBe(200)
    }
  })
})
