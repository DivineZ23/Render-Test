import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NgIf } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catchError, EMPTY, exhaustMap, filter, interval, startWith } from 'rxjs';
import {
  LucideBell,
  LucideBadgeDollarSign,
  LucideBriefcaseBusiness,
  LucideChartNoAxesCombined,
  LucideBlocks,
  LucideBuilding2,
  LucideChevronDown,
  LucideClipboardList,
  LucideClockAlert,
  LucideContactRound,
  LucideFileWarning,
  LucideGavel,
  LucideGauge,
  LucideHistory,
  LucideHousePlus,
  LucideInbox,
  LucideLayoutDashboard,
  LucideList,
  LucideListX,
  LucideLogOut,
  LucideMenu,
  LucidePanelLeftClose,
  LucidePanelLeftOpen,
  LucideScrollText,
  LucideSettings,
  LucideShieldCheck,
  LucideTimer,
  LucideUploadCloud,
  LucideUserRoundCheck,
  LucideUserRoundX,
  LucideUsersRound,
} from '@lucide/angular';
import { AuthService } from '../../core/services/auth.service';
import { PageAccessService } from '../../core/services/page-access.service';
import { ThemeToggleComponent } from '../../shared/components/theme-toggle/theme-toggle.component';
import { SiteCreditComponent } from '../../shared/components/site-credit/site-credit.component';
import { USER_ROLES } from '../../core/constants/user-role.constants';
import { PropertyService } from '../../core/services/property.service';

@Component({
  selector: 'app-dashboard-layout',
  standalone: true,
  imports: [
    NgIf,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    ThemeToggleComponent,
    SiteCreditComponent,
    LucideBell,
    LucideBadgeDollarSign,
    LucideBriefcaseBusiness,
    LucideChartNoAxesCombined,
    LucideLayoutDashboard,
    LucideUsersRound,
    LucideBuilding2,
    LucideChevronDown,
    LucideClipboardList,
    LucideClockAlert,
    LucideBlocks,
    LucideContactRound,
    LucideFileWarning,
    LucideTimer,
    LucideGavel,
    LucideGauge,
    LucideHistory,
    LucideHousePlus,
    LucideInbox,
    LucideUploadCloud,
    LucideUserRoundCheck,
    LucideUserRoundX,
    LucideList,
    LucideListX,
    LucideShieldCheck,
    LucideScrollText,
    LucideSettings,
    LucidePanelLeftClose,
    LucidePanelLeftOpen,
    LucideMenu,
    LucideLogOut,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="shell" [class.collapsed]="collapsed()">
    <aside>
      <div class="aside-head">
        <a routerLink="/dashboard" class="mark" aria-label="Imperial Estates dashboard"
          ><span class="brand-logo" aria-hidden="true"></span></a
        ><strong>Imperial Estates</strong>
      </div>
      <nav aria-label="Dashboard sections">
        <section class="nav-section" *ngIf="hasAny(['analytics', 'agentAnalytics'])">
          <button
            class="section-toggle"
            type="button"
            [attr.aria-expanded]="performanceOpen()"
            aria-controls="performance-navigation"
            (click)="performanceOpen.update((open) => !open)"
          >
            <span><svg lucideGauge></svg><b>Performance</b></span>
            <svg class="chevron" lucideChevronDown [class.rotated]="!performanceOpen()"></svg>
          </button>
          <ng-container *ngIf="performanceOpen()">
            <div class="section-links" id="performance-navigation">
              <a
                *ngIf="access.canAccess('analytics')"
                routerLink="/dashboard/analytics"
                routerLinkActive="active"
              >
                <svg lucideChartNoAxesCombined></svg><b>My Analytics</b>
              </a>
              <a
                *ngIf="auth.isManager() && access.canAccess('agentAnalytics')"
                routerLink="/dashboard/agent-analytics"
                routerLinkActive="active"
              >
                <svg lucideUsersRound></svg><b>Agent Analytics</b>
              </a>
            </div>
          </ng-container>
        </section>
        <section class="nav-section" *ngIf="access.canAccess('commissions')">
          <button
            class="section-toggle"
            type="button"
            [attr.aria-expanded]="financeOpen()"
            aria-controls="finance-navigation"
            (click)="financeOpen.update((open) => !open)"
          >
            <span><svg lucideBadgeDollarSign></svg><b>Finance</b></span>
            <svg class="chevron" lucideChevronDown [class.rotated]="!financeOpen()"></svg>
          </button>
          <ng-container *ngIf="financeOpen()">
            <div class="section-links" id="finance-navigation">
              <a routerLink="/dashboard/commissions" routerLinkActive="active">
                <svg lucideBadgeDollarSign></svg><b>Commissions</b>
              </a>
            </div>
          </ng-container>
        </section>
        <section class="nav-section" *ngIf="hasAny(['auction.createListing', 'auction.listings'])">
          <button
            class="section-toggle"
            type="button"
            [attr.aria-expanded]="auctionOpen()"
            aria-controls="auction-navigation"
            (click)="auctionOpen.update((open) => !open)"
          >
            <span><svg lucideGavel></svg><b>Auction</b></span>
            <svg class="chevron" lucideChevronDown [class.rotated]="!auctionOpen()"></svg>
          </button>
          <ng-container *ngIf="auctionOpen()">
            <div class="section-links" id="auction-navigation">
              <a
                *ngIf="access.canAccess('auction.createListing')"
                routerLink="/dashboard/auction/create-listing"
                routerLinkActive="active"
              >
                <svg lucideHousePlus></svg><b>Create Listing</b>
              </a>
              <a
                *ngIf="access.canAccess('auction.listings')"
                routerLink="/dashboard/auction/listings"
                routerLinkActive="active"
              >
                <svg lucideClipboardList></svg><b>Listings</b>
              </a>
            </div>
          </ng-container>
        </section>
        <section
          class="nav-section"
          *ngIf="
            hasAny([
              'portfolio.properties',
              'portfolio.blocks',
              'portfolio.tenants',
              'portfolio.bookings',
            ])
          "
        >
          <button
            class="section-toggle"
            type="button"
            [attr.aria-expanded]="portfolioOpen()"
            aria-controls="portfolio-navigation"
            (click)="portfolioOpen.update((open) => !open)"
          >
            <span><svg lucideBuilding2></svg><b>Portfolio</b></span>
            <svg class="chevron" lucideChevronDown [class.rotated]="!portfolioOpen()"></svg>
          </button>
          <ng-container *ngIf="portfolioOpen()">
            <div class="section-links" id="portfolio-navigation">
              <a
                *ngIf="access.canAccess('portfolio.properties')"
                routerLink="/dashboard/properties"
                routerLinkActive="active"
              >
                <svg lucideBuilding2></svg><b>Properties</b>
              </a>
              <a
                *ngIf="access.canAccess('portfolio.bookings')"
                routerLink="/dashboard/bookings"
                routerLinkActive="active"
              >
                <svg lucideClipboardList></svg><b>Bookings</b>
                <span
                  *ngIf="auth.isManager() && properties.pendingBookingAnnouncements() > 0"
                  class="nav-alert"
                  [attr.aria-label]="
                    properties.pendingBookingAnnouncements() + ' booking announcements pending'
                  "
                  title="Pending Discord property posts"
                >
                  <svg lucideBell></svg>
                  <span class="nav-alert-count">{{
                    properties.pendingBookingAnnouncements() > 99
                      ? '99+'
                      : properties.pendingBookingAnnouncements()
                  }}</span>
                </span>
              </a>
              <a
                *ngIf="access.canAccess('portfolio.blocks')"
                routerLink="/dashboard/blocks"
                routerLinkActive="active"
              >
                <svg lucideBlocks></svg><b>Blocks</b>
              </a>
              <a
                *ngIf="access.canAccess('portfolio.tenants')"
                routerLink="/dashboard/tenants"
                routerLinkActive="active"
              >
                <svg lucideContactRound></svg><b>Tenants</b>
              </a>
            </div>
          </ng-container>
        </section>
        <section
          class="nav-section"
          *ngIf="
            hasAny([
              'notices.overdue',
              'notices.eviction',
              'notices.overdueList',
              'notices.evictionQueue',
              'notices.evictionHistory',
              'notices.sync',
              'notices.syncedDataRecords',
            ])
          "
        >
          <button
            class="section-toggle"
            type="button"
            [attr.aria-expanded]="noticesOpen()"
            aria-controls="notices-navigation"
            (click)="noticesOpen.update((open) => !open)"
          >
            <span><svg lucideBell></svg><b>Notices</b></span>
            <svg class="chevron" lucideChevronDown [class.rotated]="!noticesOpen()"></svg>
          </button>
          <ng-container *ngIf="noticesOpen()">
            <div class="section-links" id="notices-navigation">
              <a
                *ngIf="access.canAccess('notices.overdue')"
                routerLink="/dashboard/notices/overdue"
                routerLinkActive="active"
              >
                <svg lucideClockAlert></svg><b>Overdue Notice</b>
              </a>
              <a
                *ngIf="access.canAccess('notices.eviction')"
                routerLink="/dashboard/notices/eviction"
                routerLinkActive="active"
              >
                <svg lucideFileWarning></svg><b>Eviction Notice</b>
              </a>
              <a
                *ngIf="access.canAccess('notices.overdueList')"
                routerLink="/dashboard/notices/overdue-list"
                routerLinkActive="active"
              >
                <svg lucideList></svg><b>Overdue List</b>
              </a>
              <a
                *ngIf="access.canAccess('notices.evictionQueue')"
                routerLink="/dashboard/notices/eviction-queue"
                routerLinkActive="active"
              >
                <svg lucideTimer></svg><b>Eviction Queue</b>
              </a>
              <a
                *ngIf="access.canAccess('notices.evictionHistory')"
                routerLink="/dashboard/notices/eviction-history"
                routerLinkActive="active"
              >
                <svg lucideListX></svg><b>Eviction History</b>
              </a>
              <ng-container *ngIf="auth.isManager() && access.canAccess('notices.sync')">
                <a routerLink="/dashboard/notices/sync" routerLinkActive="active">
                  <svg lucideUploadCloud></svg><b>Data Sync</b>
                </a>
              </ng-container>
              <a
                *ngIf="access.canAccess('notices.syncedDataRecords')"
                routerLink="/dashboard/notices/synced-data-records"
                routerLinkActive="active"
              >
                <svg lucideHistory></svg><b>Sync History</b>
              </a>
            </div>
          </ng-container>
        </section>
        <section
          class="nav-section"
          *ngIf="
            auth.isManager() &&
            hasAny(['recruitment.pending', 'recruitment.accepted', 'recruitment.rejected'])
          "
        >
          <button
            class="section-toggle"
            type="button"
            [attr.aria-expanded]="recruitmentOpen()"
            aria-controls="recruitment-navigation"
            (click)="recruitmentOpen.update((open) => !open)"
          >
            <span><svg lucideBriefcaseBusiness></svg><b>Recruitment</b></span>
            <svg class="chevron" lucideChevronDown [class.rotated]="!recruitmentOpen()"></svg>
          </button>
          <ng-container *ngIf="recruitmentOpen()">
            <div class="section-links" id="recruitment-navigation">
              <a
                *ngIf="access.canAccess('recruitment.pending')"
                routerLink="/dashboard/recruitment/pending"
                routerLinkActive="active"
              >
                <svg lucideInbox></svg><b>Pending</b>
              </a>
              <a
                *ngIf="access.canAccess('recruitment.accepted')"
                routerLink="/dashboard/recruitment/accepted"
                routerLinkActive="active"
              >
                <svg lucideUserRoundCheck></svg><b>Accepted</b>
              </a>
              <a
                *ngIf="access.canAccess('recruitment.rejected')"
                routerLink="/dashboard/recruitment/rejected"
                routerLinkActive="active"
              >
                <svg lucideUserRoundX></svg><b>Rejected</b>
              </a>
            </div>
          </ng-container>
        </section>
        <ng-container
          *ngIf="
            auth.isManager() &&
            hasAny([
              'administration.users',
              'administration.auditLogs',
              'administration.settings',
              'administration.accessManagement',
            ])
          "
        >
          <section class="nav-section">
            <button
              class="section-toggle"
              type="button"
              [attr.aria-expanded]="administrationOpen()"
              aria-controls="administration-navigation"
              (click)="administrationOpen.update((open) => !open)"
            >
              <span><svg lucideShieldCheck></svg><b>Administration</b></span>
              <svg class="chevron" lucideChevronDown [class.rotated]="!administrationOpen()"></svg>
            </button>
            <ng-container *ngIf="administrationOpen()">
              <div class="section-links" id="administration-navigation">
                <a
                  *ngIf="access.canAccess('administration.users')"
                  routerLink="/dashboard/users"
                  routerLinkActive="active"
                >
                  <svg lucideUsersRound></svg><b>User Management</b>
                </a>
                <a
                  *ngIf="access.canAccess('administration.auditLogs')"
                  routerLink="/dashboard/audit-logs"
                  routerLinkActive="active"
                >
                  <svg lucideScrollText></svg><b>Audit Logs</b>
                </a>
                <a
                  *ngIf="access.canAccess('administration.settings')"
                  routerLink="/dashboard/settings"
                  routerLinkActive="active"
                >
                  <svg lucideSettings></svg><b>Settings</b>
                </a>
                <a
                  *ngIf="access.canAccess('administration.accessManagement')"
                  routerLink="/dashboard/access-management"
                  routerLinkActive="active"
                >
                  <svg lucideShieldCheck></svg><b>Access Management</b>
                </a>
              </div>
            </ng-container>
          </section>
        </ng-container>
      </nav>
      <button class="collapse" (click)="collapsed.update((x) => !x)" aria-label="Toggle sidebar">
        <ng-container *ngIf="collapsed(); else expanded">
          <svg lucidePanelLeftOpen></svg>
        </ng-container>
        <ng-template #expanded>
          <svg lucidePanelLeftClose></svg>
        </ng-template>
        <b>Collapse</b>
      </button>
    </aside>
    <section class="workspace">
      <header>
        <button
          class="mobile-menu"
          (click)="collapsed.update((x) => !x)"
          aria-label="Toggle dashboard navigation"
        >
          <svg lucideMenu></svg>
        </button>
        <nav class="breadcrumb" aria-label="Dashboard breadcrumb">
          <a
            *ngIf="access.canAccess('overview')"
            routerLink="/dashboard"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: true }"
            ><svg lucideLayoutDashboard></svg>Overview</a
          ><a
            *ngIf="access.canAccess('team')"
            routerLink="/dashboard/team"
            routerLinkActive="active"
            ><svg lucideUsersRound></svg>Team</a
          >
        </nav>
        <div class="account">
          <app-theme-toggle />
          <a *ngIf="access.canAccess('profile')" routerLink="/dashboard/profile"
            ><img [src]="auth.user()?.avatarUrl || fallback" alt="" /><span
              ><b>{{ auth.user()?.displayName }}</b
              ><small>{{ roleLabel() }}</small></span
            ></a
          ><button (click)="auth.logout()"><svg lucideLogOut></svg><span>Sign out</span></button>
        </div>
      </header>
      <main><router-outlet /></main>
      <app-site-credit />
    </section>
  </div>`,
  styles: [
    `
      .shell {
        min-height: 100vh;
        display: grid;
        grid-template-columns: 250px 1fr;
        background: var(--workspace-bg);
      }
      aside {
        position: sticky;
        top: 0;
        height: 100vh;
        background: var(--sidebar-bg);
        color: var(--sidebar-text);
        padding: 0 14px 18px;
        display: flex;
        flex-direction: column;
        overflow-y: auto;
      }
      .aside-head {
        height: 72px;
        flex: 0 0 72px;
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 0 6px;
        border-bottom: 1px solid var(--sidebar-border);
      }
      .mark {
        width: 62px;
        height: 62px;
        flex: 0 0 auto;
      }
      .mark .brand-logo {
        width: 100%;
        height: 100%;
        color: var(--sidebar-active);
      }
      .aside-head strong {
        font-family: var(--font-brand);
        font-size: 0.78rem;
        font-weight: 650;
        line-height: 1.2;
        letter-spacing: 0.055em;
        text-transform: uppercase;
        white-space: nowrap;
      }
      nav {
        display: flex;
        flex-direction: column;
        gap: 15px;
        padding-top: 18px;
        flex: 1;
      }
      .section-toggle,
      nav a {
        display: flex;
        align-items: center;
        gap: 11px;
        padding: 9px 11px;
        color: var(--sidebar-link);
        border-radius: 8px;
        font-size: 0.79rem;
        line-height: 1.3;
      }
      .section-toggle {
        width: 100%;
        justify-content: space-between;
        border: 0;
        background: transparent;
        cursor: pointer;
        color: var(--sidebar-subtle);
        font-size: 0.67rem;
        letter-spacing: 0.075em;
        text-transform: uppercase;
      }
      .section-toggle > span {
        display: flex;
        align-items: center;
        gap: 13px;
      }
      .section-toggle:hover {
        background: transparent;
        color: var(--sidebar-text);
      }
      .section-links {
        display: grid;
        gap: 10px;
        padding: 3px 0 5px 8px;
        border-left: 1px solid var(--sidebar-border);
        margin-left: 20px;
      }
      nav a svg,
      .section-toggle svg {
        width: 18px;
        height: 18px;
        flex: 0 0 auto;
        stroke-width: 1.8;
      }
      .section-toggle .chevron.rotated {
        transform: rotate(-90deg);
      }
      nav a b {
        font-weight: 530;
        letter-spacing: 0;
      }
      .section-toggle b {
        font-weight: 600;
      }
      nav a.active b {
        font-weight: 680;
      }
      nav a.active {
        background: var(--sidebar-active);
        color: var(--sidebar-active-text);
      }
      nav a:hover:not(.active) {
        background: var(--sidebar-hover);
        color: var(--sidebar-hover-text);
      }
      .collapse {
        border: 0;
        background: transparent;
        color: var(--sidebar-muted);
        padding: 10px 11px;
        display: flex;
        gap: 14px;
        cursor: pointer;
      }
      .collapse svg {
        width: 18px;
        height: 18px;
      }
      .workspace {
        min-width: 0;
        min-height: 100vh;
        display: flex;
        flex-direction: column;
      }
      .workspace > header {
        height: 72px;
        background: var(--header-bg);
        backdrop-filter: blur(14px);
        border-bottom: 1px solid var(--border);
        display: grid;
        grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
        align-items: center;
        padding: 0 28px;
        position: sticky;
        top: 0;
        z-index: 20;
      }
      .breadcrumb {
        grid-column: 2;
        justify-self: center;
        display: flex;
        flex-direction: row;
        align-items: center;
        gap: 8px;
        padding: 0;
      }
      .breadcrumb a {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 7px 10px;
        color: var(--muted);
        border-radius: 7px;
        font-size: 0.82rem;
      }
      .breadcrumb a:hover,
      .breadcrumb a.active {
        color: var(--forest);
        background: var(--forest-light);
      }
      .account,
      .account a {
        display: flex;
        align-items: center;
        gap: 10px;
      }
      .account {
        grid-column: 3;
        justify-self: end;
      }
      .account img {
        width: 34px;
        height: 34px;
        object-fit: cover;
        border-radius: 50%;
      }
      .account span {
        display: grid;
      }
      .account small {
        color: var(--muted);
        text-transform: capitalize;
        font-size: 0.7rem;
      }
      .account button {
        border: 0;
        border-left: 1px solid var(--border);
        background: transparent;
        margin-left: 10px;
        padding-left: 18px;
        cursor: pointer;
        display: inline-flex;
        align-items: center;
        gap: 7px;
        color: var(--ink);
      }
      .workspace main {
        flex: 1;
        width: 100%;
        padding: 28px 24px 36px;
        max-width: 1640px;
        margin: 0 auto;
      }
      .mobile-menu {
        display: none;
      }
      .mobile-menu svg {
        width: 21px;
        height: 21px;
      }
      .collapsed {
        grid-template-columns: 74px 1fr;
      }
      .collapsed .aside-head strong,
      .collapsed nav b,
      .collapsed .section-toggle .chevron,
      .collapsed .collapse b {
        display: none;
      }
      .collapsed .aside-head {
        padding-inline: 4px;
      }
      .collapsed nav a,
      .collapsed .section-toggle {
        justify-content: center;
      }
      .collapsed .section-links {
        padding-left: 0;
        margin-left: 0;
        border-left: 0;
      }
      .collapsed .collapse {
        justify-content: center;
      }
      @media (max-width: 900px) {
        .shell,
        .collapsed {
          grid-template-columns: 1fr;
        }
        aside {
          position: fixed;
          z-index: 40;
          width: 250px;
          transform: translateX(-105%);
          transition: transform var(--ease);
        }
        .collapsed aside {
          transform: translateX(0);
        }
        .collapsed .aside-head strong,
        .collapsed nav b,
        .collapsed .section-toggle .chevron,
        .collapsed .collapse b {
          display: block;
        }
        .collapsed nav a,
        .collapsed .section-toggle {
          justify-content: flex-start;
        }
        .collapsed .section-toggle {
          justify-content: space-between;
        }
        .collapsed .section-links {
          padding-left: 13px;
          margin-left: 22px;
          border-left: 1px solid var(--sidebar-border);
        }
        .mobile-menu {
          display: block;
          border: 0;
          background: none;
        }
        .workspace > header {
          grid-template-columns: auto 1fr auto;
          padding: 0 18px;
        }
        .workspace main {
          padding: 20px 14px;
        }
        .account a span,
        .account button {
          display: none;
        }
      }
    `,
  ],
})
export class DashboardLayoutComponent {
  readonly auth = inject(AuthService);
  readonly access = inject(PageAccessService);
  readonly properties = inject(PropertyService);
  private readonly destroyRef = inject(DestroyRef);
  readonly collapsed = signal(false);
  readonly performanceOpen = signal(true);
  readonly financeOpen = signal(true);
  readonly portfolioOpen = signal(true);
  readonly auctionOpen = signal(true);
  readonly noticesOpen = signal(true);
  readonly recruitmentOpen = signal(true);
  readonly administrationOpen = signal(true);
  readonly fallback = 'https://api.dicebear.com/9.x/initials/svg?seed=Imperial';
  readonly roleLabel = computed(() => {
    const role = this.auth.user()?.role;
    return role ? USER_ROLES[role] : '';
  });
  constructor() {
    this.access.load().subscribe();
    if (this.auth.isManager()) {
      interval(30_000)
        .pipe(
          startWith(0),
          filter(() => !document.hidden),
          exhaustMap(() =>
            this.properties.refreshBookingAnnouncementCount().pipe(catchError(() => EMPTY)),
          ),
          takeUntilDestroyed(this.destroyRef),
        )
        .subscribe();
    }
  }
  hasAny(resources: string[]): boolean {
    return resources.some((resource) => this.access.canAccess(resource));
  }
}
