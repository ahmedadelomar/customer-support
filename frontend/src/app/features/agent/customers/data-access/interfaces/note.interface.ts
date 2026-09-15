import type { Attachment } from '../../../../../shared/ui/file-upload/attachment.models';

/** Mirrors `CustomerNoteDto`. */
export interface CustomerNote {
  id: string;
  customerId: string;
  ticketId?: string;
  body: string;
  isPinned: boolean;
  isInternal: boolean;
  createdById?: string;
  authorNameEn?: string;
  authorNameAr?: string;
  createdAt: string;
  modifiedAt?: string;
  /** Resolved server-side: true when the caller is the author or holds `customers.notes.manage`. */
  canEdit: boolean;
  attachments: Attachment[];
}

/** Request body for adding a note; matches `CreateCustomerNoteCommand`. */
export interface CreateNoteRequest {
  body: string;
  isInternal: boolean;
  ticketId?: string;
}

/** Request body for editing a note; matches `UpdateCustomerNoteCommand`. */
export interface UpdateNoteRequest {
  body: string;
}
