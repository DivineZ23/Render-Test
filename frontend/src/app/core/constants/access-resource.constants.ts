import { AccessManagementSettings, UserRole } from '../models/user.models';

export type AccessTier = 'all' | 'seniorAgent' | 'manager' | 'owner';

export interface AccessResourceDefinition {
  key: string;
  label: string;
  tier: AccessTier;
  child?: boolean;
}

export interface AccessSectionDefinition {
  key: string;
  label: string;
  resources: AccessResourceDefinition[];
}

export const ACCESS_SECTIONS: AccessSectionDefinition[] = [
  {
    key: 'workspace',
    label: 'Workspace',
    resources: [
      { key: 'overview', label: 'Overview', tier: 'all' },
      { key: 'team', label: 'Team', tier: 'all' },
      { key: 'profile', label: 'My Profile', tier: 'all' },
    ],
  },
  {
    key: 'performance',
    label: 'Performance',
    resources: [
      { key: 'analytics', label: 'Personal Analytics', tier: 'all' },
      { key: 'agentAnalytics', label: 'Agent Analytics', tier: 'manager' },
    ],
  },
  {
    key: 'finance',
    label: 'Finance',
    resources: [{ key: 'commissions', label: 'Commissions', tier: 'all' }],
  },
  {
    key: 'auction',
    label: 'Auction',
    resources: [
      { key: 'auction.createListing', label: 'Create Listing', tier: 'all' },
      { key: 'auction.listings', label: 'Listings', tier: 'all' },
    ],
  },
  {
    key: 'portfolio',
    label: 'Portfolio',
    resources: [
      { key: 'portfolio.properties', label: 'Properties', tier: 'all' },
      {
        key: 'portfolio.properties.add',
        label: 'Add Property',
        tier: 'manager',
        child: true,
      },
      {
        key: 'portfolio.properties.edit',
        label: 'Edit Property',
        tier: 'manager',
        child: true,
      },
      {
        key: 'portfolio.properties.sell',
        label: 'Sell Property',
        tier: 'all',
        child: true,
      },
      {
        key: 'portfolio.properties.book',
        label: 'Book Property',
        tier: 'all',
        child: true,
      },
      {
        key: 'portfolio.properties.evict',
        label: 'Evict Tenant',
        tier: 'seniorAgent',
        child: true,
      },
      { key: 'portfolio.blocks', label: 'Blocks', tier: 'all' },
      { key: 'portfolio.tenants', label: 'Tenants', tier: 'all' },
      { key: 'portfolio.bookings', label: 'Bookings', tier: 'all' },
    ],
  },
  {
    key: 'notices',
    label: 'Notices',
    resources: [
      { key: 'notices.overdue', label: 'Overdue Notice', tier: 'all' },
      { key: 'notices.eviction', label: 'Eviction Notice', tier: 'all' },
      { key: 'notices.overdueList', label: 'Overdue List', tier: 'all' },
      { key: 'notices.evictionQueue', label: 'Eviction Queue', tier: 'all' },
      { key: 'notices.evictionHistory', label: 'Eviction History', tier: 'all' },
      { key: 'notices.sync', label: 'Data Sync', tier: 'manager' },
      { key: 'notices.syncedDataRecords', label: 'Sync History', tier: 'all' },
    ],
  },
  {
    key: 'recruitment',
    label: 'Recruitment',
    resources: [
      { key: 'recruitment.pending', label: 'Pending Applications', tier: 'manager' },
      { key: 'recruitment.accepted', label: 'Accepted Applications', tier: 'manager' },
      { key: 'recruitment.rejected', label: 'Rejected Applications', tier: 'manager' },
    ],
  },
  {
    key: 'administration',
    label: 'Administration',
    resources: [
      { key: 'administration.users', label: 'User Management', tier: 'manager' },
      { key: 'administration.auditLogs', label: 'Audit Logs', tier: 'manager' },
      { key: 'administration.settings', label: 'Settings', tier: 'manager' },
      { key: 'administration.accessManagement', label: 'Access Management', tier: 'owner' },
    ],
  },
];

const ROLE_LEVEL: Record<UserRole, number> = {
  stateManager: -1,
  agent: 0,
  seniorAgent: 1,
  manager: 2,
  owner: 3,
};

const TIER_LEVEL: Record<AccessTier, number> = {
  all: 0,
  seniorAgent: 1,
  manager: 2,
  owner: 3,
};

export function roleCanOpen(resource: AccessResourceDefinition, role: UserRole): boolean {
  if (role === 'stateManager') return !resource.child && resource.key !== 'auction.createListing';
  return ROLE_LEVEL[role] >= TIER_LEVEL[resource.tier];
}

export function defaultAccessSettings(): AccessManagementSettings {
  const permissions: AccessManagementSettings['permissions'] = {};
  for (const section of ACCESS_SECTIONS) {
    for (const resource of section.resources) {
      permissions[resource.key] = {
        stateManager: false,
        agent: roleCanOpen(resource, 'agent'),
        seniorAgent: roleCanOpen(resource, 'seniorAgent'),
        manager: roleCanOpen(resource, 'manager'),
        owner: true,
      };
    }
  }
  return { permissions };
}
