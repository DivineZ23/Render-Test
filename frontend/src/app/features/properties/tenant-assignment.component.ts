import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LucideArrowLeft, LucideCheck, LucideCopy, LucideUserPlus } from '@lucide/angular';
import { PHONE_NUMBER_PATTERN, PHONE_NUMBER_PLACEHOLDER } from '../../core/constants/app.constants';
import {
  propertyTypeCapacity,
  propertyTypeLabel,
  propertyTypeStorageCapacity,
} from '../../core/constants/property-status.constants';
import { AssignTenantRequest, Property } from '../../core/models/property.models';
import { AuthService } from '../../core/services/auth.service';
import { PropertyService } from '../../core/services/property.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-tenant-assignment',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    StatusBadgeComponent,
    LucideArrowLeft,
    LucideCheck,
    LucideCopy,
    LucideUserPlus,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="back">
      <a routerLink="/dashboard/properties"><svg lucideArrowLeft></svg>Back to properties</a>
    </div>
    <div class="page-title">
      <div>
        <p class="eyebrow">Tenant assignment</p>
        <h1>Assign tenant</h1>
        <p>Add a tenant without changing the property configuration.</p>
      </div>
    </div>

    @if (property(); as p) {
      <div class="assignment-grid">
        <section class="panel property-context">
          <header>
            <div>
              <p class="eyebrow">Selected property</p>
              <h2>{{ p.propertyName }}</h2>
            </div>
            <app-status-badge [status]="p.status" />
          </header>
          <dl>
            <div>
              <dt>Property ID</dt>
              <dd>#{{ p.propertyId }}</dd>
            </div>
            <div>
              <dt>Block</dt>
              <dd>{{ p.blockName }}</dd>
            </div>
            <div>
              <dt>Interior structure</dt>
              <dd>{{ typeLabel(p.type) }}</dd>
            </div>
            <div>
              <dt>Person capacity</dt>
              <dd>{{ p.personCapacity ?? typeCapacity(p.type) ?? '—' }}</dd>
            </div>
            <div>
              <dt>Storage capacity</dt>
              <dd>{{ storageCapacityLabel(p) }}</dd>
            </div>
          </dl>
          <p class="locked">Property details are read-only during tenant assignment.</p>
        </section>

        @if (p.status === 'available' || p.status === 'booked') {
          <form class="panel tenant-form" [formGroup]="form" (ngSubmit)="assign()">
            <div class="form-heading">
              <p class="eyebrow">Tenant information</p>
              <h2>Occupant details</h2>
              <p>CID, Discord ID, full name, phone number, rent, and deposit are required.</p>
            </div>

            <label class="field">
              <span>CID</span>
              <input type="number" min="1" step="1" formControlName="cid" />
              @if (invalid('cid')) {
                <small class="error">Enter a valid integer CID.</small>
              }
            </label>
            <label class="field">
              <span>Discord ID</span>
              <input inputmode="numeric" autocomplete="off" formControlName="discordId" />
              @if (invalid('discordId')) {
                <small class="error">Enter a valid numeric Discord ID.</small>
              }
            </label>
            <label class="field">
              <span>Tenant full name</span>
              <input formControlName="fullName" autocomplete="name" />
              @if (invalid('fullName')) {
                <small class="error">Tenant full name is required.</small>
              }
            </label>
            <label class="field">
              <span>Phone number</span>
              <input
                formControlName="phoneNumber"
                autocomplete="tel"
                inputmode="tel"
                maxlength="8"
                [placeholder]="phonePlaceholder"
              />
              @if (invalid('phoneNumber')) {
                <small class="error">Use the format 123-4567.</small>
              }
            </label>
            <div class="two-columns">
              <label class="field">
                <span>Start date</span>
                <input type="date" formControlName="startDate" />
              </label>
              <label class="field">
                <span>Monthly rent</span>
                <input type="number" min="0" formControlName="monthlyRent" />
                @if (invalid('monthlyRent')) {
                  <small class="error">Monthly rent is required.</small>
                }
              </label>
            </div>
            <label class="field">
              <span>Security deposit <i>optional</i></span>
              <input
                type="number"
                min="0"
                formControlName="securityDeposit"
              />
              @if (invalid('securityDeposit')) {
                <small class="error">Security deposit cannot be negative.</small>
              }
            </label>
            <label class="field">
              <span>Notes <i>optional</i></span>
              <textarea rows="3" formControlName="notes"></textarea>
            </label>
            <div class="actions">
              <a class="btn btn-secondary" routerLink="/dashboard/properties">Cancel</a>
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
              <button class="btn btn-primary" [disabled]="form.invalid || saving()">
                <svg lucideUserPlus></svg>{{ saving() ? 'Assigning…' : 'Assign tenant' }}
              </button>
            </div>
          </form>
        } @else {
          <section class="panel unavailable">
            <h2>Tenant already assigned</h2>
            <p>This property must be Available or Booked before a tenant can be assigned.</p>
            <a class="btn btn-secondary" routerLink="/dashboard/properties">Return to properties</a>
          </section>
        }
      </div>
    } @else {
      <section class="panel loading">Loading property…</section>
    }
  `,
  styles: [
    `
      .back {
        margin-bottom: 20px;
        color: var(--muted);
        font-size: 0.8rem;
      }
      .back a {
        display: inline-flex;
        align-items: center;
        gap: 7px;
      }
      .back svg {
        width: 16px;
        height: 16px;
      }
      .page-title {
        margin-bottom: 24px;
      }
      .page-title h1 {
        margin: 4px 0;
        font-size: 2.5rem;
      }
      .page-title p:last-child,
      .form-heading > p:last-child {
        margin-bottom: 0;
        color: var(--muted);
      }
      .form-heading .eyebrow {
        margin-bottom: 0;
      }
      .form-heading h2 {
        margin: 7px 0 8px;
      }
      .form-heading > p:last-child {
        line-height: 1.55;
      }
      .assignment-grid {
        display: grid;
        grid-template-columns: minmax(280px, 0.75fr) minmax(420px, 1.25fr);
        gap: 20px;
        align-items: start;
      }
      .property-context,
      .tenant-form,
      .unavailable,
      .loading {
        padding: 26px;
      }
      .property-context header {
        display: flex;
        align-items: start;
        justify-content: space-between;
        gap: 18px;
        padding-bottom: 20px;
        border-bottom: 1px solid var(--border);
      }
      h2 {
        margin: 4px 0 0;
        font-size: 1.3rem;
      }
      dl {
        margin: 10px 0 0;
      }
      dl div {
        display: flex;
        justify-content: space-between;
        gap: 20px;
        padding: 12px 0;
        border-bottom: 1px solid var(--border);
      }
      dt {
        color: var(--muted);
      }
      dd {
        margin: 0;
        text-align: right;
        font-weight: 700;
      }
      .locked {
        margin: 20px 0 0;
        padding: 12px;
        border-radius: var(--radius-sm);
        background: var(--forest-light);
        color: var(--forest);
        font-size: 0.78rem;
        font-weight: 650;
      }
      .tenant-form {
        display: grid;
        gap: 16px;
      }
      .two-columns {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 14px;
      }
      .field i {
        color: var(--muted);
        font-size: 0.72rem;
        font-weight: 500;
      }
      .actions {
        display: flex;
        justify-content: flex-end;
        gap: 10px;
        padding-top: 6px;
      }
      .unavailable p {
        color: var(--muted);
      }
      .loading {
        color: var(--muted);
      }
      @media (max-width: 900px) {
        .assignment-grid {
          grid-template-columns: 1fr;
        }
      }
      @media (max-width: 560px) {
        .two-columns {
          grid-template-columns: 1fr;
        }
        .actions {
          align-items: stretch;
          flex-direction: column-reverse;
        }
      }
    `,
  ],
})
export class TenantAssignmentComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly properties = inject(PropertyService);
  private readonly auth = inject(AuthService);

  readonly property = signal<Property | null>(null);
  readonly saving = signal(false);
  readonly receiptCopied = signal(false);
  readonly typeLabel = propertyTypeLabel;
  readonly typeCapacity = propertyTypeCapacity;
  readonly phonePlaceholder = PHONE_NUMBER_PLACEHOLDER;
  readonly form = new FormGroup(
    {
      cid: new FormControl<number | null>(null, [
        Validators.required,
        Validators.min(1),
        Validators.pattern(/^\d+$/),
      ]),
      discordId: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.pattern(/^\d+$/), Validators.maxLength(32)],
      }),
      fullName: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(160)],
      }),
      phoneNumber: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.pattern(PHONE_NUMBER_PATTERN)],
      }),
      startDate: new FormControl(new Date().toISOString().slice(0, 10), {
        nonNullable: true,
        validators: Validators.required,
      }),
      monthlyRent: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
      securityDeposit: new FormControl<number | null>(null, Validators.min(0)),
      notes: new FormControl('', { nonNullable: true }),
    },
  );

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/dashboard/properties']);
      return;
    }

    this.properties.details(id).subscribe((property) => {
      this.property.set(property);
      this.form.patchValue({
        monthlyRent: property.rent,
        securityDeposit: property.securityDeposit ?? null,
      });
    });
  }

  invalid(
    name: 'cid' | 'discordId' | 'fullName' | 'phoneNumber' | 'monthlyRent' | 'securityDeposit',
  ) {
    const control = this.form.controls[name];
    return control.invalid && (control.dirty || control.touched);
  }

  storageCapacityLabel(property: Property): string {
    const capacity = property.storageCapacity ?? propertyTypeStorageCapacity(property.type);
    return capacity?.toLocaleString('en-US') ?? '—';
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
      `CID: ${value.cid}`,
      `Number: ${value.phoneNumber.trim()}`,
      `House Location: ${property.propertyName}`,
      `House type: ${this.typeLabel(property.type)}`,
      `Rent price: ${this.currency(value.monthlyRent!)}`,
      `Deposit amount: ${this.currency(value.securityDeposit ?? 0)}`,
      `Sold by: ${this.receiptActorName()}`,
    ].join('\n');

    await navigator.clipboard.writeText(receipt);
    this.receiptCopied.set(true);
    window.setTimeout(() => this.receiptCopied.set(false), 1800);
  }

  assign() {
    const property = this.property();
    if (!property || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();
    const request: AssignTenantRequest = {
      cid: raw.cid!,
      discordId: raw.discordId,
      fullName: raw.fullName,
      phoneNumber: raw.phoneNumber,
      startDate: raw.startDate,
      monthlyRent: raw.monthlyRent!,
      securityDeposit: raw.securityDeposit,
      notes: raw.notes || undefined,
    };
    this.properties.assignTenant(property.id, request).subscribe({
      next: () => this.router.navigate(['/dashboard/properties']),
      error: () => this.saving.set(false),
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

  private receiptActorName(): string {
    const user = this.auth.user();
    return user?.fullName?.trim() || user?.displayName || user?.username || 'Unknown';
  }
}
