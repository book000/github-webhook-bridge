import fastify, { FastifyReply, FastifyRequest } from 'fastify'
import cors from '@fastify/cors'
import { Schema, WebhookEventName } from '@octokit/webhooks-types'
import { Discord, Logger } from '@book000/node-utils'
import { isSignatureValid } from './utils'
import { GWBEnvironment } from './environments'
import { getAction } from './get-action'
import fastifyRawBody from 'fastify-raw-body'
import { MuteManager } from './manager/mute'
import { isAxiosError } from 'axios'

/**
 * ログ出力用に URL 内の機密情報をマスクします。
 *
 * @param requestUrl 元のリクエスト URL
 * @returns 機密情報をマスクした URL
 */
function sanitizeRequestUrl(
  requestUrl: string | undefined
): string | undefined {
  if (!requestUrl) {
    return undefined
  }

  try {
    const parsedUrl = new URL(requestUrl)
    const pathSegments = parsedUrl.pathname.split('/')
    if (
      pathSegments.length >= 5 &&
      pathSegments[1] === 'api' &&
      pathSegments[2] === 'webhooks'
    ) {
      pathSegments[4] = '[REDACTED]'
      parsedUrl.pathname = pathSegments.join('/')
    }

    return parsedUrl.toString()
  } catch {
    return '[Invalid URL]'
  }
}

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
    muteManager.isMuted(
      request.body.sender.id,
      eventName,
      'action' in request.body ? request.body.action : null
    )
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

  const action = getAction(discord, eventName as WebhookEventName, request.body)
  try {
    await action.run()
  } catch (err) {
    const logger = Logger.configure('hook')

    if (!err || !(err instanceof Error)) {
      await reply.status(500).send({
        message: 'An error occurred (UnknownError)',
      })
      logger.error('UnknownError')
      return
    }

    // Method not implemented.
    if (err.message === 'Method not implemented.') {
      await reply.status(406).send({
        message: 'Method not implemented',
      })
      logger.info('Method not implemented')
      return
    }

    // AxiosError
    if (isAxiosError(err)) {
      const requestConfig = err.config ?? err.response?.config
      const requestMethod = requestConfig?.method
      const requestUrl = sanitizeRequestUrl(requestConfig?.url)
      const responseStatus = err.response?.status

      await reply.status(500).send({
        message: 'An error occurred (AxiosError)',
      })
      logger.error('AxiosError')
      logger.error(`- Request Method: ${requestMethod}`)
      logger.error(`- Request URL: ${requestUrl}`)
      logger.error(`- Response Status: ${responseStatus}`)
      logger.error(`- Message: ${err.message}`)
      return
    }

    await reply.status(500).send({
      message: 'An error occurred',
    })
    logger.error('Error')
    logger.error(`- Message: ${err.message}`)
    logger.error(`- Stack: ${err.stack}`)
  }
}

/**
 * Fastify アプリケーションを生成して初期化します。
 *
 * @returns 初期化済みの Fastify アプリケーション
 */
export async function getApp() {
  const app = fastify()
  await app.register(cors, {
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

/**
 * HTTP サーバーを起動します。
 *
 * @returns サーバー起動処理の Promise
 */
export async function main() {
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
