import puppeteer from 'puppeteer-core'
import fs from 'node:fs'

async function generate(embed, path) {
  const encodedBase64 = Buffer.from(JSON.stringify(embed)).toString('base64')
  const baseUrl = 'https://embed.dan.onl/?data=' + encodedBase64
  console.log('baseUrl:', baseUrl)
  const browser = await puppeteer.launch({
    headless: true,
    slowMo: 100,
    channel: 'chrome',
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
      '--disable-accelerated-2d-canvas',
      '--no-first-run',
      '--no-zygote',
      '--disable-gpu',
      '--window-size=550,2000',
    ],
  })
  const page = await browser.newPage()
  await page.setViewport({ width: 550, height: 2000 })
  await page.goto(baseUrl, { waitUntil: 'networkidle2' })

  const directory = path.split('/').slice(0, -1).join('/')
  if (!fs.existsSync(directory)) {
    fs.mkdirSync(directory, { recursive: true })
  }

  // div.screen 以下にある二つ目の div
  await page.evaluate(() => {
    const selectors = [
      'div.screen > div:nth-child(2) > div',
      'div.screen > div:nth-child(1)',
    ]
    for (const selector of selectors) {
      const element = document.querySelector(selector)
      element.remove()
    }
  })

  const element = await page.$('div.screen > div:nth-child(1) > article')
  if (!element) {
    throw new Error('element not found')
  }
  const elementHeight = await page.evaluate((element) => {
    return element.clientHeight
  }, element)

  await page.screenshot({
    path,
    clip: { x: 0, y: 0, width: 550, height: elementHeight + 64 },
  })

  await browser.close()
}

function readdirSyncRecursive(baseDirectory, deepDirectory = '') {
  const paths = []
  const files = fs.readdirSync(baseDirectory + deepDirectory)
  for (const file of files) {
    const path = deepDirectory + (deepDirectory ? '/' : '')
    if (fs.statSync(baseDirectory + path + file).isDirectory()) {
      paths.push(...readdirSyncRecursive(baseDirectory, path + file))
    } else {
      paths.push(path + file)
    }
  }
  return paths
}

// 並行処理クラス
class ParallelRunner {
  options = []

  constructor(options) {
    this.options = options
  }

  async run(concurrency) {
    const runners = []
    for (let index = 0; index < concurrency; index++) {
      runners.push(this.runner(index))
    }
    await Promise.all(runners)
  }

  async runner(runnerId) {
    while (this.options.length > 0) {
      const option = this.options.shift()
      if (!option) {
        break
      }

      console.log(
        `Runner ${runnerId}: ${option.path} (remaining: ${this.options.length})`
      )
      await generate(option.embed, '../docs/' + option.path)
    }
  }
}

function toLowerCamel(string) {
  return string
    .toLowerCase()
    .replaceAll(/([_-][a-z])/g, (group) =>
      group.toUpperCase().replace('-', '').replace('_', '')
    )
}

function snakeCaseToLowerCamelCase(object) {
  const TYPE_STRING = {
    OBJECT: '[object Object]',
    ARRAY: '[object Array]',
  }

  const toString = Object.prototype.toString

  if (toString.call(object) === TYPE_STRING.OBJECT) {
    // ObjectはキーをLowerCamelに変換する
    const result = {}
    for (const key of Object.keys(object)) {
      if (
        toString.call(object[key]) === TYPE_STRING.OBJECT ||
        toString.call(object[key]) === TYPE_STRING.ARRAY
      ) {
        // @ts-expect-error ここはObjectかArrayの場合の処理
        result[toLowerCamel(key)] = snakeCaseToLowerCamelCase(object[key])
      } else {
        // @ts-expect-error ここはそれ以外の場合の処理
        result[toLowerCamel(key)] = object[key]
      }
    }
    return result
  } else if (toString.call(object) === TYPE_STRING.ARRAY) {
    // Arrayの時は内部のデータがObjectの場合は処理をかける
    const result = []
    for (const item of object) {
      if (toString.call(item) === TYPE_STRING.OBJECT) {
        result.push(snakeCaseToLowerCamelCase(item))
      } else {
        result.push(item)
      }
    }
    return result
  } else {
    // Object以外はそのまま返す
    return object
  }
}

async function main() {
  const baseDirectory = '../data/debug/'
  const paths = readdirSyncRecursive(baseDirectory)

  const options = []
  for (const path of paths) {
    if (!path.endsWith('.json')) {
      continue
    }

    const data = JSON.parse(fs.readFileSync(baseDirectory + path, 'utf8'))
    const embed = data.embeds[0]
    if (!embed) {
      continue
    }

    options.push({
      embed: snakeCaseToLowerCamelCase(embed),
      path: path.replace('.json', '.png'),
    })
  }

  const runner = new ParallelRunner(options)
  await runner.run(5)

  // generate readme.md
  // ## path
  // ![embed](data/path.png)
  const fileMap = {}
  for (const path of paths) {
    if (!path.endsWith('.json')) {
      continue
    }

    const pngPath = path.replace('.json', '.png')
    const splitPath = path.split('/')
    const eventName = splitPath[0]
    // eslint-disable-next-line unicorn/no-null
    const actionName = splitPath.length === 3 ? splitPath[1] : null
    const fileName = splitPath.at(-1)?.replace('.json', '')
    const title = actionName ? `${eventName} - ${actionName}` : eventName

    if (!(title in fileMap)) {
      fileMap[title] = []
    }
    fileMap[title].push(`![${fileName}](${pngPath})`)
  }

  const readme = ['# Sample Discord message', '']
  for (const [title, files] of Object.entries(fileMap)) {
    readme.push(`## ${title}`, '', ...files, '')
  }

  fs.writeFileSync('../docs/README.md', readme.join('\n').trim())
}

// eslint-disable-next-line unicorn/prefer-top-level-await
;(async () => {
  await main()
})()
