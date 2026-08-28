/**
 * Numeric enums mirroring `CustomerSupport.Domain.Enums`. The API serialises enums as numbers,
 * so these must stay in lockstep with the C# definitions — the ordinal is the contract.
 */

export enum TicketStatusKind {
  New = 0,
  Open = 1,
  Pending = 2,
  OnHold = 3,
  Resolved = 4,
  Closed = 5,
  Cancelled = 6,
}

export enum ChannelKey {
  Email = 0,
  WhatsApp = 1,
  LiveChat = 2,
  Sms = 3,
  WebForm = 4,
  Portal = 5,
  Phone = 6,
  Api = 7,
  Internal = 8,
}

export enum CustomerType {
  Individual = 0,
  Company = 1,
  Government = 2,
}

export enum ContactType {
  Email = 0,
  Mobile = 1,
  Phone = 2,
  WhatsApp = 3,
  Address = 4,
  Website = 5,
  Other = 6,
}

export enum MessageDirection {
  Inbound = 0,
  Outbound = 1,
}

export enum MessageAuthorType {
  Customer = 0,
  Agent = 1,
  System = 2,
  Bot = 3,
}

export enum AgentTaskStatus {
  Pending = 0,
  InProgress = 1,
  Completed = 2,
  Cancelled = 3,
}

export enum SlaTargetType {
  FirstResponse = 0,
  Resolution = 1,
}

export enum AiSuggestionType {
  TicketSummary = 0,
  SuggestedReply = 1,
  Categorization = 2,
  SuggestedSolution = 3,
  SentimentAnalysis = 4,
}

export enum AiSuggestionStatus {
  Pending = 0,
  Accepted = 1,
  Edited = 2,
  Rejected = 3,
  Expired = 4,
}

/** Translation key for an enum member, e.g. `enums.channel.1` for WhatsApp. */
export function enumTranslationKey(group: string, value: number): string {
  return `enums.${group}.${value}`;
}
