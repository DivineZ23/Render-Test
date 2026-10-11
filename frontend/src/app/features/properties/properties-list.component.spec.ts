import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { BlockService } from '../../core/services/management.services';
import { PropertyService } from '../../core/services/property.service';
import { PropertiesListComponent } from './properties-list.component';

describe('Property list loading', () => {
  const result = {
    items: [],
    page: 1,
    pageSize: 12,
    totalItems: 0,
    totalPages: 0,
    hasNextPage: false,
    hasPreviousPage: false,
  };
  let available: ReturnType<typeof vi.fn>;
  beforeEach(() => {
    vi.useFakeTimers();
    available = vi.fn(() => of(result));
    TestBed.configureTestingModule({
      providers: [
        { provide: PropertyService, useValue: { available } },
        { provide: BlockService, useValue: { public: () => of([]) } },
      ],
    });
  });
  afterEach(() => vi.useRealTimers());
  const create = () => TestBed.runInInjectionContext(() => new PropertiesListComponent());

  it('starts the first request immediately, without the typing debounce', () => {
    create();
    expect(available).toHaveBeenCalledTimes(1);
  });

  it('debounces rapid typing into one request', () => {
    const component = create();
    component.filters.controls.search.setValue('A');
    component.filters.controls.search.setValue('Alta');
    expect(available).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(280);
    expect(available).toHaveBeenCalledTimes(2);
    expect(available.mock.lastCall![0].search).toBe('Alta');
  });

  it('can retry the same filters after a failed request', () => {
    available.mockReturnValueOnce(throwError(() => new Error('Network error')));
    const component = create();
    expect(component.error()).toBe(true);
    component.reload();
    vi.advanceTimersByTime(280);
    expect(available).toHaveBeenCalledTimes(2);
    expect(component.error()).toBe(false);
    expect(component.loading()).toBe(false);
  });
});
