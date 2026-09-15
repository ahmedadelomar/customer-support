export interface AgentDashboardTiles {
  assigned: number;
  dueToday: number;
  approachingSla: number;
  breached: number;
}

export interface AgentDashboardStats {
  resolvedThisWeek: number;
  teamAverageResolvedThisWeek: number;
}

/** One row of the next-up queue — always assigned to the caller (see the backend query's own filter). */
export interface AgentQueueItem {
  id: string;
  number: string;
  subject: string;
  customerId: string;
  customerDisplayNameEn: string;
  customerDisplayNameAr: string;
  priorityId: string;
  priorityNameEn: string;
  priorityNameAr: string;
  priorityColorHex: string;
  resolutionDueAt: string | null;
  isFirstResponseBreached: boolean;
  isResolutionBreached: boolean;
  createdAt: string;
}

export interface AgentDashboard {
  canViewQueue: boolean;
  canViewStats: boolean;
  tiles: AgentDashboardTiles;
  queue: AgentQueueItem[];
  stats: AgentDashboardStats;
}
