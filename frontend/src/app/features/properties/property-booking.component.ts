import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LucideArrowLeft, LucideCalendarPlus, LucideCheck, LucideCopy } from '@lucide/angular';
import { PHONE_NUMBER_PATTERN, PHONE_NUMBER_PLACEHOLDER } from '../../core/constants/app.constants';
import { propertyTypeLabel } from '../../core/constants/property-status.constants';
import { Property } from '../../core/models/property.models';
import { AuthService } from '../../core/services/auth.service';
import { PropertyService } from '../../core/services/property.service';

@Component({
  selector: 'app-property-booking',
  imports: [
    CurrencyPipe,
    ReactiveFormsModule,
    RouterLink,
    LucideArrowLeft,
    LucideCalendarPlus,
    LucideCheck,
    LucideCopy,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page-title">
      <div>
        <p class="eyebrow">Portfolio · Booking</p>
        <h1>Add property booking</h1>
        <p>Record a prospective buyer without replacing any existing bookings.</p>
      </div>
      <a class="btn btn-secondary" routerLink="/dashboard/properties"
        ><svg lucideArrowLeft></svg>Back to properties</a
      >
    </div>

    @if (property(); as selected) {
      <div class="booking-layout">
        <aside class="panel property-summary">
          <p class="eyebrow">Selected property</p>
          <h2>{{ selected.propertyName }}</h2>
          <span class="status">{{
            selected.status === 'booked'
              ? 'Already booked'
              : selected.currentTenantId
                ? 'Occupied · bookings enabled'
                : 'Available'
          }}</span>
          <dl>
            <div>
              <dt>Property ID</dt>
              <dd>#{{ selected.propertyId }}</dd>
            </div>
            <div>
              <dt>Block</dt>
              <dd>{{ selected.blockName }}</dd>
            </div>
            <div>
              <dt>Monthly rent</dt>
              <dd>{{ selected.rent | currency: 'USD' : 'symbol' : '1.0-0' }}</dd>
            </div>
            <div>
              <dt>Current bookings</dt>
              <dd>{{ selected.bookingCount }}</dd>
            </div>
          </dl>
        </aside>

        <form class="panel" [formGroup]="form" (ngSubmit)="submit()">
          <header>
            <div>
              <p class="eyebrow">Applicant details</p>
              <h2>Who is booking this property?</h2>
            </div>
            <svg lucideCalendarPlus></svg>
          </header>
          <div class="field-grid">
            <label class="field">
              <span>CID</span><input type="number" min="1" formControlName="cid" />
              @if (invalid('cid')) {
                <small class="error">Enter a valid CID.</small>
              }
            </label>
            <label class="field">
              <span>Full name</span><input formControlName="fullName" />
              @if (invalid('fullName')) {
                <small class="error">Enter the booking name.</small>
              }
            </label>
            <label class="field">
              <span>Phone number</span>
              <input formControlName="phoneNumber" maxlength="8" [placeholder]="phonePlaceholder" />
              @if (invalid('phoneNumber')) {
                <small class="error">Use the format 123-4567.</small>
              }
            </label>
            <label class="field">
              <span>Discord ID</span><input formControlName="discordId" inputmode="numeric" />
              @if (invalid('discordId')) {
                <small class="error">Enter a numeric Discord ID.</small>
              }
            </label>
            <label class="field">
              <span>Monthly rent</span><input type="number" min="0" formControlName="monthlyRent" />
              @if (invalid('monthlyRent')) {
                <small class="error">Enter the monthly rent.</small>
              }
            </label>
            <label class="field">
              <span>Booking amount</span>
              <input type="number" min="0" formControlName="bookingAmount" />
              @if (invalid('bookingAmount')) {
                <small class="error">Enter a booking amount of 0 or more.</small>
              }
            </label>
          </div>
          <label class="field notes"
            ><span>Notes <small>Optional</small></span
            ><textarea
              rows="5"
              formControlName="notes"
              placeholder="Add booking context or follow-up details"
            ></textarea>
          </label>
          <footer>
            <button
              class="btn btn-secondary"
              type="button"
              [disabled]="form.invalid"
              (click)="copyReceipt()"
            >
              @if (receiptCopied()) {
                <svg lucideCheck></svg>Receipt copied
              } @else {
                <svg lucideCopy></svg>Copy receipt
              }
            </button>
            <button class="btn btn-primary" type="submit" [disabled]="saving()">
              <svg lucideCalendarPlus></svg>{{ saving() ? 'Saving…' : 'Add booking' }}
            </button>
          </footer>
        </form>
      </div>
    } @else if (loadFailed()) {
      <div class="panel error-state">This property could not be loaded.</div>
    }`,
  styles: [
    `
      .page-title {
        display: flex;
        align-items: end;
        justify-content: space-between;
        gap: 20px;
        margin-bottom: 24px;
      }
      .page-title h1 {
        margin: 4px 0;
        font-size: 2.5rem;
      }
      .page-title p:last-child {
        margin: 0;
        color: var(--muted);
      }
      .booking-layout {
        display: grid;
        grid-template-columns: minmax(260px, 0.72fr) minmax(0, 1.6fr);
        gap: 18px;
        align-items: start;
      }
      .property-summary,
      form {
        padding: 24px;
      }
      .property-summary h2 {
        margin: 6px 0 12px;
      }
      .status {
        display: inline-flex;
        padding: 5px 9px;
        border-radius: 999px;
        background: var(--warning-soft);
        color: var(--warning-ink);
        font-size: 0.7rem;
        font-weight: 750;
      }
      dl {
        margin: 22px 0 0;
      }
      dl div {
        display: flex;
        justify-content: space-between;
        gap: 16px;
        padding: 12px 0;
        border-top: 1px solid var(--border);
      }
      dt {
        color: var(--muted);
        font-size: 0.75rem;
      }
      dd {
        margin: 0;
        font-size: 0.8rem;
        font-weight: 700;
        text-align: right;
      }
      form header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        margin-bottom: 22px;
      }
      form header h2 {
        margin: 4px 0 0;
      }
      form header > svg {
        width: 25px;
        color: var(--forest);
      }
      .field-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 16px;
      }
      .field span {
        display: flex;
        justify-content: space-between;
        gap: 10px;
      }
      .field small {
        color: var(--muted);
        font-weight: 500;
      }
      .notes {
        margin-top: 18px;
      }
      footer {
        display: flex;
        justify-content: flex-end;
        gap: 10px;
        margin-top: 24px;
        padding-top: 20px;
        border-top: 1px solid var(--border);
      }
      .error,
      .error-state {
        color: var(--danger);
        font-size: 0.76rem;
      }
      .error-state {
        padding: 24px;
      }
      @media (max-width: 760px) {
        .page-title {
          align-items: flex-start;
          flex-direction: column;
        }
        .booking-layout,
        .field-grid {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class PropertyBookingComponent {
  private readonly propertyService = inject(PropertyService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);
  readonly property = signal<Property | null>(null);
  readonly loadFailed = signal(false);
  readonly saving = signal(false);
  readonly receiptCopied = signal(false);
  readonly phonePlaceholder = PHONE_NUMBER_PLACEHOLDER;
  readonly form = new FormGroup({
    cid: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    fullName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(160)],
    }),
    phoneNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(PHONE_NUMBER_PATTERN)],
    }),
    discordId: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\d+$/), Validators.maxLength(32)],
    }),
    monthlyRent: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
    bookingAmount: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
  });

  constructor() {
    this.propertyService.details(this.route.snapshot.paramMap.get('id')!).subscribe({
      next: (property) => {
        if (!this.canBook(property))
          this.loadFailed.set(true);
        else {
          this.property.set(property);
          this.form.patchValue({
            monthlyRent: property.rent,
            bookingAmount: property.securityDeposit ?? null,
          });
        }
      },
      error: () => this.loadFailed.set(true),
    });
  }

  async copyReceipt() {
    const property = this.property();
    if (!property || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const receipt = [
      `Name: ${value.fullName.trim()}`,
      `Cid: ${value.cid}`,
      `Number: ${value.phoneNumber.trim()}`,
      `House Location: ${property.propertyName}`,
      `House type: ${propertyTypeLabel(property.type)}`,
      `Rent price: ${this.currency(value.monthlyRent!)}`,
      `Booking amount: ${this.currency(value.bookingAmount!)}`,
      `Booked by: ${this.auth.user()?.displayName || this.auth.user()?.username || 'Unknown'}`,
    ].join('\n');

    await navigator.clipboard.writeText(receipt);
    this.receiptCopied.set(true);
    window.setTimeout(() => this.receiptCopied.set(false), 1800);
  }

  invalid(
    name: 'cid' | 'fullName' | 'phoneNumber' | 'discordId' | 'monthlyRent' | 'bookingAmount',
  ): boolean {
    const control = this.form.controls[name];
    return control.invalid && control.touched;
  }

  submit() {
    if (this.form.invalid || !this.property()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const value = this.form.getRawValue();
    this.propertyService
      .createBooking(this.property()!.id, {
        ...value,
        cid: value.cid!,
        monthlyRent: value.monthlyRent!,
        bookingAmount: value.bookingAmount!,
        notes: value.notes || undefined,
      })
      .subscribe({
        next: () => {
          if (this.auth.isManager())
            this.propertyService
              .refreshBookingAnnouncementCount()
              .subscribe({ error: () => undefined });
          this.snackBar.open('Booking added successfully.', 'Dismiss', { duration: 3000 });
          void this.router.navigate(['/dashboard/properties']);
        },
        error: (response) => {
          this.saving.set(false);
          this.snackBar.open(
            response?.error?.detail || 'The booking could not be added.',
            'Dismiss',
            { duration: 4000, panelClass: ['error-toast'] },
          );
        },
      });
  }

  private currency(value: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
    }).format(value);
  }

  private canBook(property: Property): boolean {
    return (
      property.status === 'available' ||
      property.status === 'booked' ||
      (property.allowOccupiedBookings &&
        Boolean(property.currentTenantId) &&
        ['paid', 'overdue', 'evictable'].includes(property.status))
    );
  }
}
