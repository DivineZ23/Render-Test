import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, combineLatest, EMPTY, startWith, Subject, switchMap } from 'rxjs';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  LucideArrowLeft,
  LucideBadgeDollarSign,
  LucideBellRing,
  LucideCalendarCheck,
  LucideHouse,
  LucideTrophy,
  LucideUserX,
  LucideWalletCards,
} from '@lucide/angular';
import { AgentAnalyticsDetail } from '../../core/models/management.models';
import { DashboardService } from '../../core/services/management.services';

type Period = '30' | '90' | '365' | 'all';
type Activity = 'bookings' | 'sales' | 'commissions' | 'evictions' | 'notices';

@Component({
  selector: 'app-agent-analytics-detail',
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    RouterLink,
    LucideArrowLeft,
    LucideBadgeDollarSign,
    LucideBellRing,
    LucideCalendarCheck,
    LucideHouse,
    LucideTrophy,
    LucideUserX,
    LucideWalletCards,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="back" routerLink="/dashboard/agent-analytics">
      <svg lucideArrowLeft></svg> Back to Agent Analytics
    </a>

    @if (loading()) {
      <div class="panel state">Loading agent analysis…</div>
    } @else if (error()) {
      <div class="panel state error-state">
        <b>Agent analysis could not be loaded.</b>
        <span>{{ error() }}</span>
        <button type="button" (click)="load()">Try again</button>
      </div>
    } @else if (visibleDetail(); as report) {
      <header class="panel profile-header">
        <div class="identity">
          <div class="avatar">
            @if (report.summary.avatarUrl) {
              <img [src]="report.summary.avatarUrl" alt="" />
            } @else {
              <span>{{ initials(report.summary.displayName) }}</span>
            }
          </div>
          <div>
            <p class="eyebrow">Individual performance analysis</p>
            <h1>{{ report.summary.displayName }}</h1>
            <span class="role">{{ roleLabel(report.summary.role) }}</span>
          </div>
        </div>
        <div class="profile-actions">
          <div class="periods" aria-label="Analytics period">
            @for (option of periodOptions; track option.value) {
              <button
                type="button"
                [class.active]="period() === option.value"
                (click)="setPeriod(option.value)"
              >
                {{ option.label }}
              </button>
            }
          </div>
          <div class="score">
            <svg lucideTrophy></svg>
            <div>
              <strong>{{ report.summary.performanceScore | number: '1.0-1' }}</strong>
              <span>team-relative score</span>
            </div>
          </div>
        </div>
      </header>

      <section class="metrics" aria-label="Agent totals">
        <article class="panel metric">
          <svg lucideCalendarCheck></svg>
          <div>
            <small>Bookings</small><strong>{{ report.summary.bookings }}</strong>
          </div>
          <span>{{ report.summary.bookingAmount | currency: 'USD' : 'symbol' : '1.0-0' }}</span>
        </article>
        <article class="panel metric">
          <svg lucideHouse></svg>
          <div>
            <small>Houses sold</small><strong>{{ report.summary.housesSold }}</strong>
          </div>
          <span>{{ report.summary.conversionRate | number: '1.0-1' }}% conversion</span>
        </article>
        <article class="panel metric positive">
          <svg lucideBadgeDollarSign></svg>
          <div>
            <small>Commission taken</small
            ><strong>{{
              report.summary.commissionTaken | currency: 'USD' : 'symbol' : '1.0-0'
            }}</strong>
          </div>
          <span>Paid</span>
        </article>
        <article class="panel metric warning">
          <svg lucideWalletCards></svg>
          <div>
            <small>Commission owed</small
            ><strong>{{
              report.summary.commissionOwed | currency: 'USD' : 'symbol' : '1.0-0'
            }}</strong>
          </div>
          <span>Outstanding</span>
        </article>
        <article class="panel metric">
          <svg lucideUserX></svg>
          <div>
            <small>Evictions</small><strong>{{ report.summary.housesEvicted }}</strong>
          </div>
          <span>Completed</span>
        </article>
        <article class="panel metric">
          <svg lucideBellRing></svg>
          <div>
            <small>Notices sent</small><strong>{{ report.summary.noticesSent }}</strong>
          </div>
          <span>Overdue + eviction</span>
        </article>
      </section>

      <section class="activity-section">
        <div class="section-heading">
          <div>
            <p class="eyebrow">Sales pipeline</p>
            <h2>Booking history</h2>
          </div>
          <span
            >Showing {{ report.bookings.length }} of {{ detail()!.bookings.length }} records</span
          >
        </div>
        <div class="panel table-wrap">
          <table>
            <thead>
              <tr>
                <th>Property</th>
                <th>Client</th>
                <th>Booking amount</th>
                <th>Monthly rent</th>
                <th>Status</th>
                <th>Date</th>
              </tr>
            </thead>
            <tbody>
              @for (item of report.bookings; track item.id) {
                <tr>
                  <td>
                    <strong>{{ item.propertyName }}</strong>
                  </td>
                  <td>{{ item.clientName }}</td>
                  <td>{{ item.bookingAmount | currency: 'USD' : 'symbol' : '1.0-0' }}</td>
                  <td>{{ item.monthlyRent | currency: 'USD' : 'symbol' : '1.0-0' }}</td>
                  <td>
                    <span class="status" [class]="'status ' + item.status">{{ item.status }}</span>
                  </td>
                  <td>{{ item.createdAt | date: 'mediumDate' }}</td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="6" class="empty">No bookings in this period.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        @if (report.bookings.length < detail()!.bookings.length) {
          <button class="btn btn-secondary" (click)="loadMore('bookings')">
            Show more bookings
          </button>
        }
      </section>

      <div class="two-column">
        <section class="activity-section">
          <div class="section-heading">
            <div>
              <p class="eyebrow">Closings</p>
              <h2>Houses sold</h2>
            </div>
            <span>Showing {{ report.sales.length }} of {{ detail()!.sales.length }} records</span>
          </div>
          <div class="panel activity-list">
            @for (item of report.sales; track item.id) {
              <article>
                <div>
                  <strong>{{ item.propertyName }}</strong
                  ><span>{{ item.tenantName }}</span>
                </div>
                <div>
                  <strong>{{ item.monthlyRent | currency: 'USD' : 'symbol' : '1.0-0' }}</strong
                  ><time>{{ item.soldAt | date: 'mediumDate' }}</time>
                </div>
              </article>
            } @empty {
              <p class="empty">No completed sales in this period.</p>
            }
          </div>
          @if (report.sales.length < detail()!.sales.length) {
            <button class="btn btn-secondary" (click)="loadMore('sales')">Show more sales</button>
          }
        </section>

        <section class="activity-section">
          <div class="section-heading">
            <div>
              <p class="eyebrow">Earnings</p>
              <h2>Commission ledger</h2>
            </div>
            <span
              >Showing {{ report.commissions.length }} of
              {{ detail()!.commissions.length }} records</span
            >
          </div>
          <div class="panel activity-list">
            @for (item of report.commissions; track item.id) {
              <article>
                <div>
                  <strong>{{ item.auctionReference }}</strong
                  ><span>{{ item.isWinningAgent ? 'Winning agent' : 'Participating agent' }}</span>
                </div>
                <div>
                  <strong>{{ item.amount | currency: 'USD' : 'symbol' : '1.0-0' }}</strong
                  ><span class="payment" [class.paid]="item.isPaid">{{
                    item.isPaid ? 'Paid' : 'Owed'
                  }}</span>
                </div>
              </article>
            } @empty {
              <p class="empty">No commission records in this period.</p>
            }
          </div>
          @if (report.commissions.length < detail()!.commissions.length) {
            <button class="btn btn-secondary" (click)="loadMore('commissions')">
              Show more commissions
            </button>
          }
        </section>
      </div>

      <div class="two-column">
        <section class="activity-section">
          <div class="section-heading">
            <div>
              <p class="eyebrow">Operations</p>
              <h2>Evictions</h2>
            </div>
            <span
              >Showing {{ report.evictions.length }} of
              {{ detail()!.evictions.length }} records</span
            >
          </div>
          <div class="panel activity-list">
            @for (item of report.evictions; track item.id) {
              <article>
                <div>
                  <strong>{{ item.propertyName }}</strong
                  ><span>{{ item.tenantName }} · {{ item.reason }}</span>
                </div>
                <time>{{ item.evictedAt | date: 'mediumDate' }}</time>
              </article>
            } @empty {
              <p class="empty">No evictions in this period.</p>
            }
          </div>
          @if (report.evictions.length < detail()!.evictions.length) {
            <button class="btn btn-secondary" (click)="loadMore('evictions')">
              Show more evictions
            </button>
          }
        </section>

        <section class="activity-section">
          <div class="section-heading">
            <div>
              <p class="eyebrow">Communication</p>
              <h2>Notices sent</h2>
            </div>
            <span
              >Showing {{ report.notices.length }} of {{ detail()!.notices.length }} records</span
            >
          </div>
          <div class="panel activity-list">
            @for (item of report.notices; track item.id) {
              <article>
                <div>
                  <strong>{{ item.address }}</strong
                  ><span
                    >{{ item.renterName || 'Unknown renter' }} ·
                    {{ item.status === 'evictable' ? 'Eviction' : 'Overdue' }}</span
                  >
                </div>
                <div>
                  <strong>{{ item.rent | currency: 'USD' : 'symbol' : '1.0-0' }}</strong
                  ><time>{{ item.sentAt | date: 'mediumDate' }}</time>
                </div>
              </article>
            } @empty {
              <p class="empty">No notices sent in this period.</p>
            }
          </div>
          @if (report.notices.length < detail()!.notices.length) {
            <button class="btn btn-secondary" (click)="loadMore('notices')">
              Show more notices
            </button>
          }
        </section>
      </div>
    }
  `,
  styles: [
    `
      .back {
        display: inline-flex;
        align-items: center;
        gap: 7px;
        margin-bottom: 14px;
        color: var(--muted);
        font-size: 0.74rem;
        font-weight: 700;
      }
      .back:hover {
        color: var(--forest);
      }
      .back svg {
        width: 16px;
      }
      .profile-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 24px;
        padding: 22px;
        margin-bottom: 14px;
      }
      .identity,
      .profile-actions,
      .score {
        display: flex;
        align-items: center;
        gap: 14px;
      }
      .avatar {
        width: 66px;
        height: 66px;
        display: grid;
        place-items: center;
        overflow: hidden;
        border-radius: 50%;
        background: var(--forest-light);
        color: var(--forest);
        font-size: 1rem;
        font-weight: 800;
      }
      .avatar img {
        width: 100%;
        height: 100%;
        object-fit: cover;
      }
      .identity h1 {
        margin: 3px 0;
        font-size: 1.65rem;
      }
      .role {
        color: var(--muted);
        font-size: 0.72rem;
      }
      .periods {
        display: flex;
        gap: 3px;
        padding: 4px;
        border: 1px solid var(--border);
        border-radius: 9px;
      }
      .periods button {
        border: 0;
        border-radius: 6px;
        background: transparent;
        color: var(--muted);
        padding: 7px 10px;
        font-size: 0.67rem;
        font-weight: 700;
        cursor: pointer;
      }
      .periods button.active {
        background: var(--forest);
        color: white;
      }
      .score {
        padding-left: 15px;
        border-left: 1px solid var(--border);
      }
      .score > svg {
        width: 23px;
        color: var(--forest);
      }
      .score div {
        display: grid;
      }
      .score strong {
        font-size: 1.4rem;
      }
      .score span {
        color: var(--muted);
        font-size: 0.62rem;
      }
      .metrics {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: 11px;
      }
      .metric {
        display: grid;
        grid-template-columns: auto 1fr auto;
        align-items: center;
        gap: 12px;
        padding: 16px;
      }
      .metric > svg {
        width: 20px;
        color: var(--forest);
      }
      .metric div {
        display: grid;
        gap: 2px;
      }
      .metric small,
      .metric > span {
        color: var(--muted);
        font-size: 0.65rem;
      }
      .metric strong {
        font-size: 1.05rem;
      }
      .metric.warning strong {
        color: var(--warning-ink);
      }
      .metric.positive strong {
        color: var(--forest);
      }
      .activity-section {
        margin-top: 24px;
      }
      .section-heading {
        display: flex;
        align-items: flex-end;
        justify-content: space-between;
        gap: 15px;
        margin-bottom: 9px;
      }
      .section-heading h2 {
        margin: 2px 0 0;
        font-size: 1.05rem;
      }
      .section-heading > span {
        color: var(--muted);
        font-size: 0.66rem;
      }
      .table-wrap {
        overflow-x: auto;
      }
      table {
        width: 100%;
        min-width: 850px;
        border-collapse: collapse;
      }
      th {
        padding: 10px 14px;
        border-bottom: 1px solid var(--border);
        color: var(--muted);
        text-align: left;
        font-size: 0.61rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }
      td {
        padding: 13px 14px;
        border-bottom: 1px solid var(--border);
        font-size: 0.71rem;
      }
      tbody tr:last-child td {
        border-bottom: 0;
      }
      .status,
      .payment {
        display: inline-flex;
        padding: 4px 7px;
        border-radius: 99px;
        background: var(--surface-subtle);
        color: var(--muted);
        font-size: 0.62rem;
        font-weight: 750;
        text-transform: capitalize;
      }
      .status.converted,
      .payment.paid {
        background: var(--forest-light);
        color: var(--forest);
      }
      .status.cancelled,
      .payment:not(.paid) {
        background: var(--warning-soft);
        color: var(--warning-ink);
      }
      .two-column {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 14px;
      }
      .activity-list {
        overflow: hidden;
      }
      .activity-list article {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 14px;
        padding: 14px 16px;
        border-bottom: 1px solid var(--border);
        font-size: 0.71rem;
      }
      .activity-list article:last-child {
        border-bottom: 0;
      }
      .activity-list article > div {
        display: grid;
        gap: 4px;
      }
      .activity-list article > div:last-child {
        text-align: right;
      }
      .activity-list span,
      .activity-list time {
        color: var(--muted);
        font-size: 0.64rem;
      }
      .empty,
      .state {
        padding: 32px;
        text-align: center;
        color: var(--muted);
      }
      .error-state {
        display: grid;
        justify-items: center;
        gap: 8px;
        color: var(--danger);
      }
      .error-state span {
        color: var(--muted);
      }
      .error-state button {
        border: 0;
        border-radius: 7px;
        background: var(--forest);
        color: white;
        padding: 8px 12px;
        cursor: pointer;
      }
      @media (max-width: 1100px) {
        .profile-header,
        .profile-actions {
          align-items: flex-start;
          flex-direction: column;
        }
        .score {
          padding-left: 0;
          border-left: 0;
        }
        .metrics {
          grid-template-columns: repeat(2, 1fr);
        }
      }
      @media (max-width: 760px) {
        .metrics,
        .two-column {
          grid-template-columns: 1fr;
        }
        .metric {
          grid-template-columns: auto 1fr;
        }
        .metric > span {
          grid-column: 2;
        }
        .periods {
          max-width: 100%;
          overflow-x: auto;
        }
        .periods button {
          white-space: nowrap;
        }
        .activity-list article {
          align-items: flex-start;
        }
      }
    `,
  ],
})
export class AgentAnalyticsDetailComponent {
  private readonly service = inject(DashboardService);
  private readonly route = inject(ActivatedRoute);
  readonly detail = signal<AgentAnalyticsDetail | null>(null);
  private readonly limits = signal<Record<Activity, number>>({
    bookings: 50,
    sales: 50,
    commissions: 50,
    evictions: 50,
    notices: 50,
  });
  readonly visibleDetail = computed(() => {
    const report = this.detail();
    if (!report) return null;
    const limits = this.limits();
    return {
      ...report,
      bookings: report.bookings.slice(0, limits.bookings),
      sales: report.sales.slice(0, limits.sales),
      commissions: report.commissions.slice(0, limits.commissions),
      evictions: report.evictions.slice(0, limits.evictions),
      notices: report.notices.slice(0, limits.notices),
    };
  });
  loadMore(activity: Activity) {
    this.limits.update((value) => ({ ...value, [activity]: value[activity] + 50 }));
  }
  readonly loading = signal(true);
  readonly error = signal('');
  readonly period = signal<Period>('all');
  readonly periodOptions: ReadonlyArray<{ value: Period; label: string }> = [
    { value: '30', label: '30 days' },
    { value: '90', label: '90 days' },
    { value: '365', label: '1 year' },
    { value: 'all', label: 'All time' },
  ];

  private readonly reload$ = new Subject<void>();
  private readonly destroyRef = inject(DestroyRef);
  constructor() {
    combineLatest([this.route.paramMap, this.reload$.pipe(startWith(undefined))])
      .pipe(
        switchMap(([params]) => {
          this.loading.set(true);
          this.error.set('');
          const days = this.period() === 'all' ? null : Number(this.period());
          const to = days ? new Date() : undefined;
          const from = days ? new Date(to!.getTime() - days * 24 * 60 * 60 * 1000) : undefined;
          return this.service
            .agent(params.get('id') ?? '', from?.toISOString(), to?.toISOString())
            .pipe(
              catchError((response) => {
                this.error.set(response?.error?.message || 'Please try again in a moment.');
                this.loading.set(false);
                return EMPTY;
              }),
            );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((report) => {
        this.limits.set({ bookings: 50, sales: 50, commissions: 50, evictions: 50, notices: 50 });
        this.detail.set(report);
        this.loading.set(false);
      });
  }
  load() {
    this.reload$.next();
  }

  setPeriod(value: Period) {
    if (this.period() === value) return;
    this.period.set(value);
    this.load();
  }

  initials(name: string) {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('');
  }

  roleLabel(role: AgentAnalyticsDetail['summary']['role']) {
    if (role === 'seniorAgent') return 'Senior Agent';
    if (role === 'manager') return 'Manager';
    return 'Agent';
  }
}
