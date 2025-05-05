import axios from 'axios'
import fs from 'node:fs'
import path from 'node:path'
import { parse } from 'jsonc-parser'

export abstract class BaseRecordManager<T extends string | number | symbol, U> {
  protected abstract readonly fileUrl: string | null
  protected abstract readonly filePath: string
  protected loaded = false

  protected data: Record<T, U> = {} as Record<T, U>

  public async load(): Promise<void> {
    if (this.loaded) return

    if (this.fileUrl) {
      const raw = await axios.get(this.fileUrl, {
        responseType: 'text',
      })
      const result = parse(raw.data)
      if (typeof result !== 'object') throw new Error('data is not object')

      this.data = result
      this.loaded = true
      return
    }

    if (!fs.existsSync(this.filePath)) {
      this.data = {} as Record<T, U>
      if (!fs.existsSync(path.dirname(this.filePath))) {
        fs.mkdirSync(path.dirname(this.filePath), { recursive: true })
      }
      fs.writeFileSync(this.filePath, JSON.stringify(this.data))
      this.loaded = true
      return
    }

    const file = fs.readFileSync(this.filePath, 'utf8')
    const data = JSON.parse(file)
    if (typeof data !== 'object') throw new Error('data is not object')
    this.data = data
    this.loaded = true
  }

  public save(): void {
    if (!this.loaded) throw new Error('not loaded')
    if (this.fileUrl) throw new Error('cannot save to url')
    fs.writeFileSync(this.filePath, JSON.stringify(this.data))
  }
}
