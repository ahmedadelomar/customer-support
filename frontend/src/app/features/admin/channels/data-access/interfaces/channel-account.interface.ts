export interface ChannelAccount {
  id: string;
  channelId: string;
  channelKey: number;
  channelNameEn: string;
  channelNameAr: string;
  name: string;
  identifier: string;
  defaultDepartmentId: string | null;
  defaultCategoryId: string | null;
  defaultPriorityId: string | null;
  signatureEn: string;
  signatureAr: string;
  autoReplyBodyEn: string;
  autoReplyBodyAr: string;
  sendAutoReply: boolean;
  settingsJson: string | null;
  isActive: boolean;
  lastPolledAt: string | null;
  lastPollError: string | null;
  hasWebhookSecret: boolean;
}

export interface ChannelAccountRequest {
  channelId: string;
  name: string;
  identifier: string;
  defaultDepartmentId: string | null;
  defaultCategoryId: string | null;
  defaultPriorityId: string | null;
  signatureEn: string;
  signatureAr: string;
  autoReplyBodyEn: string;
  autoReplyBodyAr: string;
  sendAutoReply: boolean;
  settingsJson: string | null;
  isActive: boolean;
  /** Write-only. Omit to leave unchanged; an empty string clears it. */
  webhookSecret?: string;
}

export interface TestChannelAccountConnectionResult {
  success: boolean;
  error: string | null;
}

/** A lookup entry for the channel picker — every channel this table can hold an account for. */
export interface ChannelLookup {
  id: string;
  key: number;
  nameEn: string;
  nameAr: string;
}
