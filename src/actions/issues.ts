import {
  Issue,
  IssuesEvent,
  IssuesAssignedEvent,
  IssuesClosedEvent,
  IssuesDeletedEvent,
  IssuesDemilestonedEvent,
  IssuesEditedEvent,
  IssuesLabeledEvent,
  IssuesLockedEvent,
  IssuesMilestonedEvent,
  IssuesOpenedEvent,
  IssuesPinnedEvent,
  IssuesReopenedEvent,
  IssuesTransferredEvent,
  IssuesUnassignedEvent,
  IssuesUnlabeledEvent,
  IssuesUnlockedEvent,
  IssuesUnpinnedEvent,
} from '@octokit/webhooks-types'
import { BaseAction } from '.'

export class IssuesAction extends BaseAction<IssuesEvent> {
  public run(): Promise<void> {
    const action = this.event.action

    const methodMap: Record<IssuesEvent['action'], () => Promise<void>> = {
      opened: () => this.onOpened(this.event as IssuesOpenedEvent),
      closed: () => this.onClosed(this.event as IssuesClosedEvent),
      reopened: () => this.onOpened(this.event as IssuesReopenedEvent),
      assigned: () => this.onAssigned(this.event as IssuesAssignedEvent),
      unassigned: () => this.onUnassigned(this.event as IssuesUnassignedEvent),
      labeled: () => this.onLabeled(this.event as IssuesLabeledEvent),
      unlabeled: () => this.onUnlabeled(this.event as IssuesUnlabeledEvent),
      edited: () => this.onEdited(this.event as IssuesEditedEvent),
      locked: () => this.onLocked(this.event as IssuesLockedEvent),
      unlocked: () => this.onUnlocked(this.event as IssuesUnlockedEvent),
      milestoned: () => this.onMilestoned(this.event as IssuesMilestonedEvent),
      demilestoned: () =>
        this.onDemilestoned(this.event as IssuesDemilestonedEvent),
      transferred: () =>
        this.onTransferred(this.event as IssuesTransferredEvent),
      pinned: () => this.onPinned(this.event as IssuesPinnedEvent),
      unpinned: () => this.onUnpinned(this.event as IssuesUnpinnedEvent),
      deleted: () => this.onDeleted(this.event as IssuesDeletedEvent),
    }

    return methodMap[action]()
  }

  /**
   * Issue がオープン・再オープンされたときの処理
   */
  private onOpened(
    event: IssuesOpenedEvent | IssuesReopenedEvent
  ): Promise<void> {
    const issue = event.issue
    const color = 
  }

  /**
   * Issue がクローズされたときの処理
   */
  private onClosed(event: IssuesClosedEvent): Promise<void> {}

  /**
   * Issue がアサインされたときの処理
   */
  private onAssigned(event: IssuesAssignedEvent): Promise<void> {}

  /**
   * Issue のアサインが解除されたときの処理
   */
  private onUnassigned(event: IssuesUnassignedEvent): Promise<void> {}

  /**
   * Issue にラベルが追加されたときの処理
   */
  private onLabeled(event: IssuesLabeledEvent): Promise<void> {}

  /**
   * Issue からラベルが削除されたときの処理
   */
  private onUnlabeled(event: IssuesUnlabeledEvent): Promise<void> {}

  /**
   * Issue が編集されたときの処理
   */
  private onEdited(event: IssuesEditedEvent): Promise<void> {}

  /**
   * Issue がロックされたときの処理
   */
  private onLocked(event: IssuesLockedEvent): Promise<void> {}

  /**
   * Issue のロックが解除されたときの処理
   */
  private onUnlocked(event: IssuesUnlockedEvent): Promise<void> {}

  /**
   * Issue がマイルストーンに追加されたときの処理
   */
  private onMilestoned(event: IssuesMilestonedEvent): Promise<void> {}

  /**
   * Issue がマイルストーンから削除されたときの処理
   */
  private onDemilestoned(event: IssuesDemilestonedEvent): Promise<void> {}

  /**
   * Issue が別のリポジトリに移動されたときの処理
   */
  private onTransferred(event: IssuesTransferredEvent): Promise<void> {}

  /**
   * Issue がピン留めされたときの処理
   */
  private onPinned(event: IssuesPinnedEvent): Promise<void> {}

  /**
   * Issue のピン留めが解除されたときの処理
   */
  private onUnpinned(event: IssuesUnpinnedEvent): Promise<void> {}

  /**
   * Issue が削除されたときの処理
   */
  private onDeleted(event: IssuesDeletedEvent): Promise<void> {}
}
