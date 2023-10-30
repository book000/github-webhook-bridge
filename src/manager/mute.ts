import { GWBEnvironment } from '@/environments'
import fs from 'node:fs'

/**
 * 通知をミュートするユーザーを管理するクラス
 */
export class MuteManager {
  private readonly path
  private readonly mutes: Set<number> = new Set()

  public constructor() {
    this.path = GWBEnvironment.get('GITHUB_USER_MAP_FILE_PATH')

    this.load()
  }

  /**
   * ミュートするユーザーを追加する
   *
   * @param userId GitHub のユーザー ID
   */
  public add(userId: number): void {
    this.mutes.add(userId)
  }

  /**
   * ミュートするユーザーを削除する
   *
   * @param userId GitHub のユーザー ID
   */
  public remove(userId: number): void {
    this.mutes.delete(userId)
  }

  /**
   * ミュートしているかどうかを返す
   *
   * @param userId GitHub のユーザー ID
   * @returns ミュートしているかどうか
   */
  public isMuted(userId: number): boolean {
    return this.mutes.has(userId)
  }

  /**
   * ミュート状態を保存する
   */
  public save(): void {
    fs.writeFileSync(this.path, JSON.stringify([...this.mutes]))
  }

  /**
   * ミュート状態を読み込む
   */
  public load(): void {
    if (!fs.existsSync(this.path)) {
      this.save()
      return
    }
    const file = fs.readFileSync(this.path, 'utf8')
    const data: number[] = JSON.parse(file)
    this.mutes.clear()
    for (const userId of data) {
      this.mutes.add(userId)
    }
  }
}
