type EnvironmentKey =
  | 'API_PORT'
  | 'GITHUB_WEBHOOK_SECRET'
  | 'DISCORD_WEBHOOK_URL'
  | 'GITHUB_USER_MAP_FILE_PATH'
  | 'GITHUB_USER_MAP_FILE_URL'
  | 'MUTE_USERS_FILE_PATH'
  | 'MUTE_USERS_FILE_URL'
  | 'DESABLED_EVENTS'

export class GWBEnvironment {
  public static get(key: EnvironmentKey, defaultValue?: string): string {
    const value = process.env[key] ?? defaultValue
    if (value === undefined) {
      throw new Error(`Environment variable ${key} is not set`)
    }
    return value
  }

  public static getOrNull(key: EnvironmentKey): string | null {
    const value = process.env[key]
    if (value === undefined) {
      return null
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
