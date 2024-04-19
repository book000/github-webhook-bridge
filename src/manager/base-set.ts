import axios from 'axios'
import fs from 'node:fs'

export abstract class BaseSetManager<T> {
  protected abstract readonly fileUrl: string | null
  protected abstract readonly filePath: string
  protected loaded = false

  protected data = new Set<T>()

  public async load(): Promise<void> {
    if (this.loaded) return

    if (this.fileUrl) {
      const result = await axios.get(this.fileUrl, {
        responseType: 'json',
      })
      if (!Array.isArray(result.data)) throw new Error('data is not array')

      this.data = new Set(result.data)
      this.loaded = true
      return
    }

    if (!fs.existsSync(this.filePath)) {
      this.data = new Set()
      fs.writeFileSync(this.filePath, JSON.stringify([...this.data]))
      this.loaded = true
      return
    }

    const file = fs.readFileSync(this.filePath, 'utf8')
    const data = JSON.parse(file)
    if (!Array.isArray(data)) throw new Error('data is not array')
    this.data = new Set(data)
    this.loaded = true
  }

  public save(): void {
    if (!this.loaded) throw new Error('not loaded')
    if (this.fileUrl) throw new Error('cannot save to url')
    fs.writeFileSync(this.filePath, JSON.stringify([...this.data]))
  }
}
