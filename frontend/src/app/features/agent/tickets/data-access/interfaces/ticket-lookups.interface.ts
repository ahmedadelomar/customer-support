import type { TicketStatusKind } from '../../../../../core/models/enums';

export interface TicketCategoryLookup {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  parentId: string | null;
  depth: number;
  path: string;
  defaultPriorityId: string | null;
  defaultDepartmentId: string | null;
}

export interface TicketPriorityLookup {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  level: number;
  colorHex: string;
  isDefault: boolean;
}

export interface TicketStatusLookup {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  kind: TicketStatusKind;
  colorHex: string;
  isTerminal: boolean;
  isDefault: boolean;
}

export interface DepartmentLookup {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
}

export interface TicketLookups {
  categories: TicketCategoryLookup[];
  priorities: TicketPriorityLookup[];
  statuses: TicketStatusLookup[];
  departments: DepartmentLookup[];
}
