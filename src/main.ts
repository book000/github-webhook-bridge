import fastify, { FastifyReply, FastifyRequest } from 'fastify'
import cors from '@fastify/cors'
import { Schema } from '@octokit/webhooks-types'
import { Discord, Logger } from '@book000/node-utils'
import { isSignatureValid } from './utils'
import { GWBEnvironment } from './environments'
import { getAction } from './get-action'
import fastifyRawBody from 'fastify-raw-body'
import { MuteManager } from './manager/mute'
import { isAxiosError } from 'axios'

async function hook(
  request: FastifyRequest<{
    Body: Schema
    Querystring: {
      url?: string
      'disabled-events'?: string
    }
  }>,
  reply: FastifyReply
) {
  const headers = request.headers
  const secret = GWBEnvironment.get('GITHUB_WEBHOOK_SECRET')
  if (!request.rawBody) {
    await reply.status(400).send({
      message: 'Bad Request: Invalid body',
    })
    return
  }
  if (!isSignatureValid(secret, headers, request.rawBody)) {
    await reply.status(400).send({
      message: 'Bad Request: Invalid X-Hub-Signature',
    })
    return
  }
  const eventName = headers['x-github-event']
  if (!eventName || typeof eventName !== 'string') {
    await reply.status(400).send({
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
    await reply.status(200).send({
      message: 'Muted user',
    })
    return
  }

  const webhookUrl =
    request.query.url ?? GWBEnvironment.get('DISCORD_WEBHOOK_URL')

  const discord = new Discord({
    webhookUrl,
  })

  const disabledEvents =
    request.query['disabled-events'] ??
    GWBEnvironment.getOrNull('DISABLED_EVENTS')
  if (disabledEvents) {
    const disabledEventsArray = disabledEvents.split(',')
    if (disabledEventsArray.includes(eventName)) {
      await reply.status(202).send({
        message: 'Disabled event',
      })
      return
    }
  }

  const action = getAction(discord, eventName, request.body)
  try {
    await action.run()
  } catch (error) {
    const logger = Logger.configure('hook')

    if (!error || !(error instanceof Error)) {
      await reply.status(500).send({
        message: 'An error occurred (UnknownError)',
      })
      logger.error('UnknownError')
      return
    }

    // Method not implemented.
    if (error.message === 'Method not implemented.') {
      await reply.status(406).send({
        message: 'Method not implemented',
      })
      logger.info('Method not implemented')
      return
    }

    // AxiosError
    if (isAxiosError(error)) {
      const requestMethod = error.response?.config.method
      const requestUrl = error.response?.config.url
      const responseStatus = error.response?.status
      const responseData = error.response?.data

      await reply.status(500).send({
        message: 'An error occurred (AxiosError)',
        details: {
          requestMethod,
          requestUrl,
          responseStatus,
          responseData,
        },
      })
      logger.error('AxiosError')
      logger.error(`- Request Method: ${requestMethod}`)
      logger.error(`- Request URL: ${requestUrl}`)
      logger.error(`- Response Status: ${responseStatus}`)
      logger.error(`- Response Data: ${JSON.stringify(responseData)}`)
      return
    }

    const errorInfo = {
      message: error.message,
      stack: error.stack,
    }

    await reply.status(500).send({
      message: 'An error occurred',
      details: errorInfo,
    })
    logger.error('Error')
    logger.error(`- Message: ${errorInfo.message}`)
    logger.error(`- Stack: ${errorInfo.stack}`)
  }
}

export async function getApp() {
  const app = fastify()
  await app.register(cors, {
    origin: true,
    credentials: true,
    methods: ['GET', 'POST'],
  })
  await app.register(fastifyRawBody)

  app.get('/', (_request, reply) => {
    // eslint-disable-next-line @typescript-eslint/no-floating-promises
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
