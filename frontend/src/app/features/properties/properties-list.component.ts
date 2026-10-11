import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { catchError, debounceTime, finalize, of, startWith, switchMap } from 'rxjs';
import { DEFAULT_PAGE_SIZE } from '../../core/constants/app.constants';
import { PROPERTY_TYPE_OPTIONS } from '../../core/constants/property-status.constants';
import { PagedResult } from '../../core/models/api.models';
import {
  Block,
  PropertyQuery,
  PropertyType,
  PublicProperty,
} from '../../core/models/property.models';
import { BlockService } from '../../core/services/management.services';
import { PropertyService } from '../../core/services/property.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PropertyCardComponent } from '../../shared/components/property-card/property-card.component';
import { LucideSearch, LucideSlidersHorizontal, LucideX } from '@lucide/angular';

@Component({
  selector: 'app-properties-list',
  imports: [
    ReactiveFormsModule,
    PropertyCardComponent,
    EmptyStateComponent,
    LucideSearch,
    LucideSlidersHorizontal,
    LucideX,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="page-head">
      <div class="container">
        <p class="eyebrow">Available now</p>
        <h1>Find your place.</h1>
        <p>Explore verified homes and commercial spaces, each currently open for enquiry.</p>
      </div>
    </section>
    <section class="container listing">
      <div class="filter-heading">
        <div><svg lucideSlidersHorizontal></svg><b>Refine your search</b></div>
        <button type="button" (click)="clear()"><svg lucideX></svg>Clear filters</button>
      </div>
      <form [formGroup]="filters" class="filters">
        <label class="search"
          ><svg lucideSearch></svg
          ><input
            formControlName="search"
            placeholder="Search property, block or ID"
            aria-label="Search properties" /></label
        ><select formControlName="blockId" aria-label="Filter by block">
          <option value="">All blocks</option>
          @for (block of blocks(); track block.id) {
            <option [value]="block.id">{{ block.blockName }}</option>
          }</select
        ><select formControlName="type" aria-label="Filter by property type">
          <option value="">All types</option>
          @for (type of types; track type.value) {
            <option [value]="type.value">{{ type.label }}</option>
          }</select
        ><select formControlName="personCapacity" aria-label="Filter by person capacity">
          <option [ngValue]="null">Any capacity</option>
          <option [ngValue]="1">1 person</option>
          <option [ngValue]="2">2 people</option>
          <option [ngValue]="3">3 people</option>
          <option [ngValue]="4">4 people</option>
          <option [ngValue]="5">5 people</option></select
        ><input
          type="number"
          formControlName="minRent"
          min="0"
          placeholder="Min rent"
          aria-label="Minimum rent"
        /><input
          type="number"
          formControlName="maxRent"
          min="0"
          placeholder="Max rent"
          aria-label="Maximum rent"
        /><select formControlName="storageCapacity" aria-label="Filter by storage capacity">
          <option [ngValue]="null">Any storage</option>
          @for (storage of storageOptions; track storage.value) {
            <option [ngValue]="storage.value">{{ storage.label }} storage</option>
          }</select
        ><select formControlName="sort">
          <option value="newest:desc">Newest</option>
          <option value="rent:asc">Rent: low to high</option>
          <option value="rent:desc">Rent: high to low</option>
          <option value="name:asc">Name</option>
        </select>
      </form>
      <div class="result-bar">
        <span
          >{{ result().totalItems }} available
          {{ result().totalItems === 1 ? 'property' : 'properties' }}</span
        >
        @if (loading()) {
          <span class="loading">Updating…</span>
        }
      </div>
      @if (error()) {
        <app-empty-state
          title="We could not load properties"
          message="Please refresh the page or try again shortly."
          ><button class="btn btn-secondary" (click)="reload()">Try again</button></app-empty-state
        >
      } @else if (!loading() && result().items.length === 0) {
        <app-empty-state
          title="No homes match those filters"
          message="Clear one or more filters to see available properties."
          ><button class="btn btn-secondary" (click)="clear()">
            Clear filters
          </button></app-empty-state
        >
      } @else {
        <div class="property-grid">
          @for (property of result().items; track property.id) {
            <app-property-card [property]="property" />
          }
          @if (loading() && result().items.length === 0) {
            @for (item of skeletons; track item) {
              <div class="skeleton"></div>
            }
          }
        </div>
        <div class="pagination">
          <button
            class="btn btn-secondary"
            [disabled]="!result().hasPreviousPage"
            (click)="page(result().page - 1)"
          >
            Previous</button
          ><span>Page {{ result().page }} of {{ result().totalPages || 1 }}</span
          ><button
            class="btn btn-secondary"
            [disabled]="!result().hasNextPage"
            (click)="page(result().page + 1)"
          >
            Next
          </button>
        </div>
      }
    </section>`,
  styles: [
    `
      .page-head {
        padding: 78px 0 62px;
        background: var(--surface-soft);
      }
      .page-head h1 {
        font-size: clamp(3rem, 7vw, 5.3rem);
        margin: 14px 0;
      }
      .page-head p:last-child {
        color: var(--muted);
        font-size: 1.04rem;
      }
      .listing {
        padding: 38px 0 90px;
      }
      .filter-heading {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 12px;
      }
      .filter-heading > div,
      .filter-heading button {
        display: inline-flex;
        align-items: center;
        gap: 7px;
      }
      .filter-heading b {
        font-size: 0.86rem;
      }
      .filter-heading svg {
        width: 16px;
        height: 16px;
      }
      .filter-heading button {
        border: 0;
        padding: 7px 9px;
        border-radius: 8px;
        background: transparent;
        color: var(--muted);
        cursor: pointer;
        font-size: 0.78rem;
        font-weight: 650;
      }
      .filter-heading button:hover {
        color: var(--ink);
        background: var(--surface-muted);
      }
      .filters {
        padding: 14px;
        border: 1px solid var(--border);
        border-radius: var(--radius-md);
        background: var(--surface);
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(145px, 1fr));
        gap: 10px;
      }
      .filters input,
      .filters select {
        width: 100%;
        height: 48px;
        border: 1px solid var(--border);
        background: var(--surface-strong);
        border-radius: var(--radius-sm);
        padding: 0 12px;
        color: var(--ink);
      }
      .search {
        position: relative;
        grid-column: span 2;
      }
      .search svg {
        position: absolute;
        left: 14px;
        top: 15px;
        width: 18px;
        height: 18px;
        color: var(--muted);
      }
      .search input {
        padding-left: 42px;
      }
      .result-bar {
        height: 66px;
        display: flex;
        align-items: center;
        justify-content: space-between;
        color: var(--muted);
        font-size: 0.83rem;
      }
      .loading {
        color: var(--bronze);
      }
      .property-grid {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 22px;
      }
      .pagination {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 20px;
        margin-top: 42px;
        font-size: 0.83rem;
      }
      .skeleton {
        height: 390px;
        border-radius: var(--radius-md);
        background: linear-gradient(
          90deg,
          var(--skeleton-start),
          var(--skeleton-mid),
          var(--skeleton-start)
        );
        background-size: 200%;
        animation: shine 1.4s infinite;
      }
      @keyframes shine {
        to {
          background-position: -200%;
        }
      }
      @media (max-width: 1050px) {
        .filters {
          grid-template-columns: repeat(3, 1fr);
        }
        .search {
          grid-column: span 2;
        }
        .property-grid {
          grid-template-columns: repeat(2, 1fr);
        }
      }
      @media (max-width: 650px) {
        .filters {
          grid-template-columns: 1fr 1fr;
        }
        .search {
          grid-column: 1/-1;
        }
        .property-grid {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class PropertiesListComponent {
  private service = inject(PropertyService);
  private blockService = inject(BlockService);
  private destroyRef = inject(DestroyRef);
  readonly types = PROPERTY_TYPE_OPTIONS;
  readonly storageOptions = Array.from(
    new Set(PROPERTY_TYPE_OPTIONS.map((option) => option.storageCapacity)),
  ).map((value) => ({ value, label: value.toLocaleString('en-US') }));
  readonly skeletons = [1, 2, 3, 4, 5, 6];
  readonly blocks = signal<Block[]>([]);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly result = signal<PagedResult<PublicProperty>>({
    items: [],
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
    totalItems: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  });
  readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    blockId: new FormControl('', { nonNullable: true }),
    type: new FormControl<PropertyType | ''>('', { nonNullable: true }),
    personCapacity: new FormControl<number | null>(null),
    minRent: new FormControl<number | null>(null),
    maxRent: new FormControl<number | null>(null),
    storageCapacity: new FormControl<number | null>(null),
    sort: new FormControl('newest:desc', { nonNullable: true }),
    page: new FormControl(1, { nonNullable: true }),
  });
  constructor() {
    this.blockService.public().subscribe((v) => this.blocks.set(v));
    this.filters.valueChanges
      .pipe(
        debounceTime(280),
        startWith(this.filters.getRawValue()),
        switchMap((value) => {
          this.loading.set(true);
          this.error.set(false);
          const [sortBy, sortDirection] = value.sort!.split(':');
          const query: PropertyQuery = {
            ...value,
            pageSize: DEFAULT_PAGE_SIZE,
            sortBy,
            sortDirection: sortDirection as 'asc' | 'desc',
          };
          return this.service.available(query).pipe(
            catchError(() => {
              this.error.set(true);
              return of(this.result());
            }),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this.result.set(result));
  }
  clear() {
    this.filters.reset({
      search: '',
      blockId: '',
      type: '',
      personCapacity: null,
      minRent: null,
      maxRent: null,
      storageCapacity: null,
      sort: 'newest:desc',
      page: 1,
    });
  }
  page(page: number) {
    this.filters.controls.page.setValue(page);
    window.scrollTo({ top: 350, behavior: 'smooth' });
  }
  reload() {
    this.filters.controls.page.setValue(this.filters.controls.page.value, { emitEvent: true });
  }
}
