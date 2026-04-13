export class HttpError extends Error {
  public readonly method: string
  public readonly url: string
  public readonly status: number
  public readonly responseData: unknown

  constructor(
    method: string,
    url: string,
    status: number,
    responseData: unknown
  ) {
    super(`HTTP error: ${status} ${url}`)
    this.name = 'HttpError'
    this.method = method
    this.url = url
    this.status = status
    this.responseData = responseData
  }
}

export async function fetchOrThrow(
  url: string,
  options?: RequestInit
): Promise<Response> {
  const method = options?.method ?? 'GET'
  const res = await fetch(url, options)
  if (!res.ok) {
    let responseData: unknown
    const text = await res.text()
    let result: unknown
    try {
      result = JSON.parse(text)
    } catch {
      responseData = text
      throw new HttpError(method, url, res.status, responseData)
    }
    if (result === null || typeof result !== 'object') {
      responseData = text
      throw new HttpError(method, url, res.status, responseData)
    }
    responseData = result
    throw new HttpError(method, url, res.status, responseData)
  }
  return res
}
