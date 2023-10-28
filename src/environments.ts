type EnvironmentKey =
  | 'API_PORT'
  | 'GITHUB_WEBHOOK_SECRET'
  | 'DISCORD_WEBHOOK_URL'
  | 'GITHUB_USER_MAP_FILE_PATH'

export class GWBEnvironment {
  public static get(key: EnvironmentKey, defaultValue?: string): string {
    const value = process.env[key] ?? defaultValue
    if (value === undefined) {
      throw new Error(`Environment variable ${key} is not set`)
    }
    return value
  }

  public static getNumber(key: EnvironmentKey, defaultValue?: number): number {
    const value = this.get(key, defaultValue?.toString())
    const number = Number(value)
    if (Number.isNaN(number)) {
      if (defaultValue === undefined) {
        throw new Error(`Environment variable ${key} is not a number`)
      }
      return defaultValue
    }
    return number
  }
}
