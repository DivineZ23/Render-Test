import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { LucideArrowRight } from '@lucide/angular';
import { catchError, of, startWith, Subject, switchMap } from 'rxjs';
import { DashboardSummary } from '../../core/models/management.models';
import { DashboardService } from '../../core/services/management.services';
import { PageAccessService } from '../../core/services/page-access.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-dashboard-overview',
  imports: [CurrencyPipe, DatePipe, RouterLink, StatusBadgeComponent, LucideArrowRight],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page-title">
      <div>
        <p class="eyebrow">Portfolio pulse</p>
        <h1>Overview</h1>
        <p>Live operational status across Imperial Estates.</p>
      </div>
    </div>
    @if (summary(); as s) {
      <div class="financial-stats">
        <article class="panel">
          <span>Total revenue</span>
          <b>{{ s.totalRevenue | currency: 'USD' : 'symbol' : '1.0-0' }}</b>
          <small>Latest synced income · listing rent before first sync</small>
        </article>
        <article class="panel">
          <span>Total cost</span>
          <b>{{ s.totalCost | currency: 'USD' : 'symbol' : '1.0-0' }}</b>
          <small>Fixed State cost by interior</small>
        </article>
        <article class="panel profit" [class.negative]="s.totalProfit < 0">
          <span>Total profit</span>
          <b>{{ s.totalProfit | currency: 'USD' : 'symbol' : '1.0-0' }}</b>
          <small
            >Rent − Cost · Avg
            {{ s.averageProfitPerProperty | currency: 'USD' : 'symbol' : '1.0-0' }}</small
          >
        </article>
        <article class="panel block-highlight">
          <span>Most profitable block</span>
          <b>{{ s.mostProfitableBlock || 'No block data' }}</b>
          <small
            >{{ s.mostProfitableBlockProfit | currency: 'USD' : 'symbol' : '1.0-0' }} profit</small
          >
        </article>
      </div>
      <div class="stats">
        <article>
          <span>Total properties</span><b>{{ s.totalProperties }}</b
          ><small>Across {{ s.totalBlocks }} blocks</small>
        </article>
        <article class="available">
          <span>Available</span><b>{{ s.availableProperties }}</b
          ><small>Open for enquiries</small>
        </article>
        <article>
          <span>Booked</span><b>{{ s.bookedProperties }}</b
          ><small>Awaiting tenancy</small>
        </article>
        <article>
          <span>Occupied</span><b>{{ s.occupiedProperties }}</b
          ><small>Active tenancies</small>
        </article>
      </div>
      <div class="dashboard-grid">
        <section class="panel activity">
          <div class="panel-head">
            <div>
              <h2>Recent status changes</h2>
              <p>Latest movement across the portfolio</p>
            </div>
            @if (access.canAccess('portfolio.properties')) {
              <a routerLink="/dashboard/properties">View properties</a>
            }
          </div>
          @for (item of s.recentStatusChanges; track item.id) {
            <div class="event">
              <span class="dot"></span>
              <div>
                <b>Property status updated</b>
                <p>
                  <app-status-badge [status]="item.previousStatus" />
                  <svg class="status-arrow" lucideArrowRight></svg>
                  <app-status-badge [status]="item.newStatus" />
                </p>
              </div>
              <time>{{ item.createdAt | date: 'mediumDate' }}</time>
            </div>
          } @empty {
            <p class="empty">No status changes yet.</p>
          }
        </section>
        <aside>
          <section class="panel actions">
            <h2>Team access</h2>
            @if (access.canAccess('administration.users')) {
              @if (s.pendingUsers > 0) {
                <a routerLink="/dashboard/users"
                  ><span>Pending approvals</span><b>{{ s.pendingUsers }}</b></a
                >
              } @else {
                <p class="empty-attention">No pending access requests.</p>
              }
            } @else {
              <p class="empty-attention">Access requests are managed by administrators.</p>
            }
          </section>
          <section class="panel occupancy">
            <div class="ring" [style.--pct]="occupancyPercent(s)">
              <span>{{ occupancyPercent(s) }}%</span>
            </div>
            <div>
              <h2>Occupancy</h2>
              <p>Occupied properties as a share of the active portfolio.</p>
            </div>
          </section>
        </aside>
      </div>
    } @else if (error()) {
      <div class="loading" role="alert">
        Could not load the portfolio.
        <button class="btn btn-secondary" (click)="reload()">Try again</button>
      </div>
    } @else {
      <div class="financial-stats" aria-busy="true" aria-label="Loading portfolio">
        @for (item of skeletons; track item) {
          <article class="panel skeleton"><span></span><b></b><small></small></article>
        }
      </div>
    }`,
  styles: [
    `
      .page-title {
        display: flex;
        justify-content: space-between;
        align-items: end;
        margin-bottom: 28px;
      }
      .page-title h1 {
        font-size: 2.6rem;
        margin: 5px 0;
      }
      .page-title p:last-child {
        color: var(--muted);
        margin: 0;
      }
      .stats {
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 14px;
        margin-bottom: 20px;
      }
      .financial-stats {
        display: grid;
        grid-template-columns: repeat(4, minmax(0, 1fr));
        gap: 14px;
        margin-bottom: 14px;
      }
      .financial-stats article {
        display: grid;
        align-content: start;
        gap: 7px;
        min-width: 0;
        min-height: 124px;
        padding: 18px;
      }
      .financial-stats span,
      .financial-stats small {
        color: var(--muted);
        font-size: 0.75rem;
        line-height: 1.35;
      }
      .financial-stats b {
        margin: 2px 0;
        overflow: visible;
        font-size: clamp(1.25rem, 2vw, 1.9rem);
        font-variant-numeric: tabular-nums;
        line-height: 1.12;
        overflow-wrap: anywhere;
        text-overflow: clip;
        white-space: normal;
      }
      .financial-stats .profit {
        border-left: 3px solid var(--forest);
      }
      .financial-stats .negative {
        border-color: var(--danger);
        background: var(--danger-soft);
      }
      .financial-stats .negative b {
        color: var(--danger);
      }
      .block-highlight b {
        font-size: clamp(1.05rem, 1.45vw, 1.3rem);
      }
      .stats article {
        background: var(--surface);
        border: 1px solid var(--border);
        border-radius: var(--radius-md);
        padding: 17px;
        display: grid;
      }
      .stats span,
      .stats small {
        color: var(--muted);
        font-size: 0.75rem;
      }
      .stats b {
        font-size: 2rem;
        margin: 10px 0;
      }
      .stats .available {
        border-left: 3px solid var(--forest);
        background: var(--surface);
        color: var(--ink);
      }
      .stats .available span,
      .stats .available small {
        color: var(--muted);
      }
      .stats .available b {
        color: var(--forest);
      }
      .dashboard-grid {
        display: grid;
        grid-template-columns: 1.4fr 0.6fr;
        gap: 20px;
      }
      .activity {
        padding: 20px;
      }
      .panel-head {
        display: flex;
        justify-content: space-between;
        border-bottom: 1px solid var(--border);
        padding-bottom: 16px;
      }
      .panel-head h2,
      .actions h2 {
        font-size: 1.2rem;
        margin-bottom: 4px;
      }
      .panel-head p {
        color: var(--muted);
        font-size: 0.78rem;
        margin: 0;
      }
      .panel-head a {
        font-size: 0.78rem;
        font-weight: 700;
      }
      .event {
        display: grid;
        grid-template-columns: 12px 1fr auto;
        gap: 12px;
        align-items: start;
        padding: 18px 0;
        border-bottom: 1px solid var(--border);
      }
      .dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: var(--bronze);
        margin-top: 7px;
      }
      .event p {
        display: flex;
        align-items: center;
        gap: 7px;
        margin: 7px 0 0;
      }
      .status-arrow {
        width: 15px;
        height: 15px;
        flex: 0 0 auto;
        color: var(--muted);
      }
      .event time {
        color: var(--muted);
        font-size: 0.75rem;
      }
      .empty {
        color: var(--muted);
        padding: 30px 0;
      }
      aside {
        display: grid;
        gap: 20px;
      }
      .actions {
        padding: 20px;
      }
      .actions a {
        display: flex;
        justify-content: space-between;
        padding: 15px 0;
        border-bottom: 1px solid var(--border);
        font-size: 0.85rem;
      }
      .actions a b {
        display: grid;
        place-items: center;
        min-width: 28px;
        height: 28px;
        border-radius: 50%;
        background: var(--forest-light);
        color: var(--forest);
      }
      .empty-attention {
        margin: 18px 0 0;
        color: var(--muted);
        font-size: 0.8rem;
      }
      .occupancy {
        padding: 20px;
        display: flex;
        align-items: center;
        gap: 20px;
      }
      .ring {
        --pct: 0;
        width: 88px;
        height: 88px;
        flex: 0 0 auto;
        border-radius: 50%;
        background: conic-gradient(var(--forest) calc(var(--pct) * 1%), var(--forest-light) 0);
        display: grid;
        place-items: center;
        position: relative;
      }
      .ring:after {
        content: '';
        position: absolute;
        width: 66px;
        height: 66px;
        border-radius: 50%;
        background: var(--surface);
      }
      .ring span {
        z-index: 1;
        font-weight: 800;
      }
      .occupancy h2 {
        font-size: 1.1rem;
        margin-bottom: 5px;
      }
      .occupancy p {
        color: var(--muted);
        font-size: 0.75rem;
        margin: 0;
      }
      .loading {
        padding: 80px;
        text-align: center;
        color: var(--muted);
      }
      .skeleton {
        min-height: 138px;
      }
      .skeleton span,
      .skeleton b,
      .skeleton small {
        display: block;
        height: 16px;
        width: 65%;
        border-radius: 6px;
        background: var(--skeleton-start);
      }
      .skeleton b {
        height: 36px;
        width: 85%;
        margin: 14px 0;
      }
      @media (max-width: 1100px) {
        .financial-stats {
          grid-template-columns: 1fr 1fr;
        }
        .stats {
          grid-template-columns: repeat(3, 1fr);
        }
        .dashboard-grid {
          grid-template-columns: 1fr;
        }
      }
      @media (max-width: 650px) {
        .page-title {
          align-items: start;
          gap: 20px;
        }
        .page-title .btn {
          display: none;
        }
        .financial-stats {
          grid-template-columns: 1fr;
        }
        .stats {
          grid-template-columns: 1fr 1fr;
        }
        .stats article:first-child {
          grid-column: 1/-1;
        }
        .event {
          grid-template-columns: 12px 1fr;
        }
        .event time {
          display: none;
        }
      }
    `,
  ],
})
export class DashboardOverviewComponent {
  private service = inject(DashboardService);
  readonly access = inject(PageAccessService);
  readonly error = signal(false);
  readonly skeletons = [1, 2, 3, 4, 5, 6, 7, 8];
  private readonly reload$ = new Subject<void>();
  readonly summary = toSignal(
    this.reload$.pipe(
      startWith(undefined),
      switchMap(() => {
        this.error.set(false);
        return this.service.get().pipe(
          catchError(() => {
            this.error.set(true);
            return of(null);
          }),
        );
      }),
    ),
    {
      initialValue: null,
    },
  );
  reload() {
    this.reload$.next();
  }
  occupancyPercent(s: DashboardSummary) {
    return s.totalProperties ? Math.round((100 * s.occupiedProperties) / s.totalProperties) : 0;
  }
}
