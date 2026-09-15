export interface SavedTicketView {
  id: string;
  nameEn: string;
  nameAr: string;
  filtersJson: string;
  displayOrder: number;
  isShared: boolean;
}

export interface CreateSavedTicketViewRequest {
  nameEn: string;
  nameAr: string;
  filtersJson: string;
  isShared?: boolean;
}
