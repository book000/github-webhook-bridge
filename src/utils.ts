import { IncomingHttpHeaders } from 'node:http'
import crypto, { BinaryLike, timingSafeEqual } from 'node:crypto'
import { DiscordEmbed } from '@book000/node-utils'
import { EmbedColors } from './embed-colors'
import { User, Team } from '@octokit/webhooks-types'
import { GitHubUserMapManager } from './manager/github-user'

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
      text: `Powered by book000/github-webhook-bridge (${eventName} event)`,
      icon_url: 'https://i.imgur.com/PdvExHP.png',
    },
    timestamp: new Date().toISOString(),
    color: embedColor,
    ...extraEmbed,
  }
}

/**
 * GitHubのユーザーからDiscordのユーザーに変換し、メンション一覧を作成する
 *
 * @param sender アクションを送信したユーザー
 * @param userOrTeams User と Team の配列
 * @returns Discordのメンション一覧
 */
export async function getUsersMentions(
  sender: User,
  userOrTeams: (User | Team)[]
): Promise<string> {
  const githubUserMap = new GitHubUserMapManager()
  await githubUserMap.load()
  return userOrTeams
    .map((reviewer) => {
      if (!('login' in reviewer)) {
        return null
      }

      if (reviewer.login === sender.login) {
        return null
      }

      const discordUserId = githubUserMap.get(reviewer.id)
      if (!discordUserId) {
        return null
      }

      return `<@${discordUserId}>`
    })
    .filter((mention) => mention !== null)
    .join(' ')
}
