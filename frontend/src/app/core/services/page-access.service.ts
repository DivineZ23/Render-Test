import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, Observable, of, shareReplay, tap } from 'rxjs';
import { AccessManagementSettings, UserRole } from '../models/user.models';
import { defaultAccessSettings } from '../constants/access-resource.constants';
import { AccessManagementService } from './management.services';
import { AuthService } from './auth.service';

const DEFAULTS: AccessManagementSettings = defaultAccessSettings();
const DASHBOARD_DESTINATIONS: ReadonlyArray<readonly [string, string]> = [
  ['overview', '/dashboard'],
  ['team', '/dashboard/team'],
  ['analytics', '/dashboard/analytics'],
  ['commissions', '/dashboard/commissions'],
  ['auction.createListing', '/dashboard/auction/create-listing'],
  ['auction.listings', '/dashboard/auction/listings'],
  ['portfolio.properties', '/dashboard/properties'],
  ['portfolio.bookings', '/dashboard/bookings'],
  ['portfolio.blocks', '/dashboard/blocks'],
  ['portfolio.tenants', '/dashboard/tenants'],
  ['notices.overdue', '/dashboard/notices/overdue'],
  ['notices.eviction', '/dashboard/notices/eviction'],
  ['notices.overdueList', '/dashboard/notices/overdue-list'],
  ['notices.evictionQueue', '/dashboard/notices/eviction-queue'],
  ['notices.evictionHistory', '/dashboard/notices/eviction-history'],
  ['notices.sync', '/dashboard/notices/sync'],
  ['notices.syncedDataRecords', '/dashboard/notices/synced-data-records'],
  ['recruitment.pending', '/dashboard/recruitment/pending'],
  ['recruitment.accepted', '/dashboard/recruitment/accepted'],
  ['recruitment.rejected', '/dashboard/recruitment/rejected'],
  ['administration.users', '/dashboard/users'],
  ['administration.auditLogs', '/dashboard/audit-logs'],
  ['administration.settings', '/dashboard/settings'],
  ['administration.accessManagement', '/dashboard/access-management'],
  ['profile', '/dashboard/profile'],
];

@Injectable({ providedIn: 'root' })
export class PageAccessService {
  private readonly api = inject(AccessManagementService);
  private readonly auth = inject(AuthService);
  private readonly state = signal<AccessManagementSettings>(DEFAULTS);
  private request$?: Observable<AccessManagementSettings>;
  readonly settings = this.state.asReadonly();
  readonly loaded = signal(false);
  readonly role = computed(() => this.auth.user()?.role);

  load(): Observable<AccessManagementSettings> {
    if (this.loaded()) return of(this.state());
    return (this.request$ ??= this.api.get().pipe(
      tap((settings) => {
        this.state.set(settings);
        this.loaded.set(true);
      }),
      catchError(() => {
        this.loaded.set(true);
        return of(this.state());
      }),
      shareReplay(1),
    ));
  }
  canAccess(resource: string): boolean {
    const role = this.role();
    if (!role) return false;
    return this.state().permissions[resource]?.[role] === true;
  }
  firstAccessibleUrl(): string {
    return (
      DASHBOARD_DESTINATIONS.find(([resource]) => this.canAccess(resource))?.[1] ?? '/'
    );
  }
  save(settings: AccessManagementSettings): Observable<AccessManagementSettings> {
    return this.api.update(settings).pipe(tap((saved) => this.state.set(saved)));
  }
}
