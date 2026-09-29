import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import {
  LucideChevronDown,
  LucideChevronLeft,
  LucideChevronRight,
  LucideRefreshCw,
  LucideSearch,
  LucideShieldCheck,
  LucideX,
} from '@lucide/angular';
import { BehaviorSubject, catchError, debounceTime, finalize, of, switchMap, tap } from 'rxjs';
import { PagedResult } from '../../core/models/api.models';
import { AuditLog, AuditLogQuery } from '../../core/models/management.models';
import { User } from '../../core/models/user.models';
import { AuditService, UserService } from '../../core/services/management.services';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

const EMPTY_RESULT: PagedResult<AuditLog> = {
  items: [],
  page: 1,
  pageSize: 25,
  totalItems: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false,
};

@Component({
  selector: 'app-audit-logs',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    EmptyStateComponent,
    LucideChevronDown,
    LucideChevronLeft,
    LucideChevronRight,
    LucideRefreshCw,
    LucideSearch,
    LucideShieldCheck,
    LucideX,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<header class="page-title">
      <div>
        <p class="eyebrow">Accountability</p>
        <h1>Audit activity</h1>
        <p>A clear, searchable history of important portfolio, access, and financial changes.</p>
      </div>
      <button
        class="btn btn-secondary refresh"
        type="button"
        [disabled]="loading()"
        (click)="refresh()"
      >
        <svg lucideRefreshCw [class.spinning]="loading()"></svg>Refresh
      </button>
    </header>

    <section class="metrics" aria-label="Audit summary">
      <article class="panel metric">
        <span>Matching events</span><strong>{{ result().totalItems }}</strong
        ><small>Across the full audit history</small>
      </article>
      <article class="panel metric">
        <span>People on this page</span><strong>{{ visibleActorCount() }}</strong
        ><small>Unique team members</small>
      </article>
      <article class="panel metric attention">
        <span>Important changes</span><strong>{{ importantCount() }}</strong
        ><small>Warnings and critical events</small>
      </article>
    </section>

    <form class="filters panel" [formGroup]="filters">
      <label class="search-field">
        <span>Search activity</span>
        <div>
          <svg lucideSearch></svg
          ><input formControlName="search" placeholder="Action, person, property, block…" />
        </div>
      </label>
      <label
        ><span>Category</span
        ><select formControlName="category">
          <option value="">All categories</option>
          <option value="portfolio">Portfolio</option>
          <option value="access">Access & people</option>
          <option value="finance">Finance</option>
          <option value="operations">Operations</option>
        </select></label
      >
      <label
        ><span>Record type</span
        ><select formControlName="entityType">
          <option value="">All record types</option>
          <option value="property">Property</option>
          <option value="block">Block</option>
          <option value="user">User</option>
          <option value="enquiry">Enquiry</option>
          <option value="commission_settlement">Auction settlement</option>
          <option value="commission">Commission</option>
          <option value="rentSyncSnapshot">Rent sync</option>
          <option value="rentSyncRecord">Rent record</option>
          <option value="recruitment_application">Recruitment application</option>
          <option value="application_setting">Settings</option>
        </select></label
      >
      <label
        ><span>Performed by</span
        ><select formControlName="actorId">
          <option value="">Anyone</option>
          @for (actor of actors(); track actor.id) {
            <option [value]="actor.id">{{ actor.displayName }}</option>
          }
        </select></label
      >
      <label><span>From</span><input type="date" formControlName="from" /></label>
      <label><span>To</span><input type="date" formControlName="to" /></label>
      <label
        ><span>Order</span
        ><select formControlName="sortDirection">
          <option value="desc">Newest first</option>
          <option value="asc">Oldest first</option>
        </select></label
      >
      <button class="clear" type="button" [disabled]="!hasFilters()" (click)="clearFilters()">
        <svg lucideX></svg>Clear filters
      </button>
    </form>

    @if (error()) {
      <div class="error-banner" role="alert">
        {{ error() }} <button type="button" (click)="refresh()">Try again</button>
      </div>
    }

    <section class="activity panel" [class.is-loading]="loading()" aria-live="polite">
      <div class="activity-heading">
        <div>
          <svg lucideShieldCheck></svg
          ><span
            ><b>Activity history</b><small>{{ rangeLabel() }}</small></span
          >
        </div>
        @if (loading()) {
          <span class="loading-label">Updating…</span>
        }
      </div>

      <div class="activity-list">
        @for (log of result().items; track log.id) {
          <article class="event" [class]="'event ' + log.severity">
            <div class="timeline">
              <span class="event-dot"></span><span class="event-line"></span>
            </div>
            <div class="event-body">
              <div class="event-topline">
                <div class="badges">
                  <span class="category">{{ categoryLabel(log.category) }}</span
                  ><span class="record-type">{{ log.entityTypeLabel }}</span>
                </div>
                <time [attr.datetime]="log.createdAt"
                  ><b>{{ log.createdAt | date: 'mediumDate' }}</b
                  ><span
                    >{{ log.createdAt | date: 'shortTime' }} ·
                    {{ relativeTime(log.createdAt) }}</span
                  ></time
                >
              </div>
              <h2>{{ log.actionLabel }}</h2>
              <p class="summary">{{ log.summary }}</p>
              <div class="actor-row">
                @if (log.performedByAvatarUrl) {
                  <img [src]="log.performedByAvatarUrl" alt="" />
                } @else {
                  <span class="avatar-fallback">{{ initials(log.performedByDisplayName) }}</span>
                }
                <span
                  ><b>{{ log.performedByDisplayName }}</b
                  ><small>{{ roleLabel(log.performedByRole) }}</small></span
                >
                <button
                  class="details-toggle"
                  type="button"
                  [attr.aria-expanded]="expandedId() === log.id"
                  (click)="toggle(log.id)"
                >
                  {{ expandedId() === log.id ? 'Hide details' : 'View details'
                  }}<svg lucideChevronDown></svg>
                </button>
              </div>

              @if (expandedId() === log.id) {
                <div class="details">
                  @if (detailEntries(log).length) {
                    <div class="detail-grid">
                      @for (entry of detailEntries(log); track entry.label) {
                        <div>
                          <span>{{ entry.label }}</span
                          ><b>{{ entry.value }}</b>
                        </div>
                      }
                    </div>
                  } @else {
                    <p class="no-context">
                      No additional business context was recorded for this event.
                    </p>
                  }
                  <details class="technical">
                    <summary>Technical details</summary>
                    <dl>
                      <div>
                        <dt>Event ID</dt>
                        <dd>{{ log.id }}</dd>
                      </div>
                      <div>
                        <dt>Action key</dt>
                        <dd>{{ log.action }}</dd>
                      </div>
                      <div>
                        <dt>Entity ID</dt>
                        <dd>{{ log.entityId }}</dd>
                      </div>
                      <div>
                        <dt>User ID</dt>
                        <dd>{{ log.performedByUserId }}</dd>
                      </div>
                    </dl>
                  </details>
                </div>
              }
            </div>
          </article>
        } @empty {
          <app-empty-state
            title="No audit activity found"
            message="Try clearing a filter or widening the selected date range."
          />
        }
      </div>
    </section>

    @if (result().totalPages > 1) {
      <nav class="pagination" aria-label="Audit log pages">
        <button
          class="btn btn-secondary"
          [disabled]="!result().hasPreviousPage || loading()"
          (click)="goToPage(result().page - 1)"
        >
          <svg lucideChevronLeft></svg>Previous
        </button>
        <span
          >Page <b>{{ result().page }}</b> of {{ result().totalPages }}</span
        >
        <button
          class="btn btn-secondary"
          [disabled]="!result().hasNextPage || loading()"
          (click)="goToPage(result().page + 1)"
        >
          Next<svg lucideChevronRight></svg>
        </button>
      </nav>
    }`,
  styles: [
    `
      :host {
        display: block;
        container-type: inline-size;
      }
      .page-title {
        display: flex;
        justify-content: space-between;
        align-items: end;
        gap: 24px;
        margin-bottom: 22px;
      }
      .page-title h1 {
        font-size: 2.5rem;
        margin: 4px 0;
      }
      .page-title p:last-child {
        color: var(--muted);
        margin: 0;
      }
      .refresh svg {
        width: 16px;
      }
      .spinning {
        animation: spin 0.8s linear infinite;
      }
      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }
      .metrics {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: 14px;
        margin-bottom: 14px;
      }
      .metric {
        padding: 18px 20px;
        display: grid;
        gap: 4px;
        border-top: 3px solid color-mix(in srgb, var(--bronze) 55%, var(--border));
      }
      .metric span,
      .metric small {
        color: var(--muted);
        font-size: 0.78rem;
      }
      .metric strong {
        font-size: 1.8rem;
        line-height: 1.1;
      }
      .metric.attention {
        border-top-color: #d69035;
      }
      .filters {
        display: grid;
        grid-template-columns: minmax(240px, 2fr) repeat(3, minmax(145px, 1fr));
        gap: 12px;
        padding: 16px;
        margin-bottom: 14px;
        align-items: end;
      }
      .filters label {
        display: grid;
        gap: 6px;
      }
      .filters label > span {
        font-size: 0.7rem;
        font-weight: 700;
        letter-spacing: 0.05em;
        text-transform: uppercase;
        color: var(--muted);
      }
      .filters input,
      .filters select {
        width: 100%;
        height: 42px;
        border: 1px solid var(--border);
        border-radius: 9px;
        background: var(--surface-strong);
        color: var(--ink);
        padding: 0 11px;
      }
      .search-field div {
        position: relative;
      }
      .search-field svg {
        position: absolute;
        left: 12px;
        top: 12px;
        width: 17px;
        color: var(--muted);
      }
      .search-field input {
        padding-left: 38px;
      }
      .clear {
        height: 42px;
        border: 0;
        background: transparent;
        color: var(--muted);
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 7px;
        cursor: pointer;
      }
      .clear svg {
        width: 15px;
      }
      .clear:disabled {
        opacity: 0.4;
        cursor: not-allowed;
      }
      .error-banner {
        padding: 12px 14px;
        margin-bottom: 14px;
        border-radius: 10px;
        background: var(--danger-soft);
        color: var(--danger);
        font-size: 0.85rem;
      }
      .error-banner button {
        border: 0;
        background: none;
        color: inherit;
        text-decoration: underline;
        cursor: pointer;
      }
      .activity {
        overflow: hidden;
        transition: opacity 0.15s;
      }
      .activity.is-loading {
        opacity: 0.72;
      }
      .activity-heading {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 16px 20px;
        border-bottom: 1px solid var(--border);
      }
      .activity-heading > div {
        display: flex;
        align-items: center;
        gap: 11px;
      }
      .activity-heading svg {
        width: 20px;
        color: var(--bronze);
      }
      .activity-heading span span,
      .activity-heading small {
        display: block;
      }
      .activity-heading small,
      .loading-label {
        color: var(--muted);
        font-size: 0.75rem;
        margin-top: 2px;
      }
      .activity-list {
        padding: 4px 20px;
      }
      .event {
        display: grid;
        grid-template-columns: 22px minmax(0, 1fr);
        gap: 13px;
      }
      .timeline {
        position: relative;
        display: flex;
        justify-content: center;
      }
      .event-dot {
        width: 10px;
        height: 10px;
        margin-top: 28px;
        border-radius: 50%;
        background: var(--muted);
        box-shadow: 0 0 0 4px var(--surface);
      }
      .event-line {
        position: absolute;
        top: 40px;
        bottom: -12px;
        width: 1px;
        background: var(--border);
      }
      .event:last-child .event-line {
        display: none;
      }
      .event.success .event-dot {
        background: #3d9b6d;
      }
      .event.warning .event-dot {
        background: #d69035;
      }
      .event.danger .event-dot {
        background: var(--danger);
      }
      .event-body {
        min-width: 0;
        padding: 22px 0;
        border-bottom: 1px solid var(--border);
      }
      .event:last-child .event-body {
        border-bottom: 0;
      }
      .event-topline {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        flex-wrap: wrap;
        gap: 20px;
      }
      .badges {
        display: flex;
        gap: 7px;
        flex-wrap: wrap;
      }
      .badges span {
        border-radius: 999px;
        padding: 4px 8px;
        font-size: 0.64rem;
        font-weight: 750;
        letter-spacing: 0.05em;
        text-transform: uppercase;
      }
      .category {
        background: color-mix(in srgb, var(--bronze) 12%, var(--surface));
        color: var(--bronze);
      }
      .record-type {
        background: var(--surface-subtle);
        color: var(--muted);
      }
      time {
        flex: 0 0 auto;
        margin-left: auto;
        text-align: right;
        white-space: nowrap;
      }
      time b,
      time span {
        display: block;
        font-size: 0.72rem;
      }
      time span {
        color: var(--muted);
        margin-top: 3px;
      }
      .event h2 {
        font-size: 1rem;
        margin: 10px 0 4px;
      }
      .summary {
        margin: 0;
        color: var(--muted);
        font-size: 0.88rem;
        overflow-wrap: anywhere;
      }
      .actor-row {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        margin-top: 15px;
        gap: 9px;
      }
      .actor-row img,
      .avatar-fallback {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        object-fit: cover;
      }
      .avatar-fallback {
        display: grid;
        place-items: center;
        background: color-mix(in srgb, var(--bronze) 16%, var(--surface));
        color: var(--bronze);
        font-size: 0.68rem;
        font-weight: 800;
      }
      .actor-row > span:nth-child(2) {
        display: grid;
        min-width: 0;
      }
      .actor-row b {
        font-size: 0.78rem;
      }
      .actor-row small {
        color: var(--muted);
        font-size: 0.68rem;
      }
      .details-toggle {
        flex: 0 0 auto;
        margin-left: auto;
        border: 0;
        background: none;
        color: var(--bronze);
        display: flex;
        align-items: center;
        gap: 5px;
        cursor: pointer;
        font-size: 0.75rem;
      }
      .details-toggle svg {
        width: 15px;
        transition: transform 0.2s;
      }
      .details-toggle[aria-expanded='true'] svg {
        transform: rotate(180deg);
      }
      .details {
        margin-top: 15px;
        border: 1px solid var(--border);
        border-radius: 10px;
        background: var(--surface-subtle);
        padding: 14px;
      }
      .detail-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 10px;
      }
      .detail-grid div {
        display: grid;
        gap: 3px;
        padding: 10px;
        background: var(--surface);
        border-radius: 8px;
      }
      .detail-grid span {
        color: var(--muted);
        font-size: 0.68rem;
      }
      .detail-grid b {
        font-size: 0.78rem;
        overflow-wrap: anywhere;
      }
      .no-context {
        color: var(--muted);
        font-size: 0.78rem;
        margin: 0;
      }
      .technical {
        margin-top: 12px;
        border-top: 1px solid var(--border);
        padding-top: 11px;
      }
      .technical summary {
        cursor: pointer;
        color: var(--muted);
        font-size: 0.72rem;
      }
      .technical dl {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 8px;
        margin: 10px 0 0;
      }
      .technical dl div {
        min-width: 0;
      }
      .technical dt {
        color: var(--muted);
        font-size: 0.65rem;
      }
      .technical dd {
        margin: 2px 0 0;
        font-family: monospace;
        font-size: 0.66rem;
        overflow-wrap: anywhere;
      }
      .pagination {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 18px;
        margin-top: 18px;
      }
      .pagination span {
        color: var(--muted);
        font-size: 0.8rem;
      }
      .pagination b {
        color: var(--ink);
      }
      @media (max-width: 1050px) {
        .filters {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
        .search-field {
          grid-column: 1/-1;
        }
      }
      @media (max-width: 720px) {
        .page-title {
          align-items: flex-start;
        }
        .page-title h1 {
          font-size: 2rem;
        }
        .refresh {
          padding: 9px;
        }
        .metrics {
          grid-template-columns: 1fr;
        }
        .filters {
          grid-template-columns: 1fr;
        }
        .search-field {
          grid-column: auto;
        }
        .event-topline {
          display: grid;
        }
        .event-topline time {
          text-align: left;
        }
        .detail-grid,
        .technical dl {
          grid-template-columns: 1fr;
        }
        .activity-list {
          padding: 4px 13px;
        }
        .actor-row {
          align-items: flex-start;
        }
        .details-toggle {
          width: auto;
          margin-left: auto;
        }
      }
      @container (max-width: 700px) {
        .event-topline {
          display: grid;
          gap: 10px;
        }
        .event-topline time {
          margin-left: 0;
          text-align: left;
        }
        .actor-row {
          align-items: center;
        }
        .details-toggle {
          width: auto;
          margin-left: auto;
        }
      }
      @container (max-width: 460px) {
        .activity-list {
          padding: 4px 10px;
        }
        .event {
          grid-template-columns: 14px minmax(0, 1fr);
          gap: 8px;
        }
        .actor-row {
          display: grid;
          grid-template-columns: 32px minmax(0, 1fr);
        }
        .details-toggle {
          grid-column: 2;
          margin: 2px 0 0;
          justify-content: flex-start;
        }
      }
    `,
  ],
})
export class AuditLogsComponent {
  private readonly auditService = inject(AuditService);
  private readonly userService = inject(UserService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly page$ = new BehaviorSubject(1);

  readonly result = signal<PagedResult<AuditLog>>(EMPTY_RESULT);
  readonly actors = signal<User[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly expandedId = signal<string | null>(null);
  readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    category: new FormControl('', { nonNullable: true }),
    entityType: new FormControl('', { nonNullable: true }),
    actorId: new FormControl('', { nonNullable: true }),
    from: new FormControl('', { nonNullable: true }),
    to: new FormControl('', { nonNullable: true }),
    sortDirection: new FormControl<'asc' | 'desc'>('desc', { nonNullable: true }),
  });

  constructor() {
    this.userService
      .all()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) =>
        this.actors.set(
          [...value.items].sort((a, b) => a.displayName.localeCompare(b.displayName)),
        ),
      );
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.page$.next(1));
    this.page$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set('');
        }),
        switchMap((page) =>
          this.auditService.all(this.query(page)).pipe(
            catchError(() => {
              this.error.set('Audit activity could not be loaded.');
              return of(EMPTY_RESULT);
            }),
            finalize(() => this.loading.set(false)),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((value) => {
        this.result.set(value);
        this.expandedId.set(null);
      });
  }

  refresh(): void {
    this.page$.next(this.result().page || 1);
  }
  goToPage(page: number): void {
    this.page$.next(page);
  }
  toggle(id: string): void {
    this.expandedId.update((value) => (value === id ? null : id));
  }
  clearFilters(): void {
    this.filters.reset({
      search: '',
      category: '',
      entityType: '',
      actorId: '',
      from: '',
      to: '',
      sortDirection: 'desc',
    });
  }
  hasFilters(): boolean {
    const { sortDirection, ...values } = this.filters.getRawValue();
    return Object.values(values).some(Boolean) || sortDirection !== 'desc';
  }
  visibleActorCount(): number {
    return new Set(this.result().items.map((x) => x.performedByUserId)).size;
  }
  importantCount(): number {
    return this.result().items.filter((x) => x.severity === 'warning' || x.severity === 'danger')
      .length;
  }
  rangeLabel(): string {
    const r = this.result();
    if (!r.totalItems) return 'No matching events';
    const start = (r.page - 1) * r.pageSize + 1;
    return `Showing ${start}–${Math.min(start + r.items.length - 1, r.totalItems)} of ${r.totalItems}`;
  }
  categoryLabel(value: string): string {
    return (
      (
        {
          access: 'Access & people',
          portfolio: 'Portfolio',
          finance: 'Finance',
          operations: 'Operations',
        } as Record<string, string>
      )[value] ?? value
    );
  }
  roleLabel(value?: string): string {
    return value ? value.replace(/([a-z])([A-Z])/g, '$1 $2') : 'User unavailable';
  }
  initials(value: string): string {
    return value
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }
  relativeTime(value: string): string {
    const seconds = Math.floor((Date.now() - new Date(value).getTime()) / 1000);
    if (seconds < 60) return 'just now';
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
    if (seconds < 604800) return `${Math.floor(seconds / 86400)}d ago`;
    return `${Math.floor(seconds / 604800)}w ago`;
  }

  detailEntries(log: AuditLog): { label: string; value: string }[] {
    const groups: [string, Record<string, unknown> | undefined][] = [
      ['Previous', log.previousValues],
      ['New', log.newValues],
      ['', log.metadata],
    ];
    return groups.flatMap(([prefix, values]) =>
      Object.entries(values ?? {})
        .filter(([key, value]) => value !== null && value !== '' && !key.endsWith('Id'))
        .map(([key, value]) => ({
          label: `${prefix ? prefix + ' ' : ''}${this.humanize(key)}`,
          value: this.displayValue(value),
        })),
    );
  }

  private query(page: number): AuditLogQuery {
    const value = this.filters.getRawValue();
    return {
      ...value,
      page,
      pageSize: 25,
      from: value.from ? `${value.from}T00:00:00.000Z` : undefined,
      to: value.to ? `${value.to}T23:59:59.999Z` : undefined,
    };
  }
  private humanize(value: string): string {
    const text = value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[._-]/g, ' ');
    return text.charAt(0).toUpperCase() + text.slice(1);
  }
  private displayValue(value: unknown): string {
    if (typeof value === 'boolean') return value ? 'Yes' : 'No';
    if (Array.isArray(value)) return value.join(', ');
    if (typeof value === 'object') return JSON.stringify(value);
    return String(value);
  }
}
