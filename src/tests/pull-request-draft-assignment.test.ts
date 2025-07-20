import { PullRequestAction } from '../actions/pull-request'
import { PullRequestAssignedEvent } from '@octokit/webhooks-types'
import * as utils from '../utils'
import { Discord } from '@book000/node-utils'

describe('PullRequest Draft Assignment functionality', () => {
  let mockSendMessage: jest.SpyInstance
  let mockGetUsersMentions: jest.SpyInstance

  beforeEach(() => {
    mockSendMessage = jest
      .spyOn(PullRequestAction.prototype as any, 'sendMessage')
      .mockImplementation(() => Promise.resolve())

    mockGetUsersMentions = jest
      .spyOn(utils, 'getUsersMentions')
      .mockImplementation(() => Promise.resolve('@assignee1'))
  })

  afterEach(() => {
    jest.restoreAllMocks()
  })

  describe('onAssigned for draft PRs', () => {
    it('should not mention assignee when assigning to draft PR', async () => {
      const event: PullRequestAssignedEvent = {
        action: 'assigned',
        number: 123,
        assignee: {
          id: 1,
          login: 'assignee1',
          type: 'User',
        } as any,
        pull_request: {
          id: 1,
          number: 123,
          title: 'Draft: Fix important bug',
          draft: true,
          html_url: 'https://github.com/owner/repo/pull/123',
          head: {
            ref: 'feature-branch',
          },
          assignees: [
            {
              id: 1,
              login: 'assignee1',
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

      // Should not call getUsersMentions for draft PRs
      expect(mockGetUsersMentions).not.toHaveBeenCalled()

      // Should send message without mentions
      expect(mockSendMessage).toHaveBeenCalledWith(
        'owner/repo#123-assigned',
        expect.objectContaining({
          content: undefined,
          embeds: expect.any(Array),
        })
      )
    })

    it('should mention assignee when assigning to non-draft PR', async () => {
      const event: PullRequestAssignedEvent = {
        action: 'assigned',
        number: 123,
        assignee: {
          id: 1,
          login: 'assignee1',
          type: 'User',
        } as any,
        pull_request: {
          id: 1,
          number: 123,
          title: 'Fix important bug',
          draft: false,
          html_url: 'https://github.com/owner/repo/pull/123',
          head: {
            ref: 'feature-branch',
          },
          assignees: [
            {
              id: 1,
              login: 'assignee1',
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

      // Should call getUsersMentions for non-draft PRs
      expect(mockGetUsersMentions).toHaveBeenCalledWith(event.sender, [
        event.assignee,
      ])

      // Should send message with mentions
      expect(mockSendMessage).toHaveBeenCalledWith(
        'owner/repo#123-assigned',
        expect.objectContaining({
          content: '@assignee1',
          embeds: expect.any(Array),
        })
      )
    })
  })
})
