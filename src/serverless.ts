import { getApp } from './main'
import type { VercelRequest, VercelResponse } from '@vercel/node'

export default async function (
  request: VercelRequest,
  response: VercelResponse
) {
  const app = await getApp()
  await app.ready()
  app.server.emit('request', request, response)
}
