import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import {
  LucideBuilding2,
  LucideBell,
  LucideCalendarPlus,
  LucideChevronDown,
  LucideClipboardList,
  LucideTrash2,
} from '@lucide/angular';
import { filter, switchMap } from 'rxjs';
import { propertyTypeLabel } from '../../core/constants/property-status.constants';
import { PropertyBookingGroup } from '../../core/models/property.models';
import { PropertyService } from '../../core/services/property.service';
import { AuthService } from '../../core/services/auth.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-portfolio-bookings',
  imports: [
    CurrencyPipe,
    DatePipe,
    RouterLink,
    EmptyStateComponent,
    StatusBadgeComponent,
    LucideBuilding2,
    LucideBell,
    LucideCalendarPlus,
    LucideChevronDown,
    LucideClipboardList,
    LucideTrash2,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-title">
      <div>
        <p class="eyebrow">Portfolio</p>
        <h1>Bookings</h1>
        <p>Review active bookings across every property in one place.</p>
      </div>
    </div>

    <section class="booking-summary" aria-label="Booking summary">
      <article class="panel">
        <span><svg lucideBuilding2></svg></span>
        <div>
          <small>Properties with bookings</small><b>{{ groups().length }}</b>
        </div>
      </article>
      <article class="panel">
        <span><svg lucideClipboardList></svg></span>
        <div>
          <small>Total active bookings</small><b>{{ totalBookings() }}</b>
        </div>
      </article>
      <article class="panel">
        <span><svg lucideCalendarPlus></svg></span>
        <div>
          <small>Total booking amount</small>
          <b>{{ totalBookingAmount() | currency: 'USD' : 'symbol' : '1.0-0' }}</b>
        </div>
      </article>
    </section>

    @if (loading()) {
      <section class="panel state">Loading portfolio bookings…</section>
    } @else if (groups().length) {
      <div class="booking-groups">
        @for (group of groups(); track group.propertyId) {
          <details class="panel booking-group">
            <summary>
              <span class="property-icon"><svg lucideBuilding2></svg></span>
              <span class="property-name">
                <small>#{{ group.propertyNumber }} · {{ group.blockName }}</small>
                <b>{{ group.propertyName }}</b>
              </span>
              <span class="property-type">
                <small>Interior</small><b>{{ typeLabel(group.type) }}</b>
              </span>
              <app-status-badge [status]="group.status" />
              <span class="booking-count">
                <b>{{ group.bookings.length }}</b>
                <small>{{ group.bookings.length === 1 ? 'booking' : 'bookings' }}</small>
              </span>
              <svg class="chevron" lucideChevronDown></svg>
            </summary>

            <div class="group-content">
              <div class="group-actions">
                <div class="group-context">
                  <a [routerLink]="['/dashboard/properties', group.propertyId, 'bookings']">
                    Open property bookings
                  </a>
                  @if (
                    auth.isManager() &&
                    (group.bookingAnnouncementPending || group.bookingAnnouncementPostedAt)
                  ) {
                    <label
                      class="announcement-control"
                      [class.pending]="group.bookingAnnouncementPending"
                      [title]="
                        group.bookingAnnouncementPending
                          ? 'Check after this property has been posted in the Discord channel.'
                          : 'This property has been posted in the Discord channel.'
                      "
                    >
                      <svg lucideBell></svg>
                      <input
                        type="checkbox"
                        [checked]="!group.bookingAnnouncementPending"
                        [disabled]="updatingAnnouncement() === group.propertyId"
                        (change)="setAnnouncementPosted(group, $any($event.target).checked)"
                      />
                      <span>
                        <b>{{
                          group.bookingAnnouncementPending
                            ? 'Discord post pending'
                            : 'Posted to Discord'
                        }}</b>
                        <small>
                          @if (!group.bookingAnnouncementPending) {
                            {{ group.bookingAnnouncementPostedByDisplayName || 'A manager' }} ·
                            {{ group.bookingAnnouncementPostedAt | date: 'mediumDate' }}
                          }
                        </small>
                      </span>
                    </label>
                  }
                </div>
                <div class="booking-actions">
                  @if (canBook(group)) {
                    <a
                      class="btn btn-secondary"
                      [routerLink]="['/dashboard/properties', group.propertyId, 'book']"
                    >
                      <svg lucideCalendarPlus></svg>Add booking
                    </a>
                  }
                  @if (auth.isManager()) {
                    <button
                      class="btn btn-danger"
                      type="button"
                      [disabled]="closingGroup() === group.propertyId"
                      (click)="closeAll(group)"
                    >
                      <svg lucideTrash2></svg>
                      {{ closingGroup() === group.propertyId ? 'Closing…' : 'Close all' }}
                    </button>
                  }
                </div>
              </div>
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
                      <th>Booked on</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (booking of group.bookings; track booking.id) {
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
                        <td>
                          <time class="booking-timestamp" [attr.datetime]="booking.createdAt">
                            <b>{{ booking.createdAt | date: 'mediumDate' }}</b>
                            <span>at {{ booking.createdAt | date: 'shortTime' }}</span>
                          </time>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </details>
        }
      </div>
    } @else {
      <section class="panel empty-wrap">
        <app-empty-state
          title="No active bookings"
          message="Property bookings will appear here after the first booking is added."
        />
      </section>
    }
  `,
  styles: [
    `
      .page-title {
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
      .booking-summary {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: 14px;
        margin-bottom: 18px;
      }
      .booking-summary article {
        display: flex;
        align-items: center;
        gap: 13px;
        padding: 17px 18px;
      }
      .booking-summary article > span,
      .property-icon {
        display: grid;
        width: 38px;
        height: 38px;
        flex: 0 0 auto;
        place-items: center;
        border-radius: 10px;
        background: var(--forest-light);
        color: var(--forest);
      }
      .booking-summary svg,
      .property-icon svg,
      .btn svg {
        width: 17px;
      }
      .booking-summary small,
      summary small,
      .notes {
        display: block;
        color: var(--muted);
        font-size: 0.7rem;
      }
      .booking-summary b {
        display: block;
        margin-top: 2px;
        font-size: 1.25rem;
      }
      .booking-groups {
        display: grid;
        gap: 12px;
      }
      .booking-group {
        overflow: hidden;
      }
      summary {
        display: grid;
        grid-template-columns: auto minmax(210px, 1.3fr) minmax(150px, 0.75fr) auto auto auto;
        align-items: center;
        gap: 18px;
        padding: 16px 18px;
        cursor: pointer;
        list-style: none;
      }
      summary::-webkit-details-marker {
        display: none;
      }
      summary:hover {
        background: var(--surface-hover);
      }
      .property-name b,
      .property-type b {
        display: block;
        margin-top: 3px;
        font-size: 0.84rem;
      }
      .booking-count {
        min-width: 68px;
        text-align: right;
      }
      .booking-count b {
        display: block;
        font-size: 1rem;
      }
      .chevron {
        width: 18px;
        color: var(--muted);
        transition: transform 160ms ease;
      }
      details[open] .chevron {
        transform: rotate(180deg);
      }
      .group-content {
        border-top: 1px solid var(--border);
      }
      .group-actions {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 16px;
        padding: 12px 18px;
        background: var(--surface-soft);
      }
      .booking-actions {
        display: flex;
        align-items: center;
        gap: 8px;
        flex-shrink: 0;
      }
      .booking-actions svg {
        width: 16px;
        height: 16px;
      }
      .group-actions > a:first-child {
        color: var(--forest);
        font-size: 0.76rem;
        font-weight: 700;
      }
      .group-context {
        display: flex;
        align-items: center;
        gap: 14px;
        min-width: 0;
      }
      .group-context > a {
        color: var(--forest);
        font-size: 0.76rem;
        font-weight: 700;
        white-space: nowrap;
      }
      .announcement-control {
        display: flex;
        align-items: center;
        gap: 6px;
        padding: 5px 8px;
        border: 1px solid var(--border);
        border-radius: 7px;
        background: var(--surface);
        cursor: pointer;
      }
      .announcement-control.pending {
        border-color: color-mix(in srgb, var(--warning-ink) 45%, var(--border));
        background: var(--warning-soft);
      }
      .announcement-control span {
        display: grid;
        gap: 1px;
      }
      .announcement-control > svg {
        width: 14px;
        height: 14px;
        flex: 0 0 auto;
        color: var(--warning-ink);
      }
      .announcement-control input {
        width: 14px;
        height: 14px;
        margin: 0;
      }
      .announcement-control b {
        font-size: 0.68rem;
      }
      .announcement-control small {
        font-size: 0.6rem;
      }
      .table-wrap {
        overflow-x: auto;
      }
      .data-table {
        min-width: 1050px;
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
      .booking-timestamp {
        display: grid;
        gap: 2px;
        white-space: nowrap;
        font-variant-numeric: tabular-nums;
      }
      .booking-timestamp b {
        font-size: 0.78rem;
      }
      .booking-timestamp span {
        color: var(--muted);
        font-size: 0.7rem;
      }
      .state,
      .empty-wrap {
        padding: 32px 22px;
        color: var(--muted);
      }
      @media (max-width: 850px) {
        .booking-summary {
          grid-template-columns: 1fr;
        }
        summary {
          grid-template-columns: auto 1fr auto auto;
        }
        .property-type,
        summary app-status-badge {
          display: none;
        }
      }
      @media (max-width: 520px) {
        .group-actions {
          align-items: flex-start;
          flex-direction: column;
        }
        .group-context {
          align-items: flex-start;
          flex-direction: column;
        }
      }
    `,
  ],
})
export class PortfolioBookingsComponent {
  private readonly properties = inject(PropertyService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  readonly auth = inject(AuthService);

  readonly groups = signal<PropertyBookingGroup[]>([]);
  readonly updatingAnnouncement = signal<string | null>(null);
  readonly closingGroup = signal<string | null>(null);

  canBook(group: PropertyBookingGroup): boolean {
    return (
      group.status === 'available' ||
      group.status === 'booked' ||
      (group.allowOccupiedBookings &&
        ['paid', 'overdue', 'evictable'].includes(group.status))
    );
  }
  readonly loading = signal(true);
  readonly typeLabel = propertyTypeLabel;
  readonly totalBookings = computed(() =>
    this.groups().reduce((total, group) => total + group.bookings.length, 0),
  );
  readonly totalBookingAmount = computed(() =>
    this.groups().reduce(
      (total, group) =>
        total +
        group.bookings.reduce((groupTotal, booking) => groupTotal + booking.bookingAmount, 0),
      0,
    ),
  );

  setAnnouncementPosted(group: PropertyBookingGroup, isPosted: boolean) {
    if (this.updatingAnnouncement()) return;
    this.updatingAnnouncement.set(group.propertyId);
    this.properties.setBookingAnnouncement(group.propertyId, isPosted).subscribe({
      next: (state) => {
        this.groups.update((groups) =>
          groups.map((value) =>
            value.propertyId === state.propertyId
              ? {
                  ...value,
                  bookingAnnouncementPending: state.isPending,
                  bookingAnnouncementCreatedAt: state.createdAt,
                  bookingAnnouncementPostedAt: state.postedAt,
                  bookingAnnouncementPostedByDisplayName: state.postedByDisplayName,
                }
              : value,
          ),
        );
        this.updatingAnnouncement.set(null);
        this.snackBar.open(
          isPosted ? 'Discord property post marked complete.' : 'Discord property post reopened.',
          'Dismiss',
          { duration: 2800 },
        );
      },
      error: () => {
        this.updatingAnnouncement.set(null);
        this.snackBar.open('The announcement status could not be updated.', 'Dismiss', {
          duration: 3500,
          panelClass: ['error-toast'],
        });
      },
    });
  }

  closeAll(group: PropertyBookingGroup) {
    if (this.closingGroup()) return;
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Close all bookings?',
          message: `All ${group.bookings.length} active bookings for ${group.propertyName} will be closed.`,
          confirmLabel: 'Close all bookings',
          dangerous: true,
        },
      })
      .afterClosed()
      .pipe(
        filter((result) => result?.confirmed),
        switchMap(() => {
          this.closingGroup.set(group.propertyId);
          return this.properties.closeAllBookings(group.propertyId);
        }),
      )
      .subscribe({
        next: () => {
          this.groups.update((groups) =>
            groups.filter((value) => value.propertyId !== group.propertyId),
          );
          this.closingGroup.set(null);
          this.properties.refreshBookingAnnouncementCount().subscribe({ error: () => undefined });
          this.snackBar.open('All bookings closed.', 'Dismiss', { duration: 2800 });
        },
        error: () => {
          this.closingGroup.set(null);
          this.snackBar.open('The bookings could not be closed.', 'Dismiss', {
            duration: 3500,
            panelClass: ['error-toast'],
          });
        },
      });
  }

  constructor() {
    this.properties.bookingGroups().subscribe({
      next: (groups) => {
        this.groups.set(groups);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
