import { Logger } from '@book000/node-utils'
import { main } from './main'

/**
 * CLI エントリーポイント。
 * サーバーを起動し、起動に失敗した場合はエラーログを出力してプロセスを終了する。
 */
;(async () => {
  try {
    await main()
  } catch (error) {
    const normalizedError =
      error instanceof Error ? error : new Error(String(error))
    Logger.configure('main').error('Error', normalizedError)
    // eslint-disable-next-line unicorn/no-process-exit
    process.exit(1)
  }
})()
