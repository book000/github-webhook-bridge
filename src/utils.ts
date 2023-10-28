import { IncomingHttpHeaders } from 'node:http'
import crypto, { BinaryLike, timingSafeEqual } from 'node:crypto'
import { DiscordEmbed } from '@book000/node-utils'
import { EmbedColors } from './embed-colors'

export type SomeRequired<T, K extends keyof T> = Omit<T, K> &
  Required<Pick<T, K>>

export function isSignatureValid(
  secret: string,
  headers: IncomingHttpHeaders,
  payload: BinaryLike
): boolean {
  const signature = headers['x-hub-signature-256']
  if (!signature) {
    return false
  }
  if (typeof signature !== 'string') {
    return false
  }
  const [algorithm, signatureHash] = signature.split('=')
  if (!algorithm || !signatureHash) {
    return false
  }
  const hmac = crypto.createHmac(algorithm, secret)
  hmac.update(payload)
  const digest = hmac.digest('hex')
  return timingSafeEqual(
    Buffer.from(digest, 'ascii'),
    Buffer.from(signatureHash, 'ascii')
  )
}

export function createEmbed(
  eventName: string,
  embedColor: (typeof EmbedColors)[keyof typeof EmbedColors],
  extraEmbed: SomeRequired<
    Omit<DiscordEmbed, 'footer' | 'timestamp' | 'color'>,
    'title'
  >
): DiscordEmbed {
  return {
    footer: {
      text: `Powered by [book000/github-webhook-bridge](https://github.com/book000/github~webhook-bridge) (${eventName} event)`,
      icon_url: 'https://i.imgur.com/PdvExHP.png',
    },
    timestamp: new Date().toISOString(),
    color: embedColor,
    ...extraEmbed,
  }
}
