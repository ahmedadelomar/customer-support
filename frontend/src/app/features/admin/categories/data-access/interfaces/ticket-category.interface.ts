/** Mirrors `TicketCategoryAdminDto` — includes inactive rows and admin-only fields, unlike the ticket-creation lookup. */
export interface TicketCategoryAdmin {
  id: string;
  parentId: string | null;
  code: string;
  nameEn: string;
  nameAr: string;
  description: string | null;
  path: string;
  depth: number;
  displayOrder: number;
  defaultPriorityId: string | null;
  defaultDepartmentId: string | null;
  defaultSlaPolicyId: string | null;
  isVisibleInPortal: boolean;
  isActive: boolean;
  ticketCount: number;
}

/** A `TicketCategoryAdmin` plus its children, built client-side from the flat, path-ordered list. */
export interface TicketCategoryNode extends TicketCategoryAdmin {
  children: TicketCategoryNode[];
}

export interface CreateTicketCategoryRequest {
  parentId?: string;
  code: string;
  nameEn: string;
  nameAr: string;
  description?: string;
  displayOrder: number;
  defaultPriorityId?: string;
  defaultDepartmentId?: string;
  defaultSlaPolicyId?: string;
  isVisibleInPortal: boolean;
}

export interface UpdateTicketCategoryRequest {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  description?: string;
  displayOrder: number;
  defaultPriorityId?: string;
  defaultDepartmentId?: string;
  defaultSlaPolicyId?: string;
  isVisibleInPortal: boolean;
}

/** Minimal picker option, reused for both priority and department defaults. */
export interface CategoryDefaultOption {
  id: string;
  nameEn: string;
  nameAr: string;
}
