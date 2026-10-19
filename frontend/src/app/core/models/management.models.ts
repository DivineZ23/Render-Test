import { PropertyStatus } from './property.models';

export type EnquiryStatus =
  'new' | 'contacted' | 'viewingScheduled' | 'booked' | 'closed' | 'rejected';
export interface Enquiry {
  id: string;
  propertyId: string;
  propertyName: string;
  fullName: string;
  phoneNumber: string;
  email?: string;
  discordUsername?: string;
  message?: string;
  preferredContactMethod?: string;
  status: EnquiryStatus;
  assignedAgentId?: string;
  internalNotes?: string;
  createdAt: string;
  updatedAt: string;
}
export interface CreateEnquiryRequest {
  propertyId: string;
  fullName: string;
  phoneNumber: string;
  email?: string;
  discordUsername?: string;
  message?: string;
  preferredContactMethod?: string;
}
export type RecruitmentStatus = 'pending' | 'accepted' | 'rejected';
export interface RecruitmentSettings {
  isEnabled: boolean;
}
export interface RecruitmentApplication {
  id: string;
  characterName: string;
  characterCid: number;
  characterPhoneNumber: string;
  discordId: string;
  reasonToJoin: string;
  totalPlaytime: string;
  beneficialSkills: string;
  availability: string;
  status: RecruitmentStatus;
  reviewedByUserId?: string;
  reviewedByDisplayName?: string;
  reviewedAt?: string;
  reviewNotes?: string;
  createdAt: string;
  updatedAt: string;
}
export interface CreateRecruitmentApplicationRequest {
  characterName: string;
  characterCid: number;
  characterPhoneNumber: string;
  discordId: string;
  reasonToJoin: string;
  totalPlaytime: string;
  beneficialSkills: string;
  availability: string;
}
export interface Tenant {
  id: string;
  cid?: number;
  fullName: string;
  phoneNumber: string;
  discordId: string;
  propertyCount: number;
  totalRent: number;
  status: string;
}
export interface AuditLog {
  id: string;
  action: string;
  actionLabel: string;
  category: 'access' | 'portfolio' | 'finance' | 'operations' | string;
  severity: 'info' | 'success' | 'warning' | 'danger' | string;
  summary: string;
  entityType: string;
  entityTypeLabel: string;
  entityId: string;
  entityDisplayName: string;
  performedByUserId: string;
  performedByDisplayName: string;
  performedByAvatarUrl?: string;
  performedByRole?: string;
  previousValues?: Record<string, unknown>;
  newValues?: Record<string, unknown>;
  metadata?: Record<string, unknown>;
  createdAt: string;
}
export interface AuditLogQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  category?: string;
  entityType?: string;
  actorId?: string;
  from?: string;
  to?: string;
  sortDirection?: 'asc' | 'desc';
}
export interface StatusHistory {
  id: string;
  previousStatus: PropertyStatus;
  newStatus: PropertyStatus;
  reason?: string;
  changedByUserId: string;
  createdAt: string;
}
export interface DashboardSummary {
  totalBlocks: number;
  totalProperties: number;
  availableProperties: number;
  bookedProperties: number;
  occupiedProperties: number;
  totalRevenue: number;
  totalCost: number;
  totalProfit: number;
  averageProfitPerProperty: number;
  mostProfitableBlock?: string;
  mostProfitableBlockProfit: number;
  pendingEnquiries: number;
  pendingUsers: number;
  recentStatusChanges: StatusHistory[];
}
export interface PersonalActivity {
  id: string;
  propertyId: string;
  propertyName: string;
  tenantName: string;
  cid?: number;
  amount: number;
  occurredAt: string;
}
export interface PersonalStatistics {
  housesSold: number;
  housesEvicted: number;
  totalDepositTaken: number;
  recentSales: PersonalActivity[];
  recentEvictions: PersonalActivity[];
}

export interface AgentPerformance {
  userId: string;
  displayName: string;
  fullName?: string;
  avatarUrl?: string;
  role: 'agent' | 'seniorAgent' | 'manager';
  bookings: number;
  bookingAmount: number;
  convertedBookings: number;
  housesSold: number;
  commissionTaken: number;
  commissionOwed: number;
  housesEvicted: number;
  noticesSent: number;
  conversionRate: number;
  performanceScore: number;
}

export interface AgentAnalyticsTotals {
  agents: number;
  bookings: number;
  bookingAmount: number;
  housesSold: number;
  commissionTaken: number;
  commissionOwed: number;
  housesEvicted: number;
  noticesSent: number;
}

export interface AgentAnalytics {
  from?: string;
  to?: string;
  totals: AgentAnalyticsTotals;
  agents: AgentPerformance[];
}

export interface AgentBookingActivity {
  id: string;
  propertyId: string;
  propertyName: string;
  clientName: string;
  bookingAmount: number;
  monthlyRent: number;
  status: 'active' | 'cancelled' | 'converted';
  createdAt: string;
}

export interface AgentSaleActivity {
  id: string;
  propertyId: string;
  propertyName: string;
  tenantName: string;
  monthlyRent: number;
  securityDeposit: number;
  soldAt: string;
}

export interface AgentCommissionActivity {
  id: string;
  auctionReference: string;
  amount: number;
  isPaid: boolean;
  isWinningAgent: boolean;
  createdAt: string;
  paidAt?: string;
}

export interface AgentEvictionActivity {
  id: string;
  propertyId: string;
  propertyName: string;
  tenantName: string;
  reason: string;
  evictedAt: string;
}

export interface AgentNoticeActivity {
  id: string;
  address: string;
  status: 'overdue' | 'evictable';
  renterName?: string;
  cid?: number;
  rent: number;
  sentAt: string;
}

export interface AgentAnalyticsDetail {
  from?: string;
  to?: string;
  summary: AgentPerformance;
  bookings: AgentBookingActivity[];
  sales: AgentSaleActivity[];
  commissions: AgentCommissionActivity[];
  evictions: AgentEvictionActivity[];
  notices: AgentNoticeActivity[];
}

export type RentSyncStatus = 'paid' | 'overdue' | 'evictable' | 'empty';
export interface NoticeFeedItem {
  snapshotId: string;
  syncedAt: string;
  record: RentSyncRecord;
}
export interface NoticeFeed {
  asOf: string;
  items: NoticeFeedItem[];
  hasMore: boolean;
  nextSkip: number;
}
export interface NoticeFeedSummary {
  syncedAt?: string;
  currentTotal: number;
  totalNotices: number;
  pending: number;
  sentToday: number;
}
export interface RentRecordPage {
  snapshot: RentSyncSnapshot;
  hasMore: boolean;
  nextSkip: number;
}
export interface RentSyncRecord {
  rowNumber: number;
  status: RentSyncStatus;
  paidThrough?: string;
  lastPaid?: string;
  nextCharge?: string;
  address: string;
  interior: string;
  cid?: number;
  renterName?: string;
  phone?: string;
  income: number;
  cost: number;
  tenantId?: string;
  discordId?: string;
  tenantMatched: boolean;
  overdueNotice?: string;
  evictionNotice?: string;
  isResolved: boolean;
  resolvedByUserId?: string;
  resolvedByDisplayName?: string;
  resolvedAt?: string;
}
export interface RentSyncSnapshot {
  id: string;
  createdBy?: string;
  syncedAt?: string;
  total: number;
  active: number;
  overdue: number;
  evictable: number;
  empty: number;
  unmappedTenants: number;
  googleSheetSyncStatus: 'notConfigured' | 'ready' | 'pending' | 'synced' | 'failed';
  googleSheetSyncedAt?: string;
  googleSheetSyncError?: string;
  googleSheetUrl?: string;
  records: RentSyncRecord[];
}
export interface EvictionHistory {
  id: string;
  propertyId: string;
  propertyBusinessId?: number;
  propertyName?: string;
  tenantName: string;
  cid?: number;
  phoneNumber: string;
  monthlyRent: number;
  reason: string;
  storageImageUrls: string[];
  evictedByUserId?: string;
  evictedByDisplayName?: string;
  evictedAt: string;
}
export interface EvictionQueueItem {
  propertyId: string;
  propertyBusinessId: number;
  propertyName: string;
  noticeSnapshotId: string;
  noticeRowNumber: number;
  tenantName?: string;
  cid?: number;
  phone?: string;
  discordId?: string;
  rent: number;
  noticeSentAt: string;
  eligibleAt: string;
  isReady: boolean;
  isOnHold: boolean;
  heldByUserId?: string;
  heldByDisplayName?: string;
  heldAt?: string;
}
