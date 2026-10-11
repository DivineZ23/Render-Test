import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  LucideBadgeDollarSign,
  LucideBellRing,
  LucideCalendarCheck,
  LucideDownload,
  LucideHouse,
  LucideSearch,
  LucideTrophy,
  LucideUserX,
  LucideUsersRound,
  LucideWalletCards,
} from '@lucide/angular';
import { AgentPerformance } from '../../core/models/management.models';
import { DashboardService } from '../../core/services/management.services';

type Period = '30' | '90' | '365' | 'all';
type RoleFilter = 'all' | 'agent' | 'seniorAgent';
type SortField =
  | 'performanceScore'
  | 'housesSold'
  | 'bookings'
  | 'bookingAmount'
  | 'commissionTaken'
  | 'commissionOwed';

@Component({
  selector: 'app-agent-analytics',
  imports: [
    CurrencyPipe,
    DecimalPipe,
    LucideBadgeDollarSign,
    LucideBellRing,
    LucideCalendarCheck,
    LucideDownload,
    LucideHouse,
    LucideSearch,
    LucideTrophy,
    LucideUserX,
    LucideUsersRound,
    LucideWalletCards,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-title">
      <div>
        <p class="eyebrow">Management intelligence</p>
        <h1>Agent Analytics</h1>
        <p>Compare bookings, sales, commissions, and operational performance across the team.</p>
      </div>
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
    </div>

    @if (loading()) {
      <div class="panel state">Loading agent performance…</div>
    } @else if (error()) {
      <div class="panel state error-state">
        <b>Agent analytics could not be loaded.</b>
        <span>{{ error() }}</span>
        <button type="button" (click)="load()">Try again</button>
      </div>
    } @else if (analytics(); as report) {
      <section class="metrics" aria-label="Team totals">
        <article class="panel metric">
          <span class="metric-icon"><svg lucideUsersRound></svg></span>
          <div><small>Agents</small><strong>{{ report.totals.agents }}</strong></div>
        </article>
        <article class="panel metric">
          <span class="metric-icon"><svg lucideCalendarCheck></svg></span>
          <div><small>Bookings</small><strong>{{ report.totals.bookings }}</strong></div>
        </article>
        <article class="panel metric">
          <span class="metric-icon"><svg lucideWalletCards></svg></span>
          <div>
            <small>Booking amount</small>
            <strong>{{ report.totals.bookingAmount | currency: 'USD' : 'symbol' : '1.0-0' }}</strong>
          </div>
        </article>
        <article class="panel metric">
          <span class="metric-icon"><svg lucideHouse></svg></span>
          <div><small>Houses sold</small><strong>{{ report.totals.housesSold }}</strong></div>
        </article>
        <article class="panel metric positive">
          <span class="metric-icon"><svg lucideBadgeDollarSign></svg></span>
          <div>
            <small>Commission taken</small>
            <strong>{{ report.totals.commissionTaken | currency: 'USD' : 'symbol' : '1.0-0' }}</strong>
          </div>
        </article>
        <article class="panel metric warning">
          <span class="metric-icon"><svg lucideBadgeDollarSign></svg></span>
          <div>
            <small>Commission owed</small>
            <strong>{{ report.totals.commissionOwed | currency: 'USD' : 'symbol' : '1.0-0' }}</strong>
          </div>
        </article>
        <article class="panel metric">
          <span class="metric-icon"><svg lucideUserX></svg></span>
          <div><small>Houses evicted</small><strong>{{ report.totals.housesEvicted }}</strong></div>
        </article>
        <article class="panel metric">
          <span class="metric-icon"><svg lucideBellRing></svg></span>
          <div><small>Notices sent</small><strong>{{ report.totals.noticesSent }}</strong></div>
        </article>
      </section>

      @if (topAgent(); as leader) {
        <section class="panel leader">
          <span class="trophy"><svg lucideTrophy></svg></span>
          <div class="leader-copy">
            <small>Top performer · {{ periodLabel() }}</small>
            <h2>{{ leader.displayName }}</h2>
            <p>
              {{ leader.housesSold }} houses sold · {{ leader.bookings }} bookings ·
              {{ leader.commissionTaken | currency: 'USD' : 'symbol' : '1.0-0' }} commission received
            </p>
          </div>
          <div class="leader-score">
            <strong>{{ leader.performanceScore | number: '1.0-1' }}</strong>
            <span>performance score</span>
          </div>
        </section>
      }

      <section class="performance-section">
        <div class="section-heading">
          <div>
            <h2>Agent performance</h2>
            <p>
              Score is relative to the strongest team result in this period, weighted toward sales and paid commission.
            </p>
          </div>
          <button type="button" class="export" (click)="exportCsv()" [disabled]="filteredAgents().length === 0">
            <svg lucideDownload></svg> Export CSV
          </button>
        </div>

        <div class="panel filters">
          <label class="search">
            <svg lucideSearch></svg>
            <input
              type="search"
              placeholder="Search agent"
              aria-label="Search agents"
              [value]="search()"
              (input)="setSearch($event)"
            />
          </label>
          <label>
            <span>Role</span>
            <select [value]="role()" (change)="setRole($event)">
              <option value="all">All agents</option>
              <option value="agent">Agent</option>
              <option value="seniorAgent">Senior agent</option>
            </select>
          </label>
          <label>
            <span>Sort by</span>
            <select [value]="sort()" (change)="setSort($event)">
              <option value="performanceScore">Performance score</option>
              <option value="housesSold">Houses sold</option>
              <option value="bookings">Bookings</option>
              <option value="bookingAmount">Booking amount</option>
              <option value="commissionTaken">Commission taken</option>
              <option value="commissionOwed">Commission owed</option>
            </select>
          </label>
          <div class="result-count">
            <strong>{{ filteredAgents().length }}</strong>
            <span>agents shown</span>
          </div>
        </div>

        <div class="panel table-wrap">
          <table>
            <thead>
              <tr>
                <th>Agent</th>
                <th>Performance</th>
                <th>Bookings</th>
                <th>Houses sold</th>
                <th>Commission</th>
                <th>Operations</th>
              </tr>
            </thead>
            <tbody>
              @for (agent of filteredAgents(); track agent.userId; let rank = $index) {
                <tr>
                  <td>
                    <div class="agent-cell">
                      <div class="avatar">
                        @if (agent.avatarUrl) {
                          <img [src]="agent.avatarUrl" alt="" />
                        } @else {
                          <span>{{ initials(agent.displayName) }}</span>
                        }
                      </div>
                      <div>
                        <strong>{{ agent.displayName }}</strong>
                        <small>{{ agent.role === 'seniorAgent' ? 'Senior Agent' : 'Agent' }}</small>
                      </div>
                    </div>
                  </td>
                  <td>
                    <div class="score-line">
                      <strong>#{{ rank + 1 }}</strong>
                      <span>{{ agent.performanceScore | number: '1.0-1' }}/100</span>
                    </div>
                    <div class="score-track" aria-hidden="true">
                      <span [style.width.%]="agent.performanceScore"></span>
                    </div>
                  </td>
                  <td>
                    <strong>{{ agent.bookings }}</strong>
                    <small>{{ agent.bookingAmount | currency: 'USD' : 'symbol' : '1.0-0' }} booked</small>
                  </td>
                  <td>
                    <strong>{{ agent.housesSold }}</strong>
                    <small>{{ agent.convertedBookings }} converted · {{ agent.conversionRate | number: '1.0-1' }}%</small>
                  </td>
                  <td>
                    <strong class="received">{{ agent.commissionTaken | currency: 'USD' : 'symbol' : '1.0-0' }} received</strong>
                    <small class="owed">{{ agent.commissionOwed | currency: 'USD' : 'symbol' : '1.0-0' }} owed</small>
                  </td>
                  <td>
                    <strong>{{ agent.housesEvicted }} evictions</strong>
                    <small>{{ agent.noticesSent }} notices sent</small>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="empty">No agents match the selected filters.</td></tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    }
  `,
  styles: [
    `
      .page-title,.section-heading{display:flex;align-items:flex-start;justify-content:space-between;gap:24px}.page-title{margin-bottom:22px}.page-title h1{font-size:2.35rem;margin:4px 0}.page-title p:last-child,.section-heading p{color:var(--muted);margin:0;max-width:720px}.periods{display:flex;gap:4px;padding:4px;border:1px solid var(--border);border-radius:10px;background:var(--surface)}.periods button{border:0;border-radius:7px;background:transparent;color:var(--muted);padding:8px 12px;cursor:pointer;font-size:.72rem;font-weight:700}.periods button.active{background:var(--forest);color:white}.metrics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-bottom:16px}.metric{display:flex;align-items:center;gap:13px;padding:17px;min-width:0}.metric-icon{width:38px;height:38px;display:grid;place-items:center;flex:0 0 auto;border-radius:10px;background:var(--forest-light);color:var(--forest)}.metric-icon svg{width:19px;height:19px}.metric div{display:grid;gap:3px;min-width:0}.metric small{color:var(--muted);font-size:.68rem}.metric strong{font-size:1.17rem;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.metric.warning .metric-icon{background:var(--warning-soft);color:var(--warning-ink)}.metric.positive strong{color:var(--forest)}.leader{display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:18px;padding:22px;margin-bottom:28px;background:linear-gradient(120deg,var(--surface),var(--forest-light))}.trophy{width:52px;height:52px;display:grid;place-items:center;border-radius:14px;background:var(--forest);color:white}.trophy svg{width:25px}.leader-copy small{color:var(--forest);font-weight:750;text-transform:uppercase;letter-spacing:.05em}.leader-copy h2{margin:4px 0;font-size:1.25rem}.leader-copy p{margin:0;color:var(--muted);font-size:.76rem}.leader-score{display:grid;text-align:right}.leader-score strong{font-size:2rem;color:var(--forest)}.leader-score span{color:var(--muted);font-size:.65rem}.section-heading{align-items:flex-end;margin-bottom:12px}.section-heading h2{margin:0 0 4px}.section-heading p{font-size:.75rem}.export{display:inline-flex;align-items:center;gap:7px;border:1px solid var(--border);border-radius:8px;background:var(--surface);color:var(--ink);padding:9px 12px;cursor:pointer;font-weight:700;font-size:.7rem}.export svg{width:15px}.export:disabled{opacity:.45;cursor:not-allowed}.filters{display:grid;grid-template-columns:minmax(220px,1fr) 180px 200px auto;align-items:end;gap:12px;padding:14px;margin-bottom:12px}.filters label{display:grid;gap:5px}.filters label>span{color:var(--muted);font-size:.65rem;font-weight:700}.filters input,.filters select{width:100%;height:39px;border:1px solid var(--border);border-radius:8px;background:var(--surface);color:var(--ink);padding:0 10px}.search{position:relative}.search svg{position:absolute;left:11px;bottom:11px;width:16px;height:16px;color:var(--muted)}.search input{padding-left:36px}.result-count{display:grid;justify-items:end;padding-bottom:3px}.result-count strong{font-size:1.05rem}.result-count span{color:var(--muted);font-size:.64rem}.table-wrap{overflow-x:auto}table{width:100%;min-width:1000px;border-collapse:collapse}th{padding:11px 15px;border-bottom:1px solid var(--border);color:var(--muted);text-align:left;font-size:.62rem;text-transform:uppercase;letter-spacing:.055em}td{padding:14px 15px;border-bottom:1px solid var(--border);vertical-align:middle;font-size:.73rem}tbody tr:last-child td{border-bottom:0}td>strong,td>small{display:block}td>small{margin-top:4px;color:var(--muted);font-size:.65rem}.agent-cell{display:flex;align-items:center;gap:10px}.agent-cell>div:last-child{display:grid;gap:3px}.agent-cell small{color:var(--muted);font-size:.65rem}.avatar{width:38px;height:38px;display:grid;place-items:center;flex:0 0 auto;border-radius:50%;overflow:hidden;background:var(--forest-light);color:var(--forest);font-size:.7rem;font-weight:800}.avatar img{width:100%;height:100%;object-fit:cover}.score-line{display:flex;justify-content:space-between;gap:15px;font-size:.68rem}.score-line span{color:var(--muted)}.score-track{width:140px;height:5px;margin-top:7px;border-radius:99px;background:var(--surface-subtle);overflow:hidden}.score-track span{display:block;height:100%;border-radius:inherit;background:var(--forest)}.received{color:var(--forest)}.owed{color:var(--warning-ink)!important}.empty,.state{text-align:center;color:var(--muted);padding:36px}.error-state{display:grid;justify-items:center;gap:8px;color:var(--danger)}.error-state span{color:var(--muted)}.error-state button{border:0;border-radius:7px;background:var(--forest);color:white;padding:8px 12px;cursor:pointer}@media(max-width:1150px){.metrics{grid-template-columns:repeat(2,minmax(0,1fr))}.filters{grid-template-columns:1fr 1fr}.search{grid-column:1/-1}.result-count{justify-items:start}}@media(max-width:700px){.page-title,.section-heading{display:grid}.periods{overflow-x:auto;max-width:100%}.periods button{white-space:nowrap}.metrics{grid-template-columns:1fr}.leader{grid-template-columns:auto 1fr}.leader-score{grid-column:1/-1;text-align:left;padding-left:70px}.filters{grid-template-columns:1fr}.search{grid-column:auto}.result-count{justify-items:start}}
    `,
  ],
})
export class AgentAnalyticsComponent {
  private readonly service = inject(DashboardService);
  readonly analytics = signal<import('../../core/models/management.models').AgentAnalytics | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly period = signal<Period>('all');
  readonly search = signal('');
  readonly role = signal<RoleFilter>('all');
  readonly sort = signal<SortField>('performanceScore');
  readonly periodOptions: ReadonlyArray<{ value: Period; label: string }> = [
    { value: '30', label: '30 days' },
    { value: '90', label: '90 days' },
    { value: '365', label: '1 year' },
    { value: 'all', label: 'All time' },
  ];
  readonly filteredAgents = computed(() => {
    const term = this.search().trim().toLocaleLowerCase();
    const role = this.role();
    const sort = this.sort();
    return [...(this.analytics()?.agents ?? [])]
      .filter(
        (agent) =>
          (role === 'all' || agent.role === role) &&
          (!term || agent.displayName.toLocaleLowerCase().includes(term)),
      )
      .sort((left, right) => right[sort] - left[sort] || left.displayName.localeCompare(right.displayName));
  });
  readonly topAgent = computed(() =>
    [...(this.analytics()?.agents ?? [])].sort(
      (left, right) => right.performanceScore - left.performanceScore,
    )[0],
  );
  readonly periodLabel = computed(
    () => this.periodOptions.find((option) => option.value === this.period())?.label ?? 'All time',
  );

  constructor() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set('');
    const days = this.period() === 'all' ? null : Number(this.period());
    const to = days ? new Date() : undefined;
    const from = days ? new Date(to!.getTime() - days * 24 * 60 * 60 * 1000) : undefined;
    this.service.agents(from?.toISOString(), to?.toISOString()).subscribe({
      next: (report) => {
        this.analytics.set(report);
        this.loading.set(false);
      },
      error: (response) => {
        this.error.set(response?.error?.message || 'Please try again in a moment.');
        this.loading.set(false);
      },
    });
  }

  setPeriod(value: Period) {
    if (this.period() === value) return;
    this.period.set(value);
    this.load();
  }

  setSearch(event: Event) {
    this.search.set((event.target as HTMLInputElement).value);
  }

  setRole(event: Event) {
    this.role.set((event.target as HTMLSelectElement).value as RoleFilter);
  }

  setSort(event: Event) {
    this.sort.set((event.target as HTMLSelectElement).value as SortField);
  }

  initials(name: string) {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('');
  }

  exportCsv() {
    const headings = [
      'Agent',
      'Role',
      'Performance Score',
      'Bookings',
      'Booking Amount',
      'Converted Bookings',
      'Houses Sold',
      'Commission Taken',
      'Commission Owed',
      'Houses Evicted',
      'Notices Sent',
      'Conversion Rate',
    ];
    const rows = this.filteredAgents().map((agent) => [
      agent.displayName,
      agent.role === 'seniorAgent' ? 'Senior Agent' : 'Agent',
      agent.performanceScore,
      agent.bookings,
      agent.bookingAmount,
      agent.convertedBookings,
      agent.housesSold,
      agent.commissionTaken,
      agent.commissionOwed,
      agent.housesEvicted,
      agent.noticesSent,
      `${agent.conversionRate}%`,
    ]);
    const csv = [headings, ...rows]
      .map((row) => row.map((value) => `"${String(value).replaceAll('"', '""')}"`).join(','))
      .join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `agent-analytics-${this.period()}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
