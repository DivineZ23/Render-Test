import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';
import {
  LucideCalendarPlus,
  LucideClipboardList,
  LucideChevronLeft,
  LucideChevronRight,
  LucidePauseCircle,
  LucidePencil,
  LucidePlus,
  LucideTrash2,
  LucideUndo2,
  LucideUserMinus,
  LucideUserPlus,
} from '@lucide/angular';
import { catchError, debounceTime, finalize, of, startWith, switchMap } from 'rxjs';
import {
  PROPERTY_STATUSES,
  PROPERTY_TYPE_OPTIONS,
  propertyTypeCapacity,
  propertyTypeLabel,
} from '../../core/constants/property-status.constants';
import { PagedResult } from '../../core/models/api.models';
import { Block, Property, PropertyStatus, PropertyType } from '../../core/models/property.models';
import { AuthService } from '../../core/services/auth.service';
import { BlockService } from '../../core/services/management.services';
import { PropertyService } from '../../core/services/property.service';
import { PageAccessService } from '../../core/services/page-access.service';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { EvictTenantDialogComponent } from './evict-tenant-dialog.component';

@Component({
  selector: 'app-property-management',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    EmptyStateComponent,
    LucideCalendarPlus,
    LucideClipboardList,
    LucideChevronLeft,
    LucideChevronRight,
    LucidePauseCircle,
    LucidePencil,
    LucidePlus,
    LucideTrash2,
    LucideUndo2,
    LucideUserMinus,
    LucideUserPlus,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page-title">
      <div>
        <p class="eyebrow">Portfolio</p>
        <h1>Properties</h1>
        <p>Search, update, and progress every property through its lifecycle.</p>
      </div>
      @if (auth.isManager() && access.canAccess('portfolio.properties.add')) {
        <a class="btn btn-primary" routerLink="new"><svg lucidePlus></svg>Add property</a>
      }
    </div>
    <form [formGroup]="filters" class="toolbar panel">
      <input
        formControlName="search"
        placeholder="Search name, block, or ID"
        aria-label="Search properties"
      /><select formControlName="status">
        <option value="">All statuses</option>
        @for (status of statuses; track status.value) {
          <option [value]="status.value">{{ status.label }}</option>
        }</select
      ><select formControlName="type">
        <option value="">All types</option>
        @for (type of types; track type.value) {
          <option [value]="type.value">{{ type.label }}</option>
        }</select
      ><select formControlName="blockId">
        <option value="">All blocks</option>
        @for (block of blocks(); track block.id) {
          <option [value]="block.id">{{ block.blockName }}</option>
        }
      </select>
    </form>
    <div class="panel table-wrap">
      @if (loading()) {
        <div class="loading">Updating portfolio…</div>
      }
      @if (error()) {
        <div class="loading" role="alert">
          Could not load properties.
          <button class="btn btn-secondary" (click)="refresh()">Try again</button>
        </div>
      }
      <table class="data-table">
        <colgroup>
          <col class="col-id" />
          <col class="col-property" />
          <col class="col-block" />
          <col class="col-type" />
          <col class="col-rent" />
          <col class="col-status" />
          <col class="col-tenant" />
          <col class="col-cid" />
          <col class="col-number" />
          <col class="col-paid" />
          <col class="col-actions" />
        </colgroup>
        <thead>
          <tr>
            <th [attr.aria-sort]="idSortDirection() === 'asc' ? 'ascending' : 'descending'">
              <button class="sort-button" type="button" (click)="toggleIdSort()">
                ID <span aria-hidden="true">{{ idSortDirection() === 'asc' ? '↑' : '↓' }}</span>
              </button>
            </th>
            <th>Property</th>
            <th>Block</th>
            <th>Type</th>
            <th>Rent</th>
            <th>Status</th>
            <th>Tenant</th>
            <th>CID</th>
            <th>Number</th>
            <th>Rent paid till</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (property of result().items; track property.id) {
            <tr>
              <td>#{{ property.propertyId }}</td>
              <td>
                <b>{{ property.propertyName }}</b>
              </td>
              <td>{{ property.blockName }}</td>
              <td>
                {{ typeLabel(property.type) }}
                @if (property.personCapacity ?? typeCapacity(property.type); as capacity) {
                  <small>{{ capacity }} {{ capacity === 1 ? 'person' : 'people' }}</small>
                }
              </td>
              <td>{{ property.rent | currency: 'USD' : 'symbol' : '1.0-0' }}</td>
              <td><app-status-badge [status]="property.status" /></td>
              <td>{{ property.tenantName || '—' }}</td>
              <td>{{ property.tenantCid ?? '—' }}</td>
              <td>{{ property.tenantPhoneNumber || '—' }}</td>
              <td>
                {{
                  property.rentPaidThrough ? (property.rentPaidThrough | date: 'mediumDate') : '—'
                }}
              </td>
              <td>
                <div class="row-actions">
                  @if (
                    access.canAccess('portfolio.properties.sell') &&
                    (property.status === 'available' || property.status === 'booked')
                  ) {
                    <a [routerLink]="[property.id, 'assign']"><svg lucideUserPlus></svg>Sell</a>
                  }
                  @if (
                    property.currentTenantId &&
                    auth.canEvict() &&
                    access.canAccess('portfolio.properties.evict')
                  ) {
                    <button class="danger" (click)="evict(property)">
                      <svg lucideUserMinus></svg>Evict
                    </button>
                  }
                  @if (access.canAccess('portfolio.properties.book') && canBook(property)) {
                    <a [routerLink]="[property.id, 'book']"><svg lucideCalendarPlus></svg>Book</a>
                  }
                  @if (access.canAccess('portfolio.properties.book') && property.bookingCount > 0) {
                    <a [routerLink]="[property.id, 'bookings']">
                      <svg lucideClipboardList></svg>Bookings ({{ property.bookingCount }})
                    </a>
                  }
                  @if (
                    property.status === 'booked' ||
                    (auth.isManager() &&
                      (property.status === 'auction' || property.status === 'onHold'))
                  ) {
                    <button (click)="release(property)"><svg lucideUndo2></svg>Release</button>
                  }
                  @if (auth.isManager()) {
                    @if (!property.currentTenantId && property.status !== 'onHold') {
                      <button (click)="hold(property)"><svg lucidePauseCircle></svg>Hold</button>
                    }
                    @if (access.canAccess('portfolio.properties.edit')) {
                      <a
                        class="edit-action"
                        [routerLink]="[property.id, 'edit']"
                        [attr.aria-label]="'Edit ' + property.propertyName"
                        title="Edit property"
                      >
                        <svg lucidePencil></svg>
                      </a>
                    }
                    <button class="danger" (click)="remove(property)">
                      <svg lucideTrash2></svg>Delete
                    </button>
                  }
                </div>
              </td>
            </tr>
          } @empty {
            @if (!loading() && !error()) {
              <tr>
                <td colspan="11">
                  <app-empty-state
                    title="No properties found"
                    message="Adjust your filters or add the first property."
                  />
                </td>
              </tr>
            }
          }
        </tbody>
      </table>
    </div>
    <div class="pagination">
      <button
        class="btn btn-secondary"
        [disabled]="!result().hasPreviousPage"
        (click)="page(result().page - 1)"
      >
        <svg lucideChevronLeft></svg>Previous</button
      ><span>Page {{ result().page }} of {{ result().totalPages || 1 }}</span
      ><button
        class="btn btn-secondary"
        [disabled]="!result().hasNextPage"
        (click)="page(result().page + 1)"
      >
        Next<svg lucideChevronRight></svg>
      </button>
    </div>`,
  styles: [
    `
      .page-title {
        display: flex;
        justify-content: space-between;
        align-items: end;
        margin-bottom: 24px;
      }
      .page-title h1 {
        font-size: 2.5rem;
        margin: 4px 0;
      }
      .page-title p:last-child {
        color: var(--muted);
        margin: 0;
      }
      .toolbar {
        display: grid;
        grid-template-columns: 2fr repeat(3, 1fr);
        gap: 10px;
        padding: 14px;
        margin-bottom: 18px;
      }
      .toolbar input,
      .toolbar select {
        height: 42px;
        border: 1px solid var(--border);
        border-radius: 9px;
        background: var(--surface-strong);
        color: var(--ink);
        padding: 0 12px;
      }
      .loading {
        padding: 12px;
        color: var(--bronze);
        font-size: 0.8rem;
      }
      .table-wrap {
        overflow-x: visible;
      }
      .data-table {
        table-layout: fixed;
      }
      .data-table th,
      .data-table td {
        padding: 11px 8px;
        overflow-wrap: normal;
        word-break: normal;
      }
      .data-table th {
        font-size: 0.68rem;
        letter-spacing: 0.045em;
      }
      .col-id {
        width: 5%;
      }
      .col-property {
        width: 13%;
      }
      .col-block {
        width: 10%;
      }
      .col-type {
        width: 11%;
      }
      .col-rent {
        width: 6%;
      }
      .col-status {
        width: 7%;
      }
      .col-tenant {
        width: 8%;
      }
      .col-cid {
        width: 5%;
      }
      .col-number {
        width: 7%;
      }
      .col-paid {
        width: 8%;
      }
      .col-actions {
        width: 20%;
      }
      .sort-button {
        display: inline-flex;
        align-items: center;
        gap: 4px;
        padding: 0;
        border: 0;
        background: transparent;
        color: inherit;
        font: inherit;
        font-weight: 750;
        letter-spacing: inherit;
        text-transform: inherit;
        cursor: pointer;
      }
      .row-actions {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: 7px 10px;
      }
      .data-table td small {
        display: block;
        color: var(--muted);
        font-size: 0.7rem;
      }
      .row-actions a,
      .row-actions button {
        border: 0;
        background: none;
        color: var(--forest);
        font-weight: 700;
        font-size: 0.72rem;
        cursor: pointer;
        display: inline-flex;
        align-items: center;
        gap: 5px;
        white-space: nowrap;
      }
      .row-actions svg {
        width: 14px;
        height: 14px;
        stroke-width: 1.9;
      }
      .row-actions .danger {
        color: var(--danger);
      }
      .row-actions .edit-action {
        display: grid;
        place-items: center;
        width: 26px;
        height: 26px;
        padding: 0;
        border: 1px solid var(--border);
        border-radius: 7px;
        color: var(--muted);
      }
      .row-actions .edit-action:hover {
        border-color: var(--forest);
        color: var(--forest);
      }
      .row-actions .edit-action svg {
        width: 14px;
        height: 14px;
      }
      .pagination {
        display: flex;
        justify-content: center;
        align-items: center;
        gap: 20px;
        margin-top: 24px;
        font-size: 0.8rem;
      }
      @media (max-width: 850px) {
        .toolbar {
          grid-template-columns: 1fr 1fr;
        }
        .page-title .btn {
          display: none;
        }
      }
    `,
  ],
})
export class PropertyManagementComponent {
  readonly auth = inject(AuthService);
  readonly access = inject(PageAccessService);
  private service = inject(PropertyService);
  private blockService = inject(BlockService);
  private dialog = inject(MatDialog);
  private destroy = inject(DestroyRef);
  readonly statuses = PROPERTY_STATUSES;
  readonly types = PROPERTY_TYPE_OPTIONS;
  readonly typeLabel = propertyTypeLabel;
  readonly typeCapacity = propertyTypeCapacity;

  canBook(property: Property): boolean {
    return (
      property.status === 'available' ||
      property.status === 'booked' ||
      (property.allowOccupiedBookings &&
        Boolean(property.currentTenantId) &&
        ['paid', 'overdue', 'evictable'].includes(property.status))
    );
  }
  readonly blocks = signal<Block[]>([]);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly result = signal<PagedResult<Property>>({
    items: [],
    page: 1,
    pageSize: 10,
    totalItems: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  });
  readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    status: new FormControl<PropertyStatus | ''>('', { nonNullable: true }),
    type: new FormControl<PropertyType | ''>('', { nonNullable: true }),
    blockId: new FormControl('', { nonNullable: true }),
    sortBy: new FormControl('id', { nonNullable: true }),
    sortDirection: new FormControl<'asc' | 'desc'>('asc', { nonNullable: true }),
    page: new FormControl(1, { nonNullable: true }),
  });
  constructor() {
    this.blockService.all().subscribe((v) => this.blocks.set(v));
    this.filters.valueChanges
      .pipe(
        debounceTime(220),
        startWith(this.filters.getRawValue()),
        switchMap((value) => {
          this.loading.set(true);
          this.error.set(false);
          return this.service.all({ ...value, pageSize: 10 }).pipe(
            catchError(() => {
              this.error.set(true);
              return of(this.result());
            }),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroy),
      )
      .subscribe((v) => this.result.set(v));
  }
  page(page: number) {
    this.filters.controls.page.setValue(page);
  }
  idSortDirection(): 'asc' | 'desc' {
    return this.filters.controls.sortDirection.value;
  }
  toggleIdSort() {
    this.filters.patchValue({
      sortBy: 'id',
      sortDirection: this.idSortDirection() === 'asc' ? 'desc' : 'asc',
      page: 1,
    });
  }
  refresh() {
    this.filters.controls.page.setValue(this.filters.controls.page.value, { emitEvent: true });
  }
  release(property: Property) {
    const source =
      property.status === 'booked'
        ? 'booking'
        : property.status === 'auction'
          ? 'auction listing'
          : 'hold';
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Release this property?',
          message: `The current ${source} will be removed and the property will become available.`,
          confirmLabel: 'Release',
        },
      })
      .afterClosed()
      .subscribe((result) => {
        if (result?.confirmed)
          this.service.changeStatus(property.id, 'available').subscribe(() => this.refresh());
      });
  }
  hold(property: Property) {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Place this property on hold?',
          message: 'It will not be available for booking, sale, or auction until it is released.',
          confirmLabel: 'Place on hold',
          requireReason: true,
        },
      })
      .afterClosed()
      .subscribe((result) => {
        if (result?.confirmed)
          this.service
            .changeStatus(property.id, 'onHold', result.reason)
            .subscribe(() => this.refresh());
      });
  }
  evict(property: Property) {
    this.dialog
      .open(EvictTenantDialogComponent, { data: property })
      .afterClosed()
      .subscribe((result) => {
        if (result?.confirmed)
          this.service
            .evict(property.id, {
              reason: result.reason,
              storageImageUrls: result.storageImageUrls,
            })
            .subscribe(() => this.refresh());
      });
  }
  remove(property: Property) {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Delete property?',
          message:
            'The property will be deactivated and removed from active management views. History is retained.',
          dangerous: true,
          confirmLabel: 'Delete',
        },
      })
      .afterClosed()
      .subscribe((result) => {
        if (result?.confirmed) this.service.delete(property.id).subscribe(() => this.refresh());
      });
  }
}
