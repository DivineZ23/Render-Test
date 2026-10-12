export type UserRole = 'agent' | 'seniorAgent' | 'manager' | 'owner' | 'stateManager';

export interface AccessManagementSettings {
  permissions: Record<string, Partial<Record<UserRole, boolean>>>;
}
export type ApprovalStatus = 'pending' | 'approved' | 'rejected';
export type AccessStatus = 'active' | 'pending' | 'revoked';

export interface User {
  id: string;
  discordUserId: string;
  username: string;
  displayName: string;
  avatarUrl?: string;
  email?: string;
  fullName?: string;
  cid?: number;
  phoneNumber?: string;
  role: UserRole;
  approvalStatus: ApprovalStatus;
  accessStatus: AccessStatus;
  approvedBy?: string;
  approvedAt?: string;
  revokedBy?: string;
  revokedAt?: string;
  revokeReason?: string;
  lastLoginAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface UpdateUserProfileRequest {
  fullName: string;
  cid: number;
  phoneNumber: string;
}

export interface AgentSummary {
  id: string;
  displayName: string;
  username: string;
  avatarUrl?: string;
  role: UserRole;
}

export interface TeamMember {
  id: string;
  name: string;
  title: string;
  biography: string;
  imageUrl: string;
}
