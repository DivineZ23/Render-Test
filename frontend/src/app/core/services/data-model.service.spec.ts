import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiService } from './api.service';
import { DataModelService } from './data-model.service';
import { QUIET_HTTP_ERRORS } from '../config/http-context';
import {
  AccessManagementService,
  NoticeService,
  SettingsService,
  CommissionService,
} from './management.services';
import { PropertyService } from './property.service';

describe('Shared data models', () => {
  let api: ApiService;
  let models: DataModelService;
  let http: HttpTestingController;
  let now: number;
  const request = (path = '/dashboard') => http.expectOne((req) => req.url.endsWith(path));
  const seed = (path = '/dashboard', value: unknown = { total: 1 }) => {
    api.read(path).subscribe();
    request(path).flush(value as object);
  };
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ApiService);
    models = TestBed.inject(DataModelService);
    http = TestBed.inject(HttpTestingController);
    now = Date.now();
    vi.spyOn(Date, 'now').mockImplementation(() => now);
  });
  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  it('reuses recent data, with query keys independent of object property order', () => {
    api.read('/properties', { page: 1, status: 'paid' }).subscribe();
    request('/properties').flush({ items: [1] });
    const receive = vi.fn();
    api.read('/properties', { status: 'paid', page: 1 }).subscribe(receive);
    expect(receive).toHaveBeenCalledWith({ items: [1] });
    http.expectNone((req) => req.url.endsWith('/properties'));
    api.read('/properties', { status: 'overdue', page: 1 }).subscribe();
    request('/properties').flush({ items: [2] });
  });

  it('shows saved data immediately and refreshes in the background after ten seconds', () => {
    seed();
    now += 10_001;
    const receive = vi.fn();
    api.read('/dashboard').subscribe(receive);
    expect(receive).toHaveBeenCalledExactlyOnceWith({ total: 1 });
    expect(models.refreshing()).toBe(true);
    const fresh = request();
    expect(fresh.request.context.get(QUIET_HTTP_ERRORS)).toBe(true);
    fresh.flush({ total: 2 });
    expect(receive.mock.calls.map(([value]) => value)).toEqual([{ total: 1 }, { total: 2 }]);
    expect(models.refreshing()).toBe(false);
  });

  it('does not show data older than two minutes', () => {
    seed();
    now += 120_001;
    const receive = vi.fn();
    api.read('/dashboard').subscribe(receive);
    expect(receive).not.toHaveBeenCalled();
    expect(models.refreshing()).toBe(false);
    request().flush({ total: 2 });
  });

  it('keeps concurrent refresh state until the last shared subscriber finishes', () => {
    seed();
    now += 11_000;
    const first = api.read('/dashboard').subscribe();
    api.read('/dashboard').subscribe();
    const fresh = request();
    first.unsubscribe();
    expect(fresh.cancelled).toBe(false);
    expect(models.refreshing()).toBe(true);
    fresh.flush({ total: 2 });
    expect(models.refreshing()).toBe(false);
  });

  it('clones models so consumer edits cannot corrupt another page', () => {
    api.read<{ items: number[] }>('/dashboard').subscribe((value) => value.items.push(99));
    request().flush({ items: [1] });
    const receive = vi.fn();
    api.read('/dashboard').subscribe(receive);
    expect(receive).toHaveBeenCalledWith({ items: [1] });
  });

  it('keeps the preview on transient refresh failure and exposes a retry warning', () => {
    seed();
    now += 11_000;
    const receive = vi.fn();
    api.read('/dashboard').subscribe(receive);
    request().flush({}, { status: 503, statusText: 'Unavailable' });
    expect(receive).toHaveBeenCalledExactlyOnceWith({ total: 1 });
    expect(models.refreshFailed()).toBe(true);
    seed('/other'); // A successful unrelated read must not hide the warning.
    expect(models.refreshFailed()).toBe(true);
    api.read('/dashboard').subscribe();
    request().flush({ total: 3 });
    expect(models.refreshFailed()).toBe(false);
  });

  it.each([401, 403])('purges saved models and propagates a %s refresh failure', (status) => {
    seed();
    seed('/other');
    now += 11_000;
    const error = vi.fn();
    api.read('/dashboard').subscribe({ error });
    request().flush({}, { status, statusText: 'Denied' });
    expect(error).toHaveBeenCalledWith(expect.any(HttpErrorResponse));
    expect(models.refreshFailed()).toBe(false);
    const receive = vi.fn();
    api.read('/other').subscribe(receive);
    expect(receive).not.toHaveBeenCalled();
    request('/other').flush({ total: 2 });
  });

  it('invalidates all display models on writes, including failed writes', () => {
    seed();
    api.patch('/properties/1', {}).subscribe({ error: () => undefined });
    request('/properties/1').flush({}, { status: 500, statusText: 'Error' });
    const receive = vi.fn();
    api.read('/dashboard').subscribe(receive);
    expect(receive).not.toHaveBeenCalled();
    request().flush({ total: 2 });
  });

  it('does not publish or cache a late read from before a write', () => {
    const receive = vi.fn();
    api.read('/dashboard').subscribe(receive);
    const old = request();
    api.patch('/properties/1', {}).subscribe();
    request('/properties/1').flush({});
    old.flush({ total: 1 });
    expect(receive).not.toHaveBeenCalled();
    request().flush({ total: 2 });
    expect(receive).toHaveBeenCalledExactlyOnceWith({ total: 2 });
  });

  it('cancels pending model reads and drops saved data when the session changes', () => {
    seed('/other');
    const receive = vi.fn();
    api.read('/dashboard').subscribe(receive);
    const pending = request();
    api.clearSavedData(true);
    expect(pending.cancelled).toBe(true);
    expect(receive).not.toHaveBeenCalled();
    api.read('/other').subscribe(receive);
    expect(receive).not.toHaveBeenCalled();
    request('/other').flush({ total: 2 });
  });

  it('never reuses models for forms, access settings or eviction eligibility', () => {
    const properties = TestBed.inject(PropertyService);
    const notices = TestBed.inject(NoticeService);
    const access = TestBed.inject(AccessManagementService);
    const settings = TestBed.inject(SettingsService);
    const commissions = TestBed.inject(CommissionService);
    seed('/commissions');
    for (let attempt = 0; attempt < 2; attempt++) {
      properties.details('1').subscribe();
      request('/properties/1/manage').flush({});
      notices.evictionQueue().subscribe();
      request('/notices/eviction-queue').flush([]);
      access.get().subscribe();
      request('/access-management').flush({});
      settings.team().subscribe();
      request('/settings/team').flush([]);
      commissions.overview(true).subscribe();
      request('/commissions').flush({});
      api.get('/auth/me').subscribe();
      request('/auth/me').flush({});
    }
  });

  it('does not invalidate page models for a read-only commission preview', () => {
    seed();
    TestBed.inject(CommissionService).preview(100000, 20000, 5, 1).subscribe();
    request('/commissions/preview').flush({});
    api.read('/dashboard').subscribe();
    http.expectNone((req) => req.url.endsWith('/dashboard'));
  });

  it('drops saved first pages when continuation paging detects a new sync', () => {
    const notices = TestBed.inject(NoticeService);
    notices.records('overdue').subscribe();
    request('/notices/records').flush({ snapshot: { id: 'old' } });
    notices.records('overdue', 50).subscribe();
    request('/notices/records').flush({ snapshot: { id: 'new' } });
    notices.refresh();
    const receive = vi.fn();
    notices.records('overdue').subscribe(receive);
    expect(receive).not.toHaveBeenCalled();
    request('/notices/records').flush({ snapshot: { id: 'new' } });
  });

  it('caps saved entry count and skips oversized exports', () => {
    for (let index = 0; index < 81; index++) seed(`/entry/${index}`);
    api.read('/entry/0').subscribe();
    request('/entry/0').flush({});
    seed('/large', { raw: 'x'.repeat(600_000) });
    api.read('/large').subscribe();
    request('/large').flush({});
  });
});
