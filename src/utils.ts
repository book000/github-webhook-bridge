import { IncomingHttpHeaders } from 'node:http'
import crypto from 'node:crypto'

export function isSignatureValid(
  secret: string,
  headers: IncomingHttpHeaders
): boolean {
  const signature = headers['x-hub-signature']
  if (!signature) {
    return false
  }
  if (typeof signature !== 'string') {
    return false
  }
  const [algorithm, signatureHash] = signature.split('=')
  if (!algorithm || !signatureHash) {
    return false
  }
  const payload = JSON.stringify(headers)
  const hmac = crypto.createHmac(algorithm, secret)
  hmac.update(payload)
  const digest = hmac.digest('hex')
  return signatureHash === digest
}
