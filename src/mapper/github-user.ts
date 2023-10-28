import { GWBEnvironment } from '@/environments'
import fs from 'node:fs'

/**
 * GitHub のユーザーと、Discord のユーザーを紐付ける
 */
export class GitHubUserMap {
  private readonly filePath: string
  private readonly map: Record<number, string>

  constructor() {
    this.filePath = GWBEnvironment.get(
      'GITHUB_USER_MAP_FILE_PATH',
      'data/github-user-map.json'
    )

    if (!fs.existsSync(this.filePath)) {
      this.map = {}
      fs.writeFileSync(this.filePath, JSON.stringify(this.map))
      return
    }

    const file = fs.readFileSync(this.filePath, 'utf8')
    this.map = JSON.parse(file)
  }

  /**
   * GitHub のユーザーと Discord のユーザーを紐付ける
   * @param githubUserId GitHub のユーザー ID
   * @param discordUserId Discord のユーザー ID
   */
  public set(githubUserId: number, discordUserId: string): void {
    this.map[githubUserId] = discordUserId
    fs.writeFileSync(this.filePath, JSON.stringify(this.map))
  }

  /**
   * GitHub のユーザー ID から Discord のユーザー ID を取得する
   * @param githubUserId GitHub のユーザー ID
   * @returns Discord のユーザー ID
   */
  public get(githubUserId: number): string | undefined {
    return this.map[githubUserId]
  }

  /**
   * GitHub のユーザー ID から Discord のユーザー ID を削除する
   * @param githubUserId GitHub のユーザー ID
   */
  public delete(githubUserId: number): void {
    delete this.map[githubUserId]
    fs.writeFileSync(this.filePath, JSON.stringify(this.map))
  }
}
