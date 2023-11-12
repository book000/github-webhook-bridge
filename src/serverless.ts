import { FastifyRequest, FastifyReply } from 'fastify'
import { getApp } from './main'

export default async (request: FastifyRequest, response: FastifyReply) => {
  const app = await getApp()
  await app.ready()
  app.server.emit('request', request, response)
}
