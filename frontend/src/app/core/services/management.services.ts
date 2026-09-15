import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_ENDPOINTS } from '../config/api-endpoints';
import { PagedResult } from '../models/api.models';
import {
  AuditLog,
  CreateEnquiryRequest,
  CreateRecruitmentApplicationRequest,
  DashboardSummary,
  PersonalStatistics,
  Enquiry,
  EnquiryStatus,
  RecruitmentApplication,
  RecruitmentSettings,
  RecruitmentStatus,
  RentSyncSnapshot,
  EvictionHistory,
  EvictionQueueItem,
  Tenant,
} from '../models/management.models';
import { Block, UpsertBlockRequest } from '../models/property.models';
import {
  AccessStatus,
  AccessManagementSettings,
  AgentSummary,
  ApprovalStatus,
  TeamMember,
  User,
  UserRole,
} from '../models/user.models';
import { ApiService } from './api.service';
import {
  CommissionOverview,
  CommissionRecord,
  AuctionCommissionCalculation,
  CreateAuctionSettlement,
} from '../models/commission.models';

@Injectable({ providedIn: 'root' })
export class BlockService {
  private readonly api = inject(ApiService);
  public(): Observable<Block[]> {
    return this.api.get(API_ENDPOINTS.blocks.public);
  }
  all(): Observable<Block[]> {
    return this.api.get(API_ENDPOINTS.blocks.root);
  }
  create(body: UpsertBlockRequest): Observable<Block> {
    return this.api.post(API_ENDPOINTS.blocks.root, body);
  }
  update(id: string, body: UpsertBlockRequest): Observable<Block> {
    return this.api.put(`${API_ENDPOINTS.blocks.root}/${id}`, body);
  }
  delete(id: string): Observable<void> {
    return this.api.delete(`${API_ENDPOINTS.blocks.root}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class EnquiryService {
  private readonly api = inject(ApiService);
  create(body: CreateEnquiryRequest): Observable<Enquiry> {
    return this.api.post(API_ENDPOINTS.enquiries, body);
  }
  all(page = 1, status?: EnquiryStatus): Observable<PagedResult<Enquiry>> {
    return this.api.get(API_ENDPOINTS.enquiries, { page, pageSize: 20, status });
  }
  update(
    id: string,
    body: { status?: EnquiryStatus; assignedAgentId?: string; internalNotes?: string },
  ): Observable<Enquiry> {
    return this.api.patch(`${API_ENDPOINTS.enquiries}/${id}`, body);
  }
}

@Injectable({ providedIn: 'root' })
export class RecruitmentService {
  private readonly api = inject(ApiService);
  create(body: CreateRecruitmentApplicationRequest): Observable<RecruitmentApplication> {
    return this.api.post(API_ENDPOINTS.recruitmentApplications, body);
  }
  settings(): Observable<RecruitmentSettings> {
    return this.api.get(API_ENDPOINTS.recruitmentSettings);
  }
  updateSettings(isEnabled: boolean): Observable<RecruitmentSettings> {
    return this.api.put(API_ENDPOINTS.recruitmentSettings, { isEnabled });
  }
  all(status: RecruitmentStatus, page = 1): Observable<PagedResult<RecruitmentApplication>> {
    return this.api.get(API_ENDPOINTS.recruitmentApplications, { page, pageSize: 50, status });
  }
  review(
    id: string,
    status: RecruitmentStatus,
    reviewNotes?: string,
  ): Observable<RecruitmentApplication> {
    return this.api.patch(`${API_ENDPOINTS.recruitmentApplications}/${id}`, {
      status,
      reviewNotes,
    });
  }
}

@Injectable({ providedIn: 'root' })
export class TeamService {
  private readonly api = inject(ApiService);
  agents(): Observable<AgentSummary[]> {
    return this.api.get(API_ENDPOINTS.teamAgents);
  }
}

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly api = inject(ApiService);
  team(): Observable<TeamMember[]> {
    return this.api.get(API_ENDPOINTS.settingsTeam);
  }
  updateTeam(members: TeamMember[]): Observable<TeamMember[]> {
    return this.api.put(API_ENDPOINTS.settingsTeam, members);
  }
}

@Injectable({ providedIn: 'root' })
export class AccessManagementService {
  private readonly api = inject(ApiService);
  get(): Observable<AccessManagementSettings> {
    return this.api.get(API_ENDPOINTS.accessManagement);
  }
  update(settings: AccessManagementSettings): Observable<AccessManagementSettings> {
    return this.api.put(API_ENDPOINTS.accessManagement, settings);
  }
}

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly api = inject(ApiService);
  all(
    filters: { approval?: ApprovalStatus; access?: AccessStatus; role?: UserRole } = {},
  ): Observable<PagedResult<User>> {
    return this.api.get(API_ENDPOINTS.users, { page: 1, pageSize: 100, ...filters });
  }
  action(
    id: string,
    action: 'approve' | 'reject' | 'promote' | 'demote' | 'revoke' | 'restore',
    reason?: string,
  ): Observable<User> {
    return this.api.post(`${API_ENDPOINTS.users}/${id}/${action}`, { reason });
  }
  delete(id: string, reason: string): Observable<void> {
    return this.api.delete(`${API_ENDPOINTS.users}/${id}`, { reason });
  }
}

@Injectable({ providedIn: 'root' })
export class CommissionService {
  private readonly api = inject(ApiService);
  overview(): Observable<CommissionOverview> {
    return this.api.get(API_ENDPOINTS.commissions);
  }
  preview(finalAuctionPrice: number, basePrice: number, totalNumberOfAgents: number, winningAgentCount: number): Observable<AuctionCommissionCalculation> {
    return this.api.post(`${API_ENDPOINTS.commissions}/preview`, { finalAuctionPrice, basePrice, totalNumberOfAgents, winningAgentCount });
  }
  createSettlement(value: CreateAuctionSettlement): Observable<CommissionRecord[]> {
    return this.api.post(`${API_ENDPOINTS.commissions}/settlements`, value);
  }
  updateSettlement(id: string, value: CreateAuctionSettlement): Observable<CommissionRecord[]> {
    return this.api.put(`${API_ENDPOINTS.commissions}/settlements/${id}`, value);
  }
  deleteSettlement(id: string): Observable<void> {
    return this.api.delete(`${API_ENDPOINTS.commissions}/settlements/${id}`);
  }
  setPaid(id: string, isPaid: boolean): Observable<CommissionRecord> {
    return this.api.patch(`${API_ENDPOINTS.commissions}/${id}/paid`, { isPaid });
  }
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly api = inject(ApiService);
  get(): Observable<DashboardSummary> {
    return this.api.get(API_ENDPOINTS.dashboard);
  }
  personal(): Observable<PersonalStatistics> {
    return this.api.get(API_ENDPOINTS.personalStatistics);
  }
}
@Injectable({ providedIn: 'root' })
export class TenantService {
  private readonly api = inject(ApiService);
  all(): Observable<PagedResult<Tenant>> {
    return this.api.get(API_ENDPOINTS.tenants, { page: 1, pageSize: 100 });
  }
  evictions(): Observable<EvictionHistory[]> {
    return this.api.get(API_ENDPOINTS.evictions);
  }
}
@Injectable({ providedIn: 'root' })
export class NoticeService {
  private readonly api = inject(ApiService);
  snapshot(): Observable<RentSyncSnapshot> {
    return this.api.get(API_ENDPOINTS.notices.snapshot);
  }
  snapshots(): Observable<RentSyncSnapshot[]> {
    return this.api.get(API_ENDPOINTS.notices.snapshots);
  }
  evictionQueue(): Observable<EvictionQueueItem[]> {
    return this.api.get(API_ENDPOINTS.notices.evictionQueue);
  }
  setEvictionHold(snapshotId: string, rowNumber: number, isOnHold: boolean): Observable<void> {
    return this.api.patch(
      `${API_ENDPOINTS.notices.evictionQueue}/${snapshotId}/records/${rowNumber}/hold`,
      { isOnHold },
    );
  }
  sync(rawData: string): Observable<RentSyncSnapshot> {
    return this.api.post(API_ENDPOINTS.notices.sync, { rawData });
  }
  retryGoogleSheet(): Observable<RentSyncSnapshot> {
    return this.api.post(API_ENDPOINTS.notices.retryGoogleSheet, {});
  }
  deleteSnapshot(id: string): Observable<void> {
    return this.api.delete<void>(`${API_ENDPOINTS.notices.snapshots}/${id}`);
  }
  setResolution(
    snapshotId: string,
    rowNumber: number,
    isResolved: boolean,
  ): Observable<RentSyncSnapshot> {
    return this.api.patch(
      `${API_ENDPOINTS.notices.snapshots}/${snapshotId}/records/${rowNumber}/resolution`,
      { isResolved },
    );
  }
}
@Injectable({ providedIn: 'root' })
export class AuditService {
  private api = inject(ApiService);
  all(): Observable<PagedResult<AuditLog>> {
    return this.api.get(API_ENDPOINTS.auditLogs, { page: 1, pageSize: 100 });
  }
}
