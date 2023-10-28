import { IncomingHttpHeaders } from 'node:http'
import crypto, { BinaryLike, timingSafeEqual } from 'node:crypto'
import { DiscordEmbed } from '@book000/node-utils'

export function isSignatureValid(
  secret: string,
  headers: IncomingHttpHeaders,
  payload: BinaryLike
): boolean {
  const signature = headers['x-hub-signature']
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
  extraEmbed: Omit<DiscordEmbed, 'footer' | 'timestamp'>
): DiscordEmbed {
  return {
    footer: {
      text: `Powered by github-webhook-bot (${eventName} event)`,
      icon_url: 'https://i.imgur.com/PdvExHP.png',
    },
    timestamp: new Date().toISOString(),
    ...extraEmbed,
  }
}
