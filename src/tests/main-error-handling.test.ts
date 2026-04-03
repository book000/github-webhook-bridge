import { Logger } from '@book000/node-utils'
import { AxiosError } from 'axios'
import { FastifyInstance } from 'fastify'
import * as getActionModule from '../get-action'
import { getApp } from '../main'
import * as utils from '../utils'

describe('Main error handling', () => {
  let app: FastifyInstance
  let loggerError: jest.Mock
  let loggerInfo: jest.Mock

  beforeAll(async () => {
    process.env.GITHUB_WEBHOOK_SECRET = '1234567890'
    process.env.DISCORD_WEBHOOK_URL =
      'https://discord.com/api/webhooks/1234567890/1234567890'

    app = await getApp()
    await app.ready()
  })

  afterAll(async () => {
    await app.close()
  })

  beforeEach(() => {
    loggerError = jest.fn()
    loggerInfo = jest.fn()

    jest.spyOn(utils, 'isSignatureValid').mockReturnValue(true)
    jest.spyOn(Logger, 'configure').mockReturnValue({
      error: loggerError,
      info: loggerInfo,
    } as never)
  })

  afterEach(() => {
    jest.restoreAllMocks()
  })

  it('should mask webhook URL and avoid exposing axios details in response', async () => {
    const webhookUrl =
      'https://discord.com/api/webhooks/1234567890/secret-token'
    const error = new AxiosError('Request failed with status code 500')
    Object.assign(error, {
      config: {
        method: 'post',
        url: webhookUrl,
      },
      response: {
        status: 500,
        config: {
          method: 'post',
          url: webhookUrl,
        },
      },
    })

    jest
      .spyOn(getActionModule, 'getAction')
      .mockReturnValue({ run: jest.fn().mockRejectedValue(error) } as never)

    const response = await app.inject({
      method: 'POST',
      url: '/',
      headers: {
        'x-github-event': 'ping',
        'x-hub-signature': 'sha1=1234567890',
      },
      payload: {
        zen: 'Keep it logically awesome.',
      },
    })

    expect(response.statusCode).toBe(500)
    expect(JSON.parse(response.body)).toEqual({
      message: 'An error occurred (AxiosError)',
    })

    const loggedMessages = loggerError.mock.calls.flat().map(String).join('\n')
    expect(loggedMessages).toContain(
      'https://discord.com/api/webhooks/1234567890/[REDACTED]'
    )
    expect(loggedMessages).not.toContain('secret-token')
  })

  it('should avoid exposing generic error details in response', async () => {
    const error = new Error('Unexpected failure')

    jest
      .spyOn(getActionModule, 'getAction')
      .mockReturnValue({ run: jest.fn().mockRejectedValue(error) } as never)

    const response = await app.inject({
      method: 'POST',
      url: '/',
      headers: {
        'x-github-event': 'ping',
        'x-hub-signature': 'sha1=1234567890',
      },
      payload: {
        zen: 'Keep it logically awesome.',
      },
    })

    expect(response.statusCode).toBe(500)
    expect(JSON.parse(response.body)).toEqual({
      message: 'An error occurred',
    })
    expect(response.body).not.toContain('details')
    expect(response.body).not.toContain('Unexpected failure')
  })
})
