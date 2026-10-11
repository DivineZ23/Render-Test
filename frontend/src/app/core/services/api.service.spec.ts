import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiService } from './api.service';

describe('ApiService', () => {
  let service: ApiService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ApiService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('serializes scalar and repeated query parameters', () => {
    service
      .get('/properties', { page: 2, amenities: ['Parking', 'Security'], empty: null })
      .subscribe();
    const request = http.expectOne((req) => req.url.endsWith('/properties'));
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.getAll('amenities')).toEqual(['Parking', 'Security']);
    expect(request.request.params.has('empty')).toBe(false);
    request.flush({});
  });

  it('shares simultaneous reads but fetches fresh data after completion', () => {
    const first = vi.fn();
    const second = vi.fn();
    service.get('/properties', { page: 1, status: 'paid' }).subscribe(first);
    service.get('/properties', { status: 'paid', page: 1 }).subscribe(second);
    http.expectOne((req) => req.url.endsWith('/properties')).flush({ total: 5 });
    expect(first).toHaveBeenCalledWith({ total: 5 });
    expect(second).toHaveBeenCalledWith({ total: 5 });
    service.get('/properties', { page: 1, status: 'paid' }).subscribe();
    http.expectOne((req) => req.url.endsWith('/properties')).flush({ total: 6 });
  });

  it('cancels a shared read only when its last subscriber leaves', () => {
    const first = service.get('/properties').subscribe();
    const second = service.get('/properties').subscribe();
    const request = http.expectOne((req) => req.url.endsWith('/properties'));
    first.unsubscribe();
    expect(request.cancelled).toBe(false);
    second.unsubscribe();
    expect(request.cancelled).toBe(true);
    service.get('/properties').subscribe();
    http.expectOne((req) => req.url.endsWith('/properties')).flush({});
  });

  it('does not retain failed requests', () => {
    service.get('/properties').subscribe({ error: () => undefined });
    http
      .expectOne((req) => req.url.endsWith('/properties'))
      .flush({}, { status: 500, statusText: 'Error' });
    service.get('/properties').subscribe();
    http.expectOne((req) => req.url.endsWith('/properties')).flush({});
  });

  it('does not join reads from before a mutation', () => {
    service.get('/properties').subscribe();
    const old = http.expectOne((req) => req.url.endsWith('/properties'));
    service.patch('/properties/1', { status: 'paid' }).subscribe();
    http.expectOne((req) => req.method === 'PATCH').flush({});
    service.get('/properties').subscribe();
    const fresh = http.expectOne((req) => req.url.endsWith('/properties'));
    old.flush({ status: 'overdue' });
    fresh.flush({ status: 'paid' });
  });
});
