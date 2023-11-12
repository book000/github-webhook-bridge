import { GWBEnvironment } from '@/environments'
import { BaseSetManager } from './base-set'

/**
 * 通知をミュートするユーザーを管理するクラス
 */
export class MuteManager extends BaseSetManager<number> {
  protected readonly fileUrl: string | null = GWBEnvironment.getOrNull(
    'MUTE_USERS_FILE_URL'
  )

  protected readonly filePath: string = GWBEnvironment.get(
    'MUTE_USERS_FILE_PATH',
    'data/mute-users.json'
  )

  /**
   * ミュートしているかどうかを返す
   *
   * @param userId GitHub のユーザー ID
   * @returns ミュートしているかどうか
   */
  public isMuted(userId: number): boolean {
    if (!this.loaded) throw new Error('not loaded')
    return this.data.has(userId)
  }
}
