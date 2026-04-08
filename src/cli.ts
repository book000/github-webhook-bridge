import { Logger } from '@book000/node-utils'
import { main } from './main'

// CLI エントリーポイント: サーバーを起動する
;(async () => {
  try {
    await main()
  } catch (error) {
    Logger.configure('main').error('Error', error as Error)
    // eslint-disable-next-line unicorn/no-process-exit
    process.exit(1)
  }
})()
