import fastify, { FastifyReply, FastifyRequest } from 'fastify'
import cors from '@fastify/cors'
import { Schema } from '@octokit/webhooks-types'
import { Discord, Logger } from '@book000/node-utils'
import { isSignatureValid } from './utils'
import { GWBEnvironment } from './environments'
import { getAction } from './get-action'
import fastifyRawBody from 'fastify-raw-body'
import { MuteManager } from './manager/mute'

async function hook(
  request: FastifyRequest<{
    Body: Schema
  }>,
  reply: FastifyReply
) {
  const headers = request.headers
  const secret = GWBEnvironment.get('GITHUB_WEBHOOK_SECRET')
  if (!request.rawBody) {
    reply.status(400).send({
      message: 'Bad Request: Invalid body',
    })
    return
  }
  if (!isSignatureValid(secret, headers, request.rawBody)) {
    reply.status(400).send({
      message: 'Bad Request: Invalid X-Hub-Signature',
    })
    return
  }
  const eventName = headers['x-github-event']
  if (!eventName || typeof eventName !== 'string') {
    reply.status(400).send({
      message: 'Bad Request: Invalid X-GitHub-Event',
    })
    return
  }

  const muteManager = new MuteManager()
  await muteManager.load()
  if (
    'sender' in request.body &&
    request.body.sender?.id &&
    muteManager.isMuted(request.body.sender.id)
  ) {
    reply.status(200).send({
      message: 'Muted user',
    })
    return
  }

  const discord = new Discord({
    webhookUrl: GWBEnvironment.get('DISCORD_WEBHOOK_URL'),
  })

  const action = getAction(discord, eventName, request.body)
  if (!action) {
    reply.status(400).send({
      message: 'Bad Request: Invalid event',
    })
    return
  }

  try {
    await action.run()
  } catch (error) {
    Logger.configure('hook').error('Error', error as Error)
    reply.status(500).send({
      message: 'An error occurred: ' + (error as Error).message,
    })
  }
}

export async function getApp() {
  const app = fastify()
  app.register(cors, {
    origin: true,
    credentials: true,
    methods: ['GET', 'POST'],
  })
  await app.register(fastifyRawBody)

  app.get('/', (_request, reply) => {
    reply.status(400).send({
      message: 'Bad Request: Please use POST method',
    })
  })
  app.post('/', hook)

  return app
}

async function main() {
  const logger = Logger.configure('main')

  const app = await getApp()

  const port = GWBEnvironment.getNumber('API_PORT', 3000)
  app.listen(
    {
      host: '0.0.0.0',
      port,
    },
    (error, address) => {
      if (error) {
        logger.error('Listen error', error)
        // eslint-disable-next-line unicorn/no-process-exit
        process.exit(1)
      }
      logger.info(`Server listening at ${address}`)
    }
  )
}

;(async () => {
  try {
    await main()
  } catch (error) {
    Logger.configure('main').error('Error', error as Error)
    // eslint-disable-next-line unicorn/no-process-exit
    process.exit(1)
  }
})()
