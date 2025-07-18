import { PullRequestAction } from '../actions/pull-request'
import { PullRequestEditedEvent } from '@octokit/webhooks-types'
import * as utils from '../utils'
import { Discord } from '@book000/node-utils'

describe('PullRequest WIP functionality', () => {
  let mockSendMessage: jest.SpyInstance
  let mockGetUsersMentions: jest.SpyInstance

  beforeEach(() => {
    mockSendMessage = jest
      .spyOn(PullRequestAction.prototype as any, 'sendMessage')
      .mockImplementation(() => Promise.resolve())

    mockGetUsersMentions = jest
      .spyOn(utils, 'getUsersMentions')
      .mockImplementation(() => Promise.resolve('@reviewer1 @reviewer2'))
  })

  afterEach(() => {
    jest.restoreAllMocks()
  })

  describe('isWipTitle', () => {
    const mockDiscord = {} as Discord
    const mockEvent = {} as PullRequestEditedEvent
    const action = new PullRequestAction(mockDiscord, 'pull_request', mockEvent)
    // eslint-disable-next-line @typescript-eslint/no-unsafe-member-access, @typescript-eslint/no-unsafe-call
    const isWipTitle = (action as any).isWipTitle.bind(action)

    it('should detect WIP in various formats', () => {
      // eslint-disable-next-line @typescript-eslint/no-unsafe-call
      expect(isWipTitle('WIP: Fix issue')).toBe(true)
      // eslint-disable-next-line @typescript-eslint/no-unsafe-call
      expect(isWipTitle('wip: Fix issue')).toBe(true)
      // eslint-disable-next-line @typescript-eslint/no-unsafe-call
      expect(isWipTitle('[WIP] Fix issue')).toBe(true)
      // eslint-disable-next-line @typescript-eslint/no-unsafe-call
      expect(isWipTitle('[wip] Fix issue')).toBe(true)
      // eslint-disable-next-line @typescript-eslint/no-unsafe-call
      expect(isWipTitle('WIP Fix issue')).toBe(true)
      // eslint-disable-next-line @typescript-eslint/no-unsafe-call
      expect(isWipTitle('Fix issue WIP')).toBe(true)
      // eslint-disable-next-line @typescript-eslint/no-unsafe-call
      expect(isWipTitle('Fix wipeable issue')).toBe(false) // Should not match partial words
      // eslint-disable-next-line @typescript-eslint/no-unsafe-call
      expect(isWipTitle('Fix issue')).toBe(false)
    })
  })

  describe('onEdited with WIP removal', () => {
    it('should mention reviewers when WIP is removed from title', async () => {
      const event: PullRequestEditedEvent = {
        action: 'edited',
        number: 123,
        changes: {
          title: {
            from: 'WIP: Fix important bug',
          },
        },
        pull_request: {
          id: 1,
          number: 123,
          title: 'Fix important bug',
          html_url: 'https://github.com/owner/repo/pull/123',
          requested_reviewers: [
            {
              id: 1,
              login: 'reviewer1',
              type: 'User',
            },
            {
              id: 2,
              login: 'reviewer2',
              type: 'User',
            },
          ],
        } as any,
        sender: {
          id: 99,
          login: 'author',
          type: 'User',
        } as any,
        repository: {
          full_name: 'owner/repo',
        } as any,
      }

      const mockDiscord = {} as Discord
      const action = new PullRequestAction(mockDiscord, 'pull_request', event)
      await action.run()

      // Should call getUsersMentions for reviewers
      expect(mockGetUsersMentions).toHaveBeenCalledWith(
        event.sender,
        event.pull_request.requested_reviewers
      )

      // Should send message with mentions
      expect(mockSendMessage).toHaveBeenCalledWith(
        'owner/repo#123-edited',
        expect.objectContaining({
          content: '@reviewer1 @reviewer2',
          embeds: expect.any(Array),
        })
      )
    })

    it('should not mention reviewers when title changes but WIP is not removed', async () => {
      const event: PullRequestEditedEvent = {
        action: 'edited',
        number: 123,
        changes: {
          title: {
            from: 'Fix important bug',
          },
        },
        pull_request: {
          id: 1,
          number: 123,
          title: 'Fix really important bug',
          html_url: 'https://github.com/owner/repo/pull/123',
          requested_reviewers: [
            {
              id: 1,
              login: 'reviewer1',
              type: 'User',
            },
          ],
        } as any,
        sender: {
          id: 99,
          login: 'author',
          type: 'User',
        } as any,
        repository: {
          full_name: 'owner/repo',
        } as any,
      }

      const mockDiscord = {} as Discord
      const action = new PullRequestAction(mockDiscord, 'pull_request', event)
      await action.run()

      // Should not call getUsersMentions
      expect(mockGetUsersMentions).not.toHaveBeenCalled()

      // Should send message without mentions
      expect(mockSendMessage).toHaveBeenCalledWith(
        'owner/repo#123-edited',
        expect.objectContaining({
          content: undefined,
          embeds: expect.any(Array),
        })
      )
    })

    it('should not mention reviewers when WIP is added to title', async () => {
      const event: PullRequestEditedEvent = {
        action: 'edited',
        number: 123,
        changes: {
          title: {
            from: 'Fix important bug',
          },
        },
        pull_request: {
          id: 1,
          number: 123,
          title: 'WIP: Fix important bug',
          html_url: 'https://github.com/owner/repo/pull/123',
          requested_reviewers: [
            {
              id: 1,
              login: 'reviewer1',
              type: 'User',
            },
          ],
        } as any,
        sender: {
          id: 99,
          login: 'author',
          type: 'User',
        } as any,
        repository: {
          full_name: 'owner/repo',
        } as any,
      }

      const mockDiscord = {} as Discord
      const action = new PullRequestAction(mockDiscord, 'pull_request', event)
      await action.run()

      // Should not call getUsersMentions
      expect(mockGetUsersMentions).not.toHaveBeenCalled()

      // Should send message without mentions
      expect(mockSendMessage).toHaveBeenCalledWith(
        'owner/repo#123-edited',
        expect.objectContaining({
          content: undefined,
          embeds: expect.any(Array),
        })
      )
    })
  })
})
