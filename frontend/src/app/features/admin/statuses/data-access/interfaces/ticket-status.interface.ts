import type { TicketStatusKind } from '../../../../../core/models/enums';

/** Mirrors `TicketStatusAdminDto` — includes inactive rows and a usage count for the admin editor. */
export interface TicketStatusAdmin {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  kind: TicketStatusKind;
  colorHex: string;
  displayOrder: number;
  isTerminal: boolean;
  pausesSla: boolean;
  isDefault: boolean;
  isVisibleInPortal: boolean;
  isActive: boolean;
  ticketCount: number;
}

export interface CreateTicketStatusRequest {
  code: string;
  nameEn: string;
  nameAr: string;
  kind: TicketStatusKind;
  colorHex: string;
  isTerminal: boolean;
  pausesSla: boolean;
  isDefault: boolean;
  isVisibleInPortal: boolean;
}

export interface UpdateTicketStatusRequest {
  id: string;
  nameEn: string;
  nameAr: string;
  kind: TicketStatusKind;
  colorHex: string;
  isTerminal: boolean;
  pausesSla: boolean;
  isDefault: boolean;
  isVisibleInPortal: boolean;
  isActive: boolean;
  force?: boolean;
}
