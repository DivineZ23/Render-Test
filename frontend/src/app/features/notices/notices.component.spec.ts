import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { NoticeService, TeamService, TenantService } from '../../core/services/management.services';
import { PageAccessService } from '../../core/services/page-access.service';
import { PropertyService } from '../../core/services/property.service';
import { NoticesComponent } from './notices.component';

describe('Notice page request budget', () => {
  const snapshot = { id: 'latest', records: [], total: 0 };
  let notices: {
    snapshot: ReturnType<typeof vi.fn>;
    snapshots: ReturnType<typeof vi.fn>;
    evictionQueue: ReturnType<typeof vi.fn>;
  };
  let team: { agents: ReturnType<typeof vi.fn> };
  let tenants: { evictions: ReturnType<typeof vi.fn> };

  function create(mode: string) {
    notices = {
      snapshot: vi.fn(() => of(snapshot)),
      snapshots: vi.fn(() => of([snapshot])),
      evictionQueue: vi.fn(() => of([])),
    };
    team = { agents: vi.fn(() => of([])) };
    tenants = { evictions: vi.fn(() => of([])) };
    TestBed.configureTestingModule({
      providers: [
        { provide: ActivatedRoute, useValue: { data: of({ mode }) } },
        { provide: NoticeService, useValue: notices },
        { provide: TeamService, useValue: team },
        { provide: TenantService, useValue: tenants },
        { provide: AuthService, useValue: {} },
        { provide: PageAccessService, useValue: {} },
        { provide: PropertyService, useValue: {} },
        { provide: MatDialog, useValue: {} },
        { provide: MatSnackBar, useValue: {} },
      ],
    });
    const component = TestBed.runInInjectionContext(() => new NoticesComponent());
    TestBed.tick();
    return component;
  }

  it('loads only the queue on the eviction queue page', () => {
    create('evictionQueue');
    expect(notices.evictionQueue).toHaveBeenCalledTimes(1);
    expect(notices.snapshot).not.toHaveBeenCalled();
    expect(notices.snapshots).not.toHaveBeenCalled();
    expect(tenants.evictions).not.toHaveBeenCalled();
    expect(team.agents).not.toHaveBeenCalled();
  });

  it('loads only completed evictions on the history page', () => {
    create('evictionHistory');
    expect(tenants.evictions).toHaveBeenCalledTimes(1);
    expect(notices.evictionQueue).not.toHaveBeenCalled();
    expect(notices.snapshot).not.toHaveBeenCalled();
    expect(notices.snapshots).not.toHaveBeenCalled();
  });

  it.each(['overdueNotice', 'evictionNotice'])(
    'reuses the latest history snapshot on %s',
    (mode) => {
      const component = create(mode);
      expect(notices.snapshots).toHaveBeenCalledExactlyOnceWith(false);
      expect(component.snapshot()?.id).toBe('latest');
      expect(notices.snapshot).not.toHaveBeenCalled();
      expect(notices.evictionQueue).not.toHaveBeenCalled();
      expect(tenants.evictions).not.toHaveBeenCalled();
    },
  );

  it('asks for lightweight summaries on Sync History', () => {
    create('syncedDataRecords');
    expect(notices.snapshots).toHaveBeenCalledExactlyOnceWith(true);
    expect(team.agents).toHaveBeenCalledTimes(1);
    expect(notices.snapshot).not.toHaveBeenCalled();
    expect(notices.evictionQueue).not.toHaveBeenCalled();
  });

  it.each(['sync', 'overdueList', 'activeList'])('only loads the current export for %s', (mode) => {
    create(mode);
    expect(notices.snapshot).toHaveBeenCalledTimes(1);
    expect(notices.snapshots).not.toHaveBeenCalled();
    expect(notices.evictionQueue).not.toHaveBeenCalled();
    expect(tenants.evictions).not.toHaveBeenCalled();
    expect(team.agents).not.toHaveBeenCalled();
  });
});
