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

export type RentSyncStatus = 'paid' | 'overdue' | 'evictable' | 'empty';
export interface RentSyncRecord {
  rowNumber: number;
  status: RentSyncStatus;
  paidThrough?: string;
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
