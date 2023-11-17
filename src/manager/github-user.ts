import { GWBEnvironment } from '../environments'
import { BaseRecordManager } from './base-record'

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
}
