import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, Subject, throwError } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { NoticeService, TeamService, TenantService } from '../../core/services/management.services';
import { PageAccessService } from '../../core/services/page-access.service';
import { PropertyService } from '../../core/services/property.service';
import { NoticesComponent } from './notices.component';
import { NoticeFeed, NoticeFeedSummary, RentSyncRecord } from '../../core/models/management.models';

describe('Notice page request budget', () => {
  const snapshot = { id: 'latest', records: [], total: 0 };
  let notices: {
    snapshot: ReturnType<typeof vi.fn>;
    snapshots: ReturnType<typeof vi.fn>;
    evictionQueue: ReturnType<typeof vi.fn>;
    feed: ReturnType<typeof vi.fn>;
    feedSummary: ReturnType<typeof vi.fn>;
    records: ReturnType<typeof vi.fn>;
    refresh: ReturnType<typeof vi.fn>;
    setResolution: ReturnType<typeof vi.fn>;
  };
  let team: { agents: ReturnType<typeof vi.fn> };
  let tenants: { evictions: ReturnType<typeof vi.fn> };

  function create(
    mode: string,
    summaryResponse = of({
      currentTotal: 20,
      totalNotices: 120,
      pending: 100,
      sentToday: 20,
    }) as import('rxjs').Observable<NoticeFeedSummary>,
    feedResponse = of({
      asOf: '2026-10-11T12:00:00Z',
      items: [],
      hasMore: true,
      nextSkip: 50,
    }) as import('rxjs').Observable<NoticeFeed>,
  ) {
    notices = {
      snapshot: vi.fn(() => of(snapshot)),
      snapshots: vi.fn(() => of([snapshot])),
      evictionQueue: vi.fn(() => of([])),
      feed: vi.fn(() => feedResponse),
      feedSummary: vi.fn(() => summaryResponse),
      records: vi.fn(() => of({ snapshot, hasMore: false, nextSkip: 0 })),
      refresh: vi.fn(),
      setResolution: vi.fn(),
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
    'loads one bounded feed instead of full history on %s',
    (mode) => {
      const component = create(mode);
      expect(notices.feed).toHaveBeenCalledExactlyOnceWith(
        mode === 'overdueNotice' ? 'overdue' : 'evictable',
        0,
        undefined,
      );
      expect(component.currentNoticeTotal()).toBe(20);
      expect(component.pendingNoticeCount()).toBe(100);
      expect(notices.snapshots).not.toHaveBeenCalled();
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

  it('only loads the current export for Data Sync', () => {
    create('sync');
    expect(notices.snapshot).toHaveBeenCalledTimes(1);
    expect(notices.feed).not.toHaveBeenCalled();
    expect(notices.evictionQueue).not.toHaveBeenCalled();
  });

  it.each(['overdueList', 'activeList'])('loads only the selected status page for %s', (mode) => {
    create(mode);
    expect(notices.records).toHaveBeenCalledExactlyOnceWith(
      mode === 'activeList' ? 'paid' : 'overdue',
      0,
    );
    expect(notices.snapshot).not.toHaveBeenCalled();
    expect(notices.snapshots).not.toHaveBeenCalled();
    expect(notices.evictionQueue).not.toHaveBeenCalled();
    expect(tenants.evictions).not.toHaveBeenCalled();
    expect(team.agents).not.toHaveBeenCalled();
  });

  it('keeps whole-history totals while loading another page and allows retry after failure', () => {
    const component = create('overdueNotice');
    notices.feed.mockReturnValueOnce(throwError(() => new Error('offline')));
    component.loadMore();
    expect(component.loadError()).toBeTruthy();
    expect(component.pendingNoticeCount()).toBe(100);
    expect(component.pageLoading()).toBe(false);
    notices.feed.mockReturnValueOnce(
      of({
        asOf: '2026-10-11T12:00:00Z',
        items: [],
        hasMore: false,
        nextSkip: 120,
      }),
    );
    component.retryLoad();
    expect(notices.feed).toHaveBeenLastCalledWith('overdue', 50, '2026-10-11T12:00:00Z');
    expect(component.loadError()).toBe('');
    expect(component.hasMore()).toBe(false);
  });

  it('prevents overlapping load-more requests', () => {
    const component = create('evictionNotice');
    const response = new Subject<NoticeFeed>();
    notices.feed.mockReturnValue(response);
    component.loadMore();
    component.loadMore();
    expect(notices.feed).toHaveBeenCalledTimes(2); // initial page + one pending page
    expect(component.pageLoading()).toBe(true);
  });

  it('shows a saved notice page but waits for its fresh cutoff before loading more', () => {
    const response = new Subject<NoticeFeed>();
    const component = create('evictionNotice', undefined, response);
    const saved = { asOf: '2026-10-11T12:00:00Z', items: [], hasMore: true, nextSkip: 50 };
    response.next(saved);
    expect(component.noticeFeed()).not.toBeNull();
    component.loadMore();
    expect(notices.feed).toHaveBeenCalledTimes(1);
    expect(component.pageLoading()).toBe(true);
    response.next({ ...saved, asOf: '2026-10-11T12:05:00Z' });
    response.complete();
    expect(component.pageLoading()).toBe(false);
    notices.feed.mockReturnValue(of({ ...saved, items: [], hasMore: false, nextSkip: 50 }));
    component.loadMore();
    expect(notices.feed).toHaveBeenLastCalledWith('evictable', 50, '2026-10-11T12:05:00Z');
  });

  it('keeps a replacement export loading until its own response completes', () => {
    const component = create('overdueList');
    const replacement = new Subject<unknown>();
    component.hasMore.set(true);
    notices.records
      .mockReturnValueOnce(
        of({ snapshot: { ...snapshot, id: 'new' }, hasMore: false, nextSkip: 0 }),
      )
      .mockReturnValueOnce(replacement);
    component.loadMore();
    expect(component.pageLoading()).toBe(true);
    expect(notices.refresh).toHaveBeenCalledTimes(1);
    replacement.next({ snapshot: { ...snapshot, id: 'new' }, hasMore: false, nextSkip: 0 });
    replacement.complete();
    expect(component.pageLoading()).toBe(false);
    expect(component.snapshot()?.id).toBe('new');
  });

  it('finishes loading records while historical totals are still pending', () => {
    const totals = new Subject<NoticeFeedSummary>();
    const component = create('evictionNotice', totals);
    expect(component.noticeFeed()).not.toBeNull();
    expect(component.pageLoading()).toBe(false);
    expect(component.noticeSummary()).toBeNull();
    totals.next({ currentTotal: 20, totalNotices: 120, pending: 100, sentToday: 20 });
    expect(component.pendingNoticeCount()).toBe(100);
  });

  it('updates one resolved notice without replacing paged records or fetching the queue', () => {
    const component = create('evictionNotice');
    const record = { rowNumber: 1, status: 'evictable', isResolved: false } as RentSyncRecord;
    const item = { snapshotId: 'latest', syncedAt: new Date().toISOString(), record };
    component.noticeFeed.update((feed) => ({ ...feed!, items: [item] }));
    notices.setResolution.mockReturnValue(
      of({ ...record, isResolved: true, resolvedAt: new Date().toISOString() }),
    );
    component.setResolution(component.noticeGroups()[0], item, true);
    expect(component.noticeFeed()?.items[0].record.isResolved).toBe(true);
    expect(component.pendingNoticeCount()).toBe(99);
    expect(component.noticesSentToday()).toBe(21);
    expect(notices.evictionQueue).not.toHaveBeenCalled();
  });
});
