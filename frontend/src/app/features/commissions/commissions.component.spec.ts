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
    const service = { overview: vi.fn(() => of(overview)), preview: vi.fn(() => of({})) };
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
});
