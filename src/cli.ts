import { Logger } from '@book000/node-utils'
import { main } from './main'
;(async () => {
  try {
    await main()
  } catch (err: unknown) {
    const logger = Logger.configure('main')

    if (err instanceof Error) {
      logger.error('Error', err)
    } else {
      logger.error(`Error: ${String(err)}`)
    }

    // eslint-disable-next-line unicorn/no-process-exit
    process.exit(1)
  }
})()
