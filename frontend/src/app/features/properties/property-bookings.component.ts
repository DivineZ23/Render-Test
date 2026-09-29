import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  LucideArrowLeft,
  LucideCalendarPlus,
  LucideClipboardList,
  LucideTrash2,
} from '@lucide/angular';
import { filter, forkJoin, switchMap } from 'rxjs';
import { propertyTypeLabel } from '../../core/constants/property-status.constants';
import { Property, PropertyBooking } from '../../core/models/property.models';
import { AuthService } from '../../core/services/auth.service';
import { PropertyService } from '../../core/services/property.service';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-property-bookings',
  imports: [
    CurrencyPipe,
    DatePipe,
    RouterLink,
    EmptyStateComponent,
    StatusBadgeComponent,
    LucideArrowLeft,
    LucideCalendarPlus,
    LucideClipboardList,
    LucideTrash2,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="back">
      <a routerLink="/dashboard/properties"><svg lucideArrowLeft></svg>Back to properties</a>
    </div>

    @if (property(); as selected) {
      <div class="page-title">
        <div>
          <p class="eyebrow">Portfolio · Property bookings</p>
          <h1>{{ selected.propertyName }}</h1>
          <p>Review every active booking recorded for this property.</p>
        </div>
        @if (canBook(selected)) {
          <a class="btn btn-primary" [routerLink]="['/dashboard/properties', selected.id, 'book']">
            <svg lucideCalendarPlus></svg>Add another booking
          </a>
        }
      </div>

      <section class="panel property-strip">
        <div class="property-identity">
          <span class="context-icon"><svg lucideClipboardList></svg></span>
          <div>
            <small>Selected property</small>
            <b>#{{ selected.propertyId }} · {{ selected.blockName }}</b>
          </div>
        </div>
        <div>
          <small>Interior</small><b>{{ typeLabel(selected.type) }}</b>
        </div>
        <div>
          <small>Listed rent</small
          ><b>{{ selected.rent | currency: 'USD' : 'symbol' : '1.0-0' }}</b>
        </div>
        <div>
          <small>Active bookings</small><b>{{ bookings().length }}</b>
        </div>
        <app-status-badge [status]="selected.status" />
      </section>

      <section class="panel bookings-panel">
        <header>
          <div>
            <p class="eyebrow">Current bookings</p>
            <h2>{{ bookings().length }} {{ bookings().length === 1 ? 'booking' : 'bookings' }}</h2>
          </div>
          @if (auth.isManager() && bookings().length) {
            <button
              class="btn btn-danger"
              type="button"
              [disabled]="closingAll()"
              (click)="closeAll()"
            >
              <svg lucideTrash2></svg>{{ closingAll() ? 'Closing…' : 'Close all bookings' }}
            </button>
          }
        </header>

        @if (loading()) {
          <div class="loading">Loading current bookings…</div>
        } @else if (bookings().length) {
          <div class="table-wrap">
            <table class="data-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>CID</th>
                  <th>Number</th>
                  <th>Discord ID</th>
                  <th>Rent</th>
                  <th>Booking amount</th>
                  <th>Booked by</th>
                  <th>Added</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (booking of bookings(); track booking.id) {
                  <tr>
                    <td>
                      <b>{{ booking.fullName }}</b>
                      @if (booking.notes) {
                        <small class="notes">{{ booking.notes }}</small>
                      }
                    </td>
                    <td>{{ booking.cid }}</td>
                    <td>{{ booking.phoneNumber }}</td>
                    <td class="discord">{{ booking.discordId }}</td>
                    <td>{{ booking.monthlyRent | currency: 'USD' : 'symbol' : '1.0-0' }}</td>
                    <td>{{ booking.bookingAmount | currency: 'USD' : 'symbol' : '1.0-0' }}</td>
                    <td>{{ booking.createdByDisplayName || 'Unknown user' }}</td>
                    <td>{{ booking.createdAt | date: 'medium' }}</td>
                    <td>
                      <button
                        type="button"
                        class="icon-action danger"
                        [disabled]="removing() === booking.id"
                        (click)="remove(booking)"
                        [attr.aria-label]="'Remove booking for ' + booking.fullName"
                        title="Remove booking"
                      >
                        <svg lucideTrash2></svg>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <app-empty-state
            title="No active bookings"
            message="Add the first booking for this property when a buyer expresses interest."
          />
        }
      </section>
    } @else if (!loading()) {
      <section class="panel error-state">
        This property or its bookings could not be loaded.
      </section>
    }
  `,
  styles: [
    `
      .back {
        margin-bottom: 20px;
        color: var(--muted);
        font-size: 0.8rem;
      }
      .back a,
      .property-identity,
      .page-title,
      .property-strip {
        display: flex;
        align-items: center;
      }
      .back a {
        width: fit-content;
        gap: 7px;
      }
      .back svg,
      .btn svg,
      .icon-action svg {
        width: 16px;
        height: 16px;
      }
      .page-title {
        justify-content: space-between;
        gap: 24px;
        margin-bottom: 22px;
      }
      .page-title h1 {
        margin: 4px 0;
        font-size: 2.5rem;
      }
      .page-title p:last-child {
        margin: 0;
        color: var(--muted);
      }
      .property-strip {
        display: grid;
        grid-template-columns: minmax(220px, 1.35fr) repeat(3, minmax(120px, 0.65fr)) auto;
        gap: 24px;
        margin-bottom: 18px;
        padding: 18px 20px;
      }
      .property-identity {
        gap: 12px;
      }
      .context-icon {
        display: grid;
        width: 38px;
        height: 38px;
        flex: 0 0 auto;
        place-items: center;
        border-radius: 10px;
        background: var(--forest-light);
        color: var(--forest);
      }
      .context-icon svg {
        width: 18px;
      }
      .property-strip small,
      .notes {
        display: block;
        color: var(--muted);
        font-size: 0.7rem;
      }
      .property-strip b {
        display: block;
        margin-top: 3px;
        font-size: 0.82rem;
      }
      .bookings-panel {
        overflow: hidden;
      }
      .bookings-panel > header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 18px;
        padding: 20px 22px;
        border-bottom: 1px solid var(--border);
      }
      .bookings-panel h2 {
        margin: 4px 0 0;
        font-size: 1.25rem;
      }
      .table-wrap {
        overflow-x: auto;
      }
      .data-table {
        min-width: 1100px;
      }
      .notes {
        max-width: 230px;
        margin-top: 4px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .discord {
        font-variant-numeric: tabular-nums;
      }
      .icon-action {
        display: grid;
        width: 32px;
        height: 32px;
        place-items: center;
        border: 0;
        border-radius: 8px;
        cursor: pointer;
      }
      .icon-action.danger {
        background: var(--danger-soft);
        color: var(--danger);
      }
      .icon-action:disabled {
        cursor: wait;
        opacity: 0.55;
      }
      .loading,
      .error-state {
        padding: 32px 22px;
        color: var(--muted);
      }
      @media (max-width: 900px) {
        .property-strip {
          grid-template-columns: 1fr 1fr;
        }
      }
      @media (max-width: 640px) {
        .page-title {
          align-items: flex-start;
          flex-direction: column;
        }
        .property-strip {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class PropertyBookingsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly properties = inject(PropertyService);
  readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly property = signal<Property | null>(null);
  readonly bookings = signal<PropertyBooking[]>([]);

  canBook(property: Property): boolean {
    return (
      property.status === 'available' ||
      property.status === 'booked' ||
      (property.allowOccupiedBookings &&
        Boolean(property.currentTenantId) &&
        ['paid', 'overdue', 'evictable'].includes(property.status))
    );
  }
  readonly loading = signal(true);
  readonly removing = signal<string | null>(null);
  readonly closingAll = signal(false);
  readonly typeLabel = propertyTypeLabel;

  constructor() {
    const id = this.route.snapshot.paramMap.get('id')!;
    forkJoin({
      property: this.properties.details(id),
      bookings: this.properties.bookings(id),
    }).subscribe({
      next: ({ property, bookings }) => {
        this.property.set(property);
        this.bookings.set(bookings);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  remove(booking: PropertyBooking) {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove this booking?',
          message: `${booking.fullName}'s booking will be removed from this property.`,
          confirmLabel: 'Remove booking',
          dangerous: true,
        },
      })
      .afterClosed()
      .pipe(
        filter((result) => result?.confirmed),
        switchMap(() => {
          this.removing.set(booking.id);
          return this.properties.cancelBooking(booking.propertyId, booking.id);
        }),
      )
      .subscribe({
        next: () => {
          this.bookings.update((values) => values.filter((value) => value.id !== booking.id));
          this.removing.set(null);
          if (this.bookings().length === 0) {
            this.property.update((value) =>
              value ? { ...value, status: 'available', bookingCount: 0 } : value,
            );
          } else {
            this.property.update((value) =>
              value ? { ...value, bookingCount: this.bookings().length } : value,
            );
          }
          if (this.auth.isManager()) {
            this.properties.refreshBookingAnnouncementCount().subscribe({ error: () => undefined });
          }
          this.snackBar.open('Booking removed.', 'Dismiss', { duration: 2500 });
        },
        error: () => {
          this.removing.set(null);
          this.snackBar.open('The booking could not be removed.', 'Dismiss', {
            duration: 3500,
            panelClass: ['error-toast'],
          });
        },
      });
  }

  closeAll() {
    const property = this.property();
    if (!property || !this.bookings().length || this.closingAll()) return;
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Close all bookings?',
          message: `All ${this.bookings().length} active bookings for ${property.propertyName} will be closed.`,
          confirmLabel: 'Close all bookings',
          dangerous: true,
        },
      })
      .afterClosed()
      .pipe(
        filter((result) => result?.confirmed),
        switchMap(() => {
          this.closingAll.set(true);
          return this.properties.closeAllBookings(property.id);
        }),
      )
      .subscribe({
        next: () => {
          this.bookings.set([]);
          this.property.update((value) =>
            value
              ? {
                  ...value,
                  status:
                    value.status === 'booked' && !value.currentTenantId
                      ? 'available'
                      : value.status,
                  bookingCount: 0,
                }
              : value,
          );
          this.closingAll.set(false);
          this.properties.refreshBookingAnnouncementCount().subscribe({ error: () => undefined });
          this.snackBar.open('All bookings closed.', 'Dismiss', { duration: 2800 });
        },
        error: () => {
          this.closingAll.set(false);
          this.snackBar.open('The bookings could not be closed.', 'Dismiss', {
            duration: 3500,
            panelClass: ['error-toast'],
          });
        },
      });
  }
}
