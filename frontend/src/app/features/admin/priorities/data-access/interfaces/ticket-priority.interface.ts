/** Mirrors `TicketPriorityAdminDto` — includes inactive rows and usage counts for the admin editor. */
export interface TicketPriorityAdmin {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  level: number;
  colorHex: string;
  icon: string | null;
  isDefault: boolean;
  isActive: boolean;
  ticketCount: number;
  slaTargetCount: number;
}

export interface CreateTicketPriorityRequest {
  code: string;
  nameEn: string;
  nameAr: string;
  colorHex: string;
  icon?: string;
  isDefault: boolean;
}

export interface UpdateTicketPriorityRequest {
  id: string;
  nameEn: string;
  nameAr: string;
  colorHex: string;
  icon?: string;
  isDefault: boolean;
  isActive: boolean;
}
