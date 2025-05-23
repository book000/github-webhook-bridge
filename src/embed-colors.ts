export const EmbedColors = {
  Unknown: 0x00_00_00,
  PullRequestOpened: 0x2e_cc_71, // Green
  PullRequestMerged: 0x00_00_00, // Black
  PullRequestClosed: 0x95_a5_a6, // Grayish Blue
  PullRequestReopened: 0x34_98_db, // Blue
  PullRequestAssigned: 0xf3_9c_12, // Orange
  PullRequestUnassigned: 0xf3_9c_12, // Orange
  PullRequestReviewRequested: 0x9b_59_b6, // Purple
  PullRequestReviewRequestRemoved: 0x9b_59_b6, // Purple
  PullRequestLabeled: 0x34_98_db, // Blue
  PullRequestUnlabeled: 0x34_98_db, // Blue
  PullRequestEdited: 0x34_98_db, // Blue
  PullRequestReadyForReview: 0x2e_cc_71, // Green
  PullRequestLocked: 0x7f_8c_8d, // Gray
  PullRequestUnlocked: 0x7f_8c_8d, // Gray
  PullRequestAutoMergeEnabled: 0x2e_cc_71, // Green
  PullRequestAutoMergeDisabled: 0xe7_4c_3c, // Red
  PullRequestConvertedToDraft: 0x95_a5_a6, // Grayish Blue
  PullRequestDemilestoned: 0x95_a5_a6, // Grayish Blue
  PullRequestMilestoned: 0x34_98_db, // Blue
  PullRequestEnqueued: 0x34_98_db, // Blue
  PullRequestDequeued: 0x34_98_db, // Blue
  PullRequestReviewApproved: 0x2e_cc_71, // Green
  PullRequestReviewChangesRequested: 0xf3_9c_12, // Orange
  PullRequestReviewDismissed: 0xe7_4c_3c, // Red
  PullRequestReviewEdited: 0x34_98_db, // Blue
  PullRequestReviewCommentCreated: 0x2e_cc_71, // Green
  PullRequestReviewCommentEdited: 0x34_98_db, // Blue
  PullRequestReviewCommentDeleted: 0xe7_4c_3c, // Red
  PullRequestReviewThreadResolved: 0x2e_cc_71, // Green
  PullRequestReviewThreadUnresolved: 0xe7_4c_3c, // Red
  IssueOpened: 0x2e_cc_71, // Green
  IssueClosed: 0x95_a5_a6, // Grayish Blue
  IssueReopened: 0x34_98_db, // Blue
  IssueAssigned: 0xf3_9c_12, // Orange
  IssueUnassigned: 0xf3_9c_12, // Orange
  IssueLabeled: 0x34_98_db, // Blue
  IssueUnlabeled: 0x34_98_db, // Blue
  IssueEdited: 0x34_98_db, // Blue
  IssueLocked: 0x7f_8c_8d, // Gray
  IssueUnlocked: 0x7f_8c_8d, // Gray
  IssueMilestoned: 0x34_98_db, // Blue
  IssueDemilestoned: 0x95_a5_a6, // Grayish Blue
  IssueTransferred: 0x95_a5_a6, // Grayish Blue
  IssuePinned: 0x2e_cc_71, // Green
  IssueUnpinned: 0xe7_4c_3c, // Red
  IssueDeleted: 0xe7_4c_3c, // Red
  IssueCommentCreated: 0x34_98_db, // Blue
  IssueCommentEdited: 0x34_98_db, // Blue
  IssueCommentDeleted: 0xe7_4c_3c, // Red
  Star: 0xff_d7_00, // Yellow
  Unstar: 0x9b_59_b6, // Purple
  Fork: 0x2e_cc_71, // Green
  Push: 0x2e_cc_71, // Green
  Ping: 0x95_a5_a6, // Grayish Blue
  Public: 0x2e_cc_71, // Green

  // Discussion Colors
  DiscussionCreated: 0x2e_cc_71, // Green
  DiscussionEdited: 0x34_98_db, // Blue
  DiscussionDeleted: 0xe7_4c_3c, // Red
  DiscussionPinned: 0x2e_cc_71, // Green
  DiscussionUnpinned: 0xe7_4c_3c, // Red
  DiscussionLabeled: 0x34_98_db, // Blue
  DiscussionUnlabeled: 0x34_98_db, // Blue
  DiscussionTransferred: 0x95_a5_a6, // Grayish Blue
  DiscussionCategoryChanged: 0x34_98_db, // Blue
  DiscussionAnswered: 0x2e_cc_71, // Green
  DiscussionUnanswered: 0xe7_4c_3c, // Red
  DiscussionLocked: 0x7f_8c_8d, // Gray
  DiscussionUnlocked: 0x7f_8c_8d, // Gray
} as const
