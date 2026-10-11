import { Routes } from '@angular/router';
import { authGuard, managerGuard, pageAccessGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'pending-approval',
    loadComponent: () =>
      import('./features/auth/auth-state-pages.component').then((m) => m.PendingApprovalComponent),
  },
  {
    path: 'access-revoked',
    loadComponent: () =>
      import('./features/auth/auth-state-pages.component').then((m) => m.AccessRevokedComponent),
  },
  {
    path: 'auth/callback',
    loadComponent: () =>
      import('./features/auth/auth-state-pages.component').then((m) => m.AuthCallbackComponent),
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layouts/dashboard-layout/dashboard-layout.component').then(
        (m) => m.DashboardLayoutComponent,
      ),
    children: [
      {
        path: '',
        data: { accessKey: 'overview' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/dashboard/dashboard-overview.component').then(
            (m) => m.DashboardOverviewComponent,
          ),
      },
      {
        path: 'team',
        data: { accessKey: 'team' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/team/team-overview.component').then((m) => m.TeamOverviewComponent),
      },
      {
        path: 'analytics',
        data: { accessKey: 'analytics' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/analytics/personal-analytics.component').then(
            (m) => m.PersonalAnalyticsComponent,
          ),
      },
      {
        path: 'agent-analytics',
        data: { accessKey: 'agentAnalytics' },
        canActivate: [managerGuard, pageAccessGuard],
        loadComponent: () =>
          import('./features/analytics/agent-analytics.component').then(
            (m) => m.AgentAnalyticsComponent,
          ),
      },
      {
        path: 'commissions',
        data: { accessKey: 'commissions' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/commissions/commissions.component').then(
            (m) => m.CommissionsComponent,
          ),
      },
      {
        path: 'properties',
        data: { accessKey: 'portfolio.properties' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/properties/property-management.component').then(
            (m) => m.PropertyManagementComponent,
          ),
      },
      {
        path: 'properties/new',
        canActivate: [managerGuard, pageAccessGuard],
        data: {
          accessKey: 'portfolio.properties.add',
          parentAccessKey: 'portfolio.properties',
        },
        loadComponent: () =>
          import('./features/properties/property-form.component').then(
            (m) => m.PropertyFormComponent,
          ),
      },
      {
        path: 'properties/:id/edit',
        canActivate: [managerGuard, pageAccessGuard],
        data: {
          accessKey: 'portfolio.properties.edit',
          parentAccessKey: 'portfolio.properties',
        },
        loadComponent: () =>
          import('./features/properties/property-form.component').then(
            (m) => m.PropertyFormComponent,
          ),
      },
      {
        path: 'properties/:id/assign',
        canActivate: [pageAccessGuard],
        data: {
          accessKey: 'portfolio.properties.sell',
          parentAccessKey: 'portfolio.properties',
        },
        loadComponent: () =>
          import('./features/properties/tenant-assignment.component').then(
            (m) => m.TenantAssignmentComponent,
          ),
      },
      {
        path: 'properties/:id/book',
        canActivate: [pageAccessGuard],
        data: {
          accessKey: 'portfolio.properties.book',
          parentAccessKey: 'portfolio.properties',
        },
        loadComponent: () =>
          import('./features/properties/property-booking.component').then(
            (m) => m.PropertyBookingComponent,
          ),
      },
      {
        path: 'properties/:id/bookings',
        canActivate: [pageAccessGuard],
        data: {
          accessKey: 'portfolio.properties.book',
          parentAccessKey: 'portfolio.properties',
        },
        loadComponent: () =>
          import('./features/properties/property-bookings.component').then(
            (m) => m.PropertyBookingsComponent,
          ),
      },
      {
        path: 'blocks',
        data: { accessKey: 'portfolio.blocks' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/blocks/block-management.component').then(
            (m) => m.BlockManagementComponent,
          ),
      },
      {
        path: 'bookings',
        data: { accessKey: 'portfolio.bookings' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/properties/portfolio-bookings.component').then(
            (m) => m.PortfolioBookingsComponent,
          ),
      },
      {
        path: 'tenants',
        data: { accessKey: 'portfolio.tenants' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/tenants/tenants.component').then((m) => m.TenantsComponent),
      },
      {
        path: 'auction/create-listing',
        data: {
          accessKey: 'auction.createListing',
          section: 'Auction',
          title: 'Create Listing',
          description: 'Select a property and prepare it for the auction listing workflow.',
        },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/workspace-placeholder/workspace-placeholder.component').then(
            (m) => m.WorkspacePlaceholderComponent,
          ),
      },
      {
        path: 'auction/listings',
        data: {
          accessKey: 'auction.listings',
          section: 'Auction',
          title: 'Listings',
          description: 'Manage properties prepared for future auction workflows.',
        },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/workspace-placeholder/workspace-placeholder.component').then(
            (m) => m.WorkspacePlaceholderComponent,
          ),
      },
      {
        path: 'notices/overdue',
        data: { mode: 'overdueNotice', accessKey: 'notices.overdue' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/notices/notices.component').then((m) => m.NoticesComponent),
      },
      {
        path: 'notices/eviction',
        data: { mode: 'evictionNotice', accessKey: 'notices.eviction' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/notices/notices.component').then((m) => m.NoticesComponent),
      },
      {
        path: 'notices/overdue-list',
        data: { mode: 'overdueList', accessKey: 'notices.overdueList' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/notices/notices.component').then((m) => m.NoticesComponent),
      },
      {
        path: 'notices/eviction-queue',
        data: { mode: 'evictionQueue', accessKey: 'notices.evictionQueue' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/notices/notices.component').then((m) => m.NoticesComponent),
      },
      {
        path: 'notices/eviction-history',
        data: { mode: 'evictionHistory', accessKey: 'notices.evictionHistory' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/notices/notices.component').then((m) => m.NoticesComponent),
      },
      {
        path: 'notices/eviction-list',
        pathMatch: 'full',
        redirectTo: 'notices/eviction-history',
      },
      {
        path: 'notices/sync',
        canActivate: [managerGuard, pageAccessGuard],
        data: { mode: 'sync', accessKey: 'notices.sync' },
        loadComponent: () =>
          import('./features/notices/notices.component').then((m) => m.NoticesComponent),
      },
      {
        path: 'notices/synced-data-records',
        data: { mode: 'syncedDataRecords', accessKey: 'notices.syncedDataRecords' },
        canActivate: [pageAccessGuard],
        loadComponent: () =>
          import('./features/notices/notices.component').then((m) => m.NoticesComponent),
      },
      {
        path: 'users',
        canActivate: [managerGuard, pageAccessGuard],
        data: { accessKey: 'administration.users' },
        loadComponent: () =>
          import('./features/users/user-management.component').then(
            (m) => m.UserManagementComponent,
          ),
      },
      {
        path: 'audit-logs',
        canActivate: [managerGuard, pageAccessGuard],
        data: { accessKey: 'administration.auditLogs' },
        loadComponent: () =>
          import('./features/audit/audit-logs.component').then((m) => m.AuditLogsComponent),
      },
      {
        path: 'profile',
        canActivate: [pageAccessGuard],
        data: { accessKey: 'profile' },
        loadComponent: () =>
          import('./features/profile/profile.component').then((m) => m.ProfileComponent),
      },
      {
        path: 'settings',
        canActivate: [managerGuard, pageAccessGuard],
        data: { accessKey: 'administration.settings' },
        loadComponent: () =>
          import('./features/settings/settings.component').then((m) => m.SettingsComponent),
      },
      {
        path: 'access-management',
        canActivate: [managerGuard, pageAccessGuard],
        data: { accessKey: 'administration.accessManagement' },
        loadComponent: () =>
          import('./features/access-management/access-management.component').then(
            (m) => m.AccessManagementComponent,
          ),
      },
      {
        path: 'recruitment/pending',
        canActivate: [managerGuard, pageAccessGuard],
        data: { status: 'pending', accessKey: 'recruitment.pending' },
        loadComponent: () =>
          import('./features/recruitment/recruitment-applications.component').then(
            (m) => m.RecruitmentApplicationsComponent,
          ),
      },
      {
        path: 'recruitment/accepted',
        canActivate: [managerGuard, pageAccessGuard],
        data: { status: 'accepted', accessKey: 'recruitment.accepted' },
        loadComponent: () =>
          import('./features/recruitment/recruitment-applications.component').then(
            (m) => m.RecruitmentApplicationsComponent,
          ),
      },
      {
        path: 'recruitment/rejected',
        canActivate: [managerGuard, pageAccessGuard],
        data: { status: 'rejected', accessKey: 'recruitment.rejected' },
        loadComponent: () =>
          import('./features/recruitment/recruitment-applications.component').then(
            (m) => m.RecruitmentApplicationsComponent,
          ),
      },
    ],
  },
  {
    path: '',
    loadComponent: () =>
      import('./layouts/public-layout/public-layout.component').then(
        (m) => m.PublicLayoutComponent,
      ),
    children: [
      {
        path: '',
        loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent),
      },
      {
        path: 'properties',
        loadComponent: () =>
          import('./features/properties/properties-list.component').then(
            (m) => m.PropertiesListComponent,
          ),
      },
      {
        path: 'properties/:id',
        loadComponent: () =>
          import('./features/properties/property-details.component').then(
            (m) => m.PropertyDetailsComponent,
          ),
      },
      {
        path: 'about',
        loadComponent: () =>
          import('./features/about/about.component').then((m) => m.AboutComponent),
      },
      {
        path: 'join-us',
        loadComponent: () =>
          import('./features/recruitment/join-us.component').then((m) => m.JoinUsComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
