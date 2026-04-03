import { Logger } from '@book000/node-utils'
import { main } from './main'
;(async () => {
  try {
    await main()
  } catch (err) {
    Logger.configure('main').error('Error', err as Error)
    // eslint-disable-next-line unicorn/no-process-exit
    process.exit(1)
  }
})()
