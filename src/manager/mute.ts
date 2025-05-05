import { GWBEnvironment } from '../environments'
import { BaseSetManager } from './base-set'

interface MuteEvent {
  /**
   * イベントの名前
   */
  eventName: string

  /**
   * イベントのアクション
   */
  actions: string[] | null
}

interface MuteRecord {
  /**
   * GitHub のユーザー ID
   */
  userId: number

  /**
   * ミュート方式
   *
   * - `include` : 指定したイベントのみミュート
   * - `exclude` : 指定したイベントを除いてミュート
   * - `all` : 全てのイベントをミュート
   */
  type: 'include' | 'exclude' | 'all'

  /**
   * 対象のイベントのリスト
   */
  events: MuteEvent[]
}

/**
 * 通知をミュートする対象を管理するクラス
 */
export class MuteManager extends BaseSetManager<MuteRecord> {
  protected readonly fileUrl: string | null =
    GWBEnvironment.getOrNull('MUTES_FILE_URL')

  protected readonly filePath: string = GWBEnvironment.get(
    'MUTES_FILE_PATH',
    'data/mutes.json'
  )

  /**
   * ミュートしているかどうかを返す
   *
   * @param userId GitHub のユーザー ID
   * @returns ミュートしているかどうか
   */
  public isMuted(
    userId: number,
    eventName: string,
    action: string | null
  ): boolean {
    if (!this.loaded) throw new Error('not loaded')

    const record = [...this.data].find((record) => record.userId === userId)
    if (!record) return false // 対象ユーザーのミュート設定がない場合はミュートしない
    if (record.type === 'all') return true // 全てのイベントをミュートする場合は常に true を返す
    if (record.type === 'include') {
      // 指定したイベントのみミュートする場合は、対象のイベントが含まれているかどうかをチェック
      return record.events.some((event) => {
        if (event.eventName !== eventName) return false
        // actions が null の場合は全てのアクションを対象とする
        if (event.actions === null) return true
        // actions が null でない場合は、指定されたアクションが含まれているかどうかをチェック
        if (action === null) return false // action が null の場合はミュートしない
        return event.actions.includes(action)
      })
    }
    // 指定したイベントを除いてミュートする場合は、対象のイベントが含まれていないかどうかをチェック
    return !record.events.some((event) => {
      if (event.eventName !== eventName) return false
      // actions が null の場合は全てのアクションを対象とする
      if (event.actions === null) return false
      // actions が null でない場合は、指定されたアクションが含まれているかどうかをチェック
      if (action === null) return true // action が null の場合はミュートする
      return event.actions.includes(action)
    })
  }
}
