import { GWBEnvironment } from '../environments'
import { BaseRecordManager } from './base-record'
import axios from 'axios'

export interface GitHubApiGetUserResponse {
  login: string
  id: number
  node_id: string
  avatar_url: string
  gravatar_id: string
  url: string
  html_url: string
  followers_url: string
  following_url: string
  gists_url: string
  starred_url: string
  subscriptions_url: string
  organizations_url: string
  repos_url: string
  events_url: string
  received_events_url: string
  type: string
  site_admin: boolean
  name: string
  company: string
  blog: string
  location: string
  email: any
  hireable: boolean
  bio: string
  twitter_username: string
  public_repos: number
  public_gists: number
  followers: number
  following: number
  created_at: string
  updated_at: string
}

/**
 * GitHub のユーザーと、Discord のユーザーを紐付ける
 */
export class GitHubUserMapManager extends BaseRecordManager<number, string> {
  protected readonly fileUrl: string | null = GWBEnvironment.getOrNull(
    'GITHUB_USER_MAP_FILE_URL'
  )

  protected readonly filePath: string = GWBEnvironment.get(
    'GITHUB_USER_MAP_FILE_PATH',
    'data/github-user-map.json'
  )

  /**
   * GitHub のユーザー ID から Discord のユーザー ID を取得する
   * @param githubUserId GitHub のユーザー ID
   * @returns Discord のユーザー ID
   */
  public get(githubUserId: number): string | undefined {
    if (!this.loaded) throw new Error('not loaded')
    return this.data[githubUserId]
  }

  public async getFromUsername(githubUsername: string) {
    const response = await axios.get<GitHubApiGetUserResponse>(
      `https://api.github.com/users/${githubUsername}`
    )

    const githubUserId = response.data.id

    return this.get(githubUserId)
  }
}
