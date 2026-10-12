import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, Subject } from 'rxjs';
import { CommissionOverview, CommissionRecord } from '../../core/models/commission.models';
import { AuthService } from '../../core/services/auth.service';
import { CommissionService } from '../../core/services/management.services';
import { CommissionsComponent } from './commissions.component';

describe('Commission editor freshness', () => {
  beforeEach(() => {
    vi.spyOn(window, 'scrollTo').mockImplementation(() => undefined);
  });
  afterEach(() => vi.restoreAllMocks());
  const record = {
    settlementId: 'sale-1',
    auctionReference: 'Old reference',
    basePrice: 20000,
    finalAuctionPrice: 100000,
    isPaid: false,
    isWinningAgent: true,
    agentUserId: 'agent-1',
  } as CommissionRecord;
  const overview = { records: [record], agents: [] } as unknown as CommissionOverview;

  function setup() {
    const service = {
      overview: vi.fn(() => of(overview)), preview: vi.fn(() => of({})),
      createSettlement: vi.fn(() => of([])), updateSettlement: vi.fn(() => of([])),
    };
    const snackBar = { open: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        { provide: CommissionService, useValue: service },
        { provide: AuthService, useValue: {} },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: snackBar },
      ],
    });
    const component = TestBed.runInInjectionContext(() => new CommissionsComponent());
    const fresh = new Subject<CommissionOverview>();
    service.overview.mockReturnValue(fresh);
    return { component, fresh, service, snackBar };
  }

  it('populates the editor from a server read, not the saved ledger', () => {
    const { component, fresh, service } = setup();
    component.editSettlement('sale-1');
    expect(service.overview).toHaveBeenLastCalledWith(true);
    expect(component.loadingSettlement()).toBe(true);
    expect(component.editingSettlementId()).toBeNull();
    fresh.next({ ...overview, records: [{ ...record, finalAuctionPrice: 200000 }] });
    fresh.complete();
    expect(component.settlementForm.getRawValue().finalAuctionPrice).toBe(200000);
    expect(component.loadingSettlement()).toBe(false);
  });

  it('does not open a settlement paid since the saved ledger was loaded', () => {
    const { component, fresh, snackBar } = setup();
    component.editSettlement('sale-1');
    fresh.next({ ...overview, records: [{ ...record, isPaid: true }] });
    fresh.complete();
    expect(component.editingSettlementId()).toBeNull();
    expect(snackBar.open).toHaveBeenCalled();
  });

  it('requires explicit no-winners selection and at least one participant', () => {
    const { component, service } = setup();
    component.toggleOtherAgent('agent-2', true);
    expect(service.preview).not.toHaveBeenCalled();
    component.toggleNoWinningAgents(true);
    expect(service.preview).toHaveBeenLastCalledWith(0, 0, 1, 0);
    expect(component.hasValidParticipants()).toBe(true);
    component.toggleOtherAgent('agent-2', false);
    expect(component.hasValidParticipants()).toBe(false);
    expect(component.preview()).toBeNull();
  });

  it('clears winners and saves a participant-only settlement', () => {
    const { component, service } = setup();
    component.settlementForm.setValue({ auctionReference: 'House', basePrice: 10000, finalAuctionPrice: 110000 });
    component.toggleWinningAgent('agent-1', true);
    component.toggleOtherAgent('agent-2', true);
    component.toggleNoWinningAgents(true);
    component.toggleWinningAgent('agent-1', true);
    expect(component.winningAgentIds()).toEqual([]);
    expect(service.preview).toHaveBeenLastCalledWith(110000, 10000, 1, 0);
    component.createSettlement();
    expect(service.createSettlement).toHaveBeenCalledWith({
      auctionReference: 'House', basePrice: 10000, finalAuctionPrice: 110000,
      winningAgentUserIds: [], otherAgentUserIds: ['agent-2'],
    });
    expect(component.noWinningAgents()).toBe(false);
  });

  it('restores no-winners mode when editing a participant-only settlement', () => {
    const { component, fresh, service } = setup();
    component.editSettlement('sale-1');
    fresh.next({ ...overview, records: [{ ...record, isWinningAgent: false }] });
    fresh.complete();
    expect(component.noWinningAgents()).toBe(true);
    expect(component.otherAgentIds()).toEqual(['agent-1']);
    component.createSettlement();
    expect(service.updateSettlement).toHaveBeenCalledWith('sale-1', expect.objectContaining({ winningAgentUserIds: [], otherAgentUserIds: ['agent-1'] }));
  });

  it('ignores a pending winner preview after switching to no winners', () => {
    const { component, service } = setup();
    const pending = new Subject<{}>();
    service.preview.mockReturnValue(pending);
    component.toggleWinningAgent('agent-1', true);
    component.toggleNoWinningAgents(true);
    pending.next({});
    expect(component.preview()).toBeNull();
    component.createSettlement();
    expect(service.createSettlement).not.toHaveBeenCalled();
  });
});
