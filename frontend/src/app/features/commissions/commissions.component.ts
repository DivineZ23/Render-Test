import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { LucideCalculator, LucideCheck, LucideRotateCcw, LucideTrophy } from '@lucide/angular';
import {
  AgentCommissionSummary,
  AuctionCommissionCalculation,
  CommissionOverview,
  CommissionRecord,
} from '../../core/models/commission.models';
import { USER_ROLES } from '../../core/constants/user-role.constants';
import { AuthService } from '../../core/services/auth.service';
import { CommissionService } from '../../core/services/management.services';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

type LedgerFilter = 'outstanding' | 'received' | 'all';

@Component({
  selector: 'app-commissions',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    EmptyStateComponent,
    LucideCalculator,
    LucideCheck,
    LucideRotateCcw,
    LucideTrophy,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-title">
      <div>
        <p class="eyebrow">Auction performance</p>
        <h1>Agent commissions</h1>
        <p>Calculate auction earnings and track every agent payout from one ledger.</p>
      </div>
    </div>

    @if (overview(); as data) {
      <section class="metrics" aria-label="Commission summary">
        <article class="panel metric attention">
          <span>Outstanding</span>
          <strong>{{ data.totalOutstanding | currency: 'INR' : 'symbol' : '1.0-2' }}</strong>
          <small>{{ outstandingCount() }} agent payouts awaiting payment</small>
        </article>
        <article class="panel metric">
          <span>Paid out</span>
          <strong>{{ data.totalReceived | currency: 'INR' : 'symbol' : '1.0-2' }}</strong>
          <small>Commission handed out to agents</small>
        </article>
        <article class="panel metric">
          <span>Auction settlements</span>
          <strong>{{ settlementCount() }}</strong>
          <small>Recorded under the current commission structure</small>
        </article>
      </section>

      @if (auth.isManager()) {
        <section class="panel settlement-panel">
          <div class="section-heading">
            <div>
              <p class="eyebrow">New settlement</p>
              <h2>Calculate an auction commission</h2>
              <p>The base price goes to the winner. Only the auction premium enters the slab calculation.</p>
            </div>
            <svg class="heading-icon" lucideCalculator></svg>
          </div>

          <form [formGroup]="settlementForm" (ngSubmit)="createSettlement()">
            <div class="input-grid">
              <label>
                <span>Auction / property</span>
                <input formControlName="auctionReference" placeholder="Property name or auction reference" />
              </label>
              <label>
                <span>Base price</span>
                <input type="number" min="0" step="0.01" formControlName="basePrice" (input)="refreshPreview()" />
                <small>Use the monthly rent or deposit chosen as the auction base.</small>
              </label>
              <label>
                <span>Final auction price</span>
                <input type="number" min="0" step="0.01" formControlName="finalAuctionPrice" (input)="refreshPreview()" />
              </label>
              <label>
                <span>Winning agent</span>
                <select formControlName="winningAgentUserId" (change)="winnerChanged()">
                  <option value="">Select the winning agent</option>
                  @for (agent of data.agents; track agent.userId) {
                    <option [value]="agent.userId">{{ agent.displayName }} · {{ roleLabel(agent.role) }}</option>
                  }
                </select>
              </label>
            </div>

            <fieldset class="participant-picker">
              <legend>Other participating agents</legend>
              <p>Select only agents who validly participated. The winner is excluded automatically.</p>
              <div class="participant-grid">
                @for (agent of data.agents; track agent.userId) {
                  <label [class.disabled]="agent.userId === settlementForm.controls.winningAgentUserId.value">
                    <input
                      type="checkbox"
                      [checked]="otherAgentIds().includes(agent.userId)"
                      [disabled]="agent.userId === settlementForm.controls.winningAgentUserId.value"
                      (change)="toggleOtherAgent(agent.userId, $any($event.target).checked)"
                    />
                    <span><strong>{{ agent.displayName }}</strong><small>{{ roleLabel(agent.role) }}</small></span>
                  </label>
                } @empty {
                  <p class="empty-agents">No approved agents are available.</p>
                }
              </div>
            </fieldset>

            @if (preview(); as result) {
              <div class="calculation">
                <div class="calculation-summary">
                  <span><small>Final auction</small><strong>{{ result.finalAuctionPrice | currency: 'INR' : 'symbol' : '1.0-2' }}</strong></span>
                  <span><small>Base price</small><strong>{{ result.basePrice | currency: 'INR' : 'symbol' : '1.0-2' }}</strong></span>
                  <span><small>Auction premium</small><strong>{{ result.auctionPremium | currency: 'INR' : 'symbol' : '1.0-2' }}</strong></span>
                  <span><small>Additional agent pool</small><strong>{{ result.additionalAgentPool | currency: 'INR' : 'symbol' : '1.0-2' }}</strong></span>
                </div>
                <div class="payout-breakdown">
                  <article class="winner-card">
                    <svg lucideTrophy></svg>
                    <div><small>Winning agent</small><strong>{{ selectedWinnerName() || 'Select an agent' }}</strong></div>
                    <dl>
                      <div><dt>Base deposit</dt><dd>{{ result.winningAgentBaseShare | currency: 'INR' : 'symbol' : '1.0-2' }}</dd></div>
                      <div><dt>60% closing share</dt><dd>{{ result.winningAgentClosingShare | currency: 'INR' : 'symbol' : '1.0-2' }}</dd></div>
                      <div class="total"><dt>Total</dt><dd>{{ result.winningAgentTotal | currency: 'INR' : 'symbol' : '1.0-2' }}</dd></div>
                    </dl>
                  </article>
                  <article>
                    <div><small>Other agents</small><strong>{{ result.otherAgentCount }} participating</strong></div>
                    <dl>
                      <div><dt>40% participation pool</dt><dd>{{ result.participationPool | currency: 'INR' : 'symbol' : '1.0-2' }}</dd></div>
                      <div class="total"><dt>Per other agent</dt><dd>{{ result.amountPerOtherAgent | currency: 'INR' : 'symbol' : '1.0-2' }}</dd></div>
                    </dl>
                  </article>
                </div>
              </div>
            }

            <div class="form-actions">
              <span>{{ 1 + otherAgentIds().length }} total agent{{ otherAgentIds().length ? 's' : '' }}</span>
              <button class="btn btn-primary" type="submit" [disabled]="settlementForm.invalid || !preview() || saving()">
                {{ saving() ? 'Recording…' : 'Record settlement' }}
              </button>
            </div>
          </form>
        </section>

        <section class="agents-section">
          <div class="section-heading compact"><div><p class="eyebrow">Agent balances</p><h2>Commission owed to agents</h2></div></div>
          <div class="agent-grid">
            @for (agent of data.agents; track agent.userId) {
              <article class="panel agent-card" [class.clear]="agent.outstandingCommission === 0">
                <header><div><h3>{{ agent.displayName }}</h3><p>{{ roleLabel(agent.role) }}</p></div><span>{{ agent.outstandingCount }} pending</span></header>
                <strong>{{ agent.outstandingCommission | currency: 'INR' : 'symbol' : '1.0-2' }}</strong>
                <footer><span>{{ agent.auctionCount }} auctions</span><span>{{ agent.receivedCommission | currency: 'INR' : 'symbol' : '1.0-2' }} paid</span></footer>
              </article>
            } @empty {
              <app-empty-state title="No commission agents" message="Approved agents and senior agents will appear here." />
            }
          </div>
        </section>
      }

      <section class="ledger-section">
        <div class="section-heading ledger-heading">
          <div><p class="eyebrow">Payout ledger</p><h2>Auction commissions</h2></div>
          <nav class="filters" aria-label="Commission status filter">
            <button [class.active]="filter() === 'outstanding'" (click)="filter.set('outstanding')">Outstanding</button>
            <button [class.active]="filter() === 'received'" (click)="filter.set('received')">Paid</button>
            <button [class.active]="filter() === 'all'" (click)="filter.set('all')">All</button>
          </nav>
        </div>
        @if (filteredRecords().length) {
          <div class="panel table-wrap">
            <table>
              <thead><tr><th>Auction</th>@if (auth.isManager()) {<th>Agent</th>}<th>Position</th><th>Base share</th><th>Premium share</th><th>Total payout</th><th>Status</th></tr></thead>
              <tbody>
                @for (record of filteredRecords(); track record.id) {
                  <tr [class.received-row]="record.isPaid">
                    <td><strong>{{ record.auctionReference }}</strong><small>{{ record.createdAt | date: 'mediumDate' }} · {{ record.finalAuctionPrice | currency: 'INR' : 'symbol' : '1.0-2' }}</small></td>
                    @if (auth.isManager()) {<td><strong>{{ record.agentDisplayName }}</strong><small>{{ roleLabel(record.agentRole) }}</small></td>}
                    <td><span class="position" [class.winner]="record.isWinningAgent">{{ record.isWinningAgent ? 'Winner' : 'Participant' }}</span></td>
                    <td>{{ record.baseShare | currency: 'INR' : 'symbol' : '1.0-2' }}</td>
                    <td>{{ record.premiumShare | currency: 'INR' : 'symbol' : '1.0-2' }}</td>
                    <td><strong class="amount">{{ record.commissionAmount | currency: 'INR' : 'symbol' : '1.0-2' }}</strong></td>
                    <td>
                      @if (auth.isManager()) {
                        <button class="status-action" [class.received]="record.isPaid" (click)="togglePaid(record)">
                          @if (record.isPaid) {<svg lucideRotateCcw></svg>Mark unpaid} @else {<svg lucideCheck></svg>Mark paid}
                        </button>
                      } @else {<span class="status-label" [class.received]="record.isPaid">{{ record.isPaid ? 'Paid' : 'Outstanding' }}</span>}
                      @if (record.paidAt) {<small class="receipt-note">{{ record.paidAt | date: 'mediumDate' }} · {{ record.paidByDisplayName }}</small>}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <app-empty-state title="No auction commissions in this view" message="New auction settlements will appear here." />
        }
      </section>
    } @else {
      <section class="panel loading">Loading commission ledger…</section>
    }
  `,
  styles: [`
    .page-title,.section-heading{display:flex;align-items:flex-end;justify-content:space-between;gap:24px}.page-title{margin-bottom:24px}.page-title h1{margin:4px 0;font-size:2.5rem}.page-title p:last-child,.section-heading p:last-child{margin:0;color:var(--muted)}
    .metrics{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;margin-bottom:18px}.metric{padding:20px;display:grid;gap:5px;border-top:3px solid var(--border)}.metric.attention{border-top-color:var(--forest)}.metric>span{color:var(--muted);font-size:.72rem}.metric>strong{font-size:1.65rem}
    .settlement-panel{padding:24px}.section-heading h2{margin:4px 0 5px;font-size:1.25rem}.heading-icon{width:28px;height:28px;color:var(--forest)}form{margin-top:22px}.input-grid{display:grid;grid-template-columns:1.3fr repeat(2,1fr) 1.2fr;gap:12px}.input-grid label{display:grid;grid-auto-rows:max-content;align-content:start;gap:7px}.input-grid span,legend{font-size:.7rem;font-weight:700;color:var(--muted)}input,select{width:100%;min-width:0;border:1px solid var(--border);border-radius:8px;background:var(--surface-subtle);color:var(--ink);padding:11px 12px;font:inherit}label small{color:var(--muted);font-size:.62rem}
    .participant-picker{margin:20px 0 0;padding:0;border:0}.participant-picker>p{margin:5px 0 10px;color:var(--muted);font-size:.68rem}.participant-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:8px}.participant-grid>label{display:flex;align-items:center;gap:9px;padding:10px;border:1px solid var(--border);border-radius:8px;background:var(--surface-subtle);cursor:pointer}.participant-grid>label.disabled{opacity:.45;cursor:not-allowed}.participant-grid input{width:auto}.participant-grid span{display:grid;color:var(--ink)}.participant-grid small{display:block;margin-top:2px}.empty-agents{color:var(--muted)}
    .calculation{margin-top:20px;border:1px solid color-mix(in srgb,var(--forest) 35%,var(--border));border-radius:12px;overflow:hidden}.calculation-summary{display:grid;grid-template-columns:repeat(4,1fr);background:var(--forest-light)}.calculation-summary>span{display:grid;gap:4px;padding:14px;border-right:1px solid var(--border)}.calculation-summary>span:last-child{border:0}.calculation small{color:var(--muted)}.payout-breakdown{display:grid;grid-template-columns:1fr 1fr;gap:0}.payout-breakdown article{padding:18px}.payout-breakdown article+article{border-left:1px solid var(--border)}.winner-card{display:grid;grid-template-columns:auto 1fr;gap:10px}.winner-card>svg{width:19px;color:var(--forest)}.payout-breakdown dl{grid-column:1/-1;margin:15px 0 0}.payout-breakdown dl div{display:flex;justify-content:space-between;gap:15px;padding:7px 0;border-top:1px solid var(--border);font-size:.7rem}.payout-breakdown dt{color:var(--muted)}.payout-breakdown dd{margin:0;font-weight:700}.payout-breakdown .total{font-size:.8rem}.form-actions{display:flex;align-items:center;justify-content:flex-end;gap:14px;margin-top:18px}.form-actions>span{color:var(--muted);font-size:.68rem}
    .section-heading.compact{margin:30px 0 12px}.agent-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(245px,1fr));gap:12px}.agent-card{padding:17px}.agent-card header,.agent-card footer{display:flex;align-items:center;justify-content:space-between;gap:12px}.agent-card h3{margin:0;font-size:.92rem}.agent-card header p{margin:3px 0 0;color:var(--muted);font-size:.68rem}.agent-card header>span{color:var(--warning-ink);font-size:.65rem;font-weight:700}.agent-card.clear header>span{color:var(--forest)}.agent-card>strong{display:block;margin:18px 0;font-size:1.45rem}.agent-card footer{color:var(--muted);font-size:.67rem}
    .ledger-section{margin-top:30px}.ledger-heading{margin-bottom:12px}.filters{display:flex;gap:4px;padding:4px;border:1px solid var(--border);border-radius:9px;background:var(--surface)}.filters button{border:0;border-radius:6px;background:transparent;color:var(--muted);padding:7px 10px;cursor:pointer;font-size:.7rem;font-weight:650}.filters button.active{background:var(--forest-light);color:var(--forest)}.table-wrap{overflow-x:auto}table{width:100%;border-collapse:collapse;min-width:980px}th{padding:11px 14px;border-bottom:1px solid var(--border);color:var(--muted);text-align:left;font-size:.63rem;letter-spacing:.05em;text-transform:uppercase}td{padding:13px 14px;border-bottom:1px solid var(--border);font-size:.73rem;vertical-align:middle}tbody tr:last-child td{border-bottom:0}td strong,td small{display:block}td small{margin-top:3px;color:var(--muted);font-size:.65rem}.received-row{opacity:.72}.amount{color:var(--forest);font-size:.82rem}.position,.status-label{display:inline-flex;padding:5px 8px;border-radius:99px;background:var(--surface-subtle);color:var(--muted);font-weight:700;font-size:.65rem}.position.winner,.status-label.received{background:var(--forest-light);color:var(--forest)}.status-label:not(.received){background:var(--warning-soft);color:var(--warning-ink)}.status-action{border:0;border-radius:7px;background:var(--forest-light);color:var(--forest);padding:7px 9px;display:inline-flex;align-items:center;gap:5px;cursor:pointer;font-size:.66rem;font-weight:700}.status-action.received{background:var(--surface-subtle);color:var(--muted)}.status-action svg{width:13px;height:13px}.receipt-note{white-space:nowrap}.loading{padding:24px;color:var(--muted)}
    @media(max-width:1100px){.input-grid{grid-template-columns:1fr 1fr}.calculation-summary{grid-template-columns:1fr 1fr}}@media(max-width:720px){.page-title,.section-heading{align-items:stretch;flex-direction:column}.metrics,.input-grid,.calculation-summary,.payout-breakdown{grid-template-columns:1fr}.payout-breakdown article+article{border-left:0;border-top:1px solid var(--border)}.filters{width:100%}.filters button{flex:1}}
  `],
})
export class CommissionsComponent {
  readonly auth = inject(AuthService);
  private readonly service = inject(CommissionService);
  private readonly snackBar = inject(MatSnackBar);
  readonly overview = signal<CommissionOverview | null>(null);
  readonly filter = signal<LedgerFilter>('outstanding');
  readonly saving = signal(false);
  readonly preview = signal<AuctionCommissionCalculation | null>(null);
  readonly otherAgentIds = signal<string[]>([]);
  readonly roleLabel = (role: keyof typeof USER_ROLES) => USER_ROLES[role];
  readonly settlementForm = new FormGroup({
    auctionReference: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(160)] }),
    basePrice: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    finalAuctionPrice: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    winningAgentUserId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  readonly filteredRecords = computed(() => {
    const records = this.overview()?.records ?? [];
    return this.filter() === 'all' ? records : records.filter((record) => record.isPaid === (this.filter() === 'received'));
  });
  readonly outstandingCount = computed(() => this.overview()?.records.filter((record) => !record.isPaid).length ?? 0);
  readonly settlementCount = computed(() => new Set((this.overview()?.records ?? []).map((record) => record.settlementId)).size);
  constructor() { this.load(); }

  load() { this.service.overview().subscribe((value) => this.overview.set(value)); }

  refreshPreview() {
    const { basePrice, finalAuctionPrice } = this.settlementForm.getRawValue();
    if (basePrice < 0 || finalAuctionPrice < basePrice) { this.preview.set(null); return; }
    this.service.preview(finalAuctionPrice, basePrice, this.otherAgentIds().length + 1).subscribe({
      next: (value) => this.preview.set(value),
      error: () => this.preview.set(null),
    });
  }

  winnerChanged() {
    const winnerId = this.settlementForm.controls.winningAgentUserId.value;
    this.otherAgentIds.update((ids) => ids.filter((id) => id !== winnerId));
    this.refreshPreview();
  }

  selectedWinnerName() {
    const winnerId = this.settlementForm.controls.winningAgentUserId.value;
    return this.overview()?.agents.find((agent) => agent.userId === winnerId)?.displayName ?? '';
  }

  toggleOtherAgent(id: string, checked: boolean) {
    this.otherAgentIds.update((ids) => checked ? [...new Set([...ids, id])] : ids.filter((value) => value !== id));
    this.refreshPreview();
  }

  createSettlement() {
    if (this.settlementForm.invalid || !this.preview()) return;
    this.saving.set(true);
    this.service.createSettlement({ ...this.settlementForm.getRawValue(), otherAgentUserIds: this.otherAgentIds() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.settlementForm.reset({ auctionReference: '', basePrice: 0, finalAuctionPrice: 0, winningAgentUserId: '' });
        this.otherAgentIds.set([]);
        this.preview.set(null);
        this.load();
        this.snackBar.open('Auction settlement recorded.', 'Dismiss', { duration: 3000, panelClass: ['success-toast'] });
      },
      error: () => this.saving.set(false),
    });
  }

  togglePaid(record: CommissionRecord) {
    this.service.setPaid(record.id, !record.isPaid).subscribe(() => this.load());
  }
}
