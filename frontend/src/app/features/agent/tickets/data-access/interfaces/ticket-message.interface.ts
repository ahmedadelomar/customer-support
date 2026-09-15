import type { ChannelKey, MessageAuthorType, MessageDirection } from '../../../../../core/models/enums';
import type { Attachment } from '../../../../../shared/ui/file-upload/attachment.models';

export interface TicketMessage {
  id: string;
  ticketId: string;
  channel: ChannelKey;
  direction: MessageDirection;
  authorType: MessageAuthorType;
  authorId: string | null;
  authorDisplayName: string | null;
  subject: string | null;
  bodyText: string;
  bodyHtml: string | null;
  isInternalNote: boolean;
  sentAt: string;
  attachments: Attachment[];
}
