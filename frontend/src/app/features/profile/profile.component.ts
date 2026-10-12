import { DatePipe, TitleCasePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LucideCheck, LucidePencil, LucideSave, LucideX } from '@lucide/angular';
import { PHONE_NUMBER_PATTERN, PHONE_NUMBER_PLACEHOLDER } from '../../core/constants/app.constants';
import { USER_ROLES } from '../../core/constants/user-role.constants';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-profile',
  imports: [
    DatePipe,
    TitleCasePipe,
    ReactiveFormsModule,
    LucideCheck,
    LucidePencil,
    LucideSave,
    LucideX,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page-title">
      <div>
        <p class="eyebrow">Personal account</p>
        <h1>Profile</h1>
        <p>Manage the staff details connected to your Imperial Estates account.</p>
      </div>
    </div>
    @if (auth.user(); as user) {
      <div class="profile-grid">
        <aside class="identity-card panel">
          <img [src]="user.avatarUrl || fallback" alt="" />
          <h2>{{ user.displayName }}</h2>
          <p class="username">@{{ user.username }}</p>
          <span class="role-badge">{{ roleLabel(user.role) }}</span>
          <div class="discord-identity">
            <small>Discord ID</small>
            <b>{{ user.discordUserId }}</b>
          </div>
          <p class="identity-note">
            Your avatar, Discord name, username, and account ID refresh automatically when you sign
            in.
          </p>
        </aside>

        <div class="profile-content">
          <section class="details-card panel">
            <header>
              <div>
                <p class="eyebrow">Staff details</p>
                <h2>Personal information</h2>
                <p>These details are visible to authorized managers in User Management.</p>
              </div>
              <div class="header-actions">
                @if (user.fullName && user.cid && user.phoneNumber) {
                  <span class="complete"><svg lucideCheck></svg>Complete</span>
                } @else {
                  <span class="incomplete">Profile incomplete</span>
                }
                @if (!editing() && !auth.isReadOnly()) {
                  <button class="edit-button" type="button" (click)="edit()">
                    <svg lucidePencil></svg>
                    Edit
                  </button>
                }
              </div>
            </header>

            @if (editing()) {
              <form [formGroup]="form" (ngSubmit)="save()">
                <label class="field full-name">
                  <span>Full name</span>
                  <input
                    formControlName="fullName"
                    autocomplete="name"
                    placeholder="Your full name"
                  />
                  @if (invalid('fullName')) {
                    <small class="error">Enter your full name.</small>
                  }
                </label>
                <div class="field-grid">
                  <label class="field">
                    <span>CID</span>
                    <input type="number" min="1" step="1" formControlName="cid" />
                    @if (invalid('cid')) {
                      <small class="error">Enter a valid integer CID.</small>
                    }
                  </label>
                  <label class="field">
                    <span>Number</span>
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
                </div>
                <div class="form-actions">
                  @if (failed()) {
                    <span class="failed">Profile could not be saved.</span>
                  }
                  <button class="btn btn-secondary" type="button" (click)="cancel()">
                    <svg lucideX></svg>Cancel
                  </button>
                  <button
                    class="btn btn-primary"
                    [disabled]="form.invalid || form.pristine || saving() || !auth.isApproved()"
                  >
                    <svg lucideSave></svg>{{ saving() ? 'Saving…' : 'Save changes' }}
                  </button>
                </div>
              </form>
            } @else {
              <dl class="personal-details">
                <div>
                  <dt>Full name</dt>
                  <dd [class.not-set]="!user.fullName">{{ user.fullName || 'Not set' }}</dd>
                </div>
                <div>
                  <dt>CID</dt>
                  <dd [class.not-set]="!user.cid">{{ user.cid || 'Not set' }}</dd>
                </div>
                <div>
                  <dt>Number</dt>
                  <dd [class.not-set]="!user.phoneNumber">{{ user.phoneNumber || 'Not set' }}</dd>
                </div>
              </dl>
              @if (saved()) {
                <p class="saved display-saved"><svg lucideCheck></svg>Profile saved</p>
              }
            }
          </section>

          <section class="status-card panel">
            <div>
              <p class="eyebrow">Account</p>
              <h2>Access status</h2>
            </div>
            <dl>
              <div>
                <dt>Approval</dt>
                <dd>{{ user.approvalStatus | titlecase }}</dd>
              </div>
              <div>
                <dt>Access</dt>
                <dd>{{ user.accessStatus | titlecase }}</dd>
              </div>
              <div>
                <dt>Role</dt>
                <dd>{{ roleLabel(user.role) }}</dd>
              </div>
              <div>
                <dt>Last sign in</dt>
                <dd>
                  {{ user.lastLoginAt ? (user.lastLoginAt | date: 'medium') : 'Current session' }}
                </dd>
              </div>
            </dl>
          </section>
        </div>
      </div>
    }`,
  styles: [
    `
      .page-title {
        margin-bottom: 26px;
      }
      .page-title h1 {
        margin: 4px 0 7px;
        font-size: 2.5rem;
      }
      .page-title p:last-child {
        margin: 0;
        color: var(--muted);
      }
      .profile-grid {
        display: grid;
        grid-template-columns: minmax(260px, 320px) minmax(0, 1fr);
        align-items: start;
        gap: 18px;
        max-width: 1120px;
      }
      .identity-card {
        padding: 30px 26px;
        text-align: center;
      }
      .identity-card > img {
        width: 112px;
        height: 112px;
        margin: 0 auto 18px;
        border: 3px solid var(--surface);
        border-radius: 50%;
        object-fit: cover;
        box-shadow:
          0 0 0 1px var(--border),
          var(--shadow-sm);
      }
      .identity-card h2 {
        margin: 0 0 4px;
        font-size: 1.55rem;
      }
      .username {
        margin: 0;
        color: var(--muted);
      }
      .role-badge,
      .complete,
      .incomplete {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        border-radius: 999px;
        font-size: 0.72rem;
        font-weight: 750;
      }
      .role-badge {
        margin-top: 13px;
        padding: 6px 10px;
        background: var(--forest-light);
        color: var(--forest);
      }
      .discord-identity {
        display: grid;
        gap: 4px;
        margin-top: 26px;
        padding: 18px 0;
        border-block: 1px solid var(--border);
        text-align: left;
      }
      .discord-identity small,
      dt {
        color: var(--muted);
        font-size: 0.72rem;
      }
      .discord-identity b {
        overflow-wrap: anywhere;
        font-size: 0.82rem;
      }
      .identity-note {
        margin: 18px 0 0;
        color: var(--muted);
        font-size: 0.78rem;
        line-height: 1.55;
        text-align: left;
      }
      .profile-content {
        display: grid;
        gap: 18px;
      }
      .details-card,
      .status-card {
        padding: 26px;
      }
      .details-card > header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 20px;
        padding-bottom: 22px;
        border-bottom: 1px solid var(--border);
      }
      .details-card h2,
      .status-card h2 {
        margin: 5px 0 7px;
        font-size: 1.35rem;
      }
      .details-card header p:last-child {
        max-width: 560px;
        margin: 0;
        color: var(--muted);
        line-height: 1.5;
      }
      .complete,
      .incomplete {
        flex: 0 0 auto;
        padding: 6px 9px;
      }
      .complete {
        background: var(--available-bg);
        color: var(--available-ink);
      }
      .complete svg,
      .saved svg {
        width: 14px;
        height: 14px;
      }
      .incomplete {
        background: var(--warning-soft);
        color: var(--warning-ink);
      }
      .header-actions {
        display: flex;
        align-items: center;
        gap: 10px;
      }
      .edit-button {
        display: inline-flex;
        align-items: center;
        gap: 7px;
        min-height: 34px;
        padding: 6px 10px;
        border: 1px solid var(--border);
        border-radius: var(--radius-sm);
        background: var(--surface-subtle);
        color: var(--text);
        font: inherit;
        font-size: 0.78rem;
        font-weight: 700;
        cursor: pointer;
      }
      .edit-button:hover {
        border-color: var(--accent);
        color: var(--accent);
      }
      .edit-button svg {
        width: 14px;
        height: 14px;
      }
      .personal-details {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 14px;
        margin: 0;
        padding-top: 22px;
      }
      .personal-details div {
        min-height: 76px;
        padding: 15px 16px;
        border: 1px solid var(--border);
        border-radius: var(--radius-sm);
        background: var(--surface-subtle);
      }
      .personal-details div:first-child {
        grid-column: 1 / -1;
      }
      .personal-details dd {
        font-size: 0.9rem;
      }
      .not-set {
        color: var(--muted);
        font-weight: 600;
      }
      .display-saved {
        justify-content: flex-end;
        margin: 14px 0 0;
      }
      form {
        display: grid;
        gap: 16px;
        padding-top: 22px;
      }
      .field-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 14px;
      }
      .form-actions {
        min-height: 42px;
        display: flex;
        align-items: center;
        justify-content: flex-end;
        gap: 12px;
        padding-top: 5px;
      }
      .saved {
        display: inline-flex;
        align-items: center;
        gap: 5px;
        color: var(--available-ink);
        font-size: 0.78rem;
        font-weight: 700;
      }
      .failed {
        color: var(--danger);
        font-size: 0.78rem;
        font-weight: 700;
      }
      .status-card > div:first-child {
        margin-bottom: 18px;
      }
      .status-card dl {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 16px;
        margin: 0;
      }
      .status-card dl div {
        padding: 14px;
        border: 1px solid var(--border);
        border-radius: var(--radius-sm);
        background: var(--surface-subtle);
      }
      dd {
        margin: 5px 0 0;
        font-weight: 700;
      }
      @media (max-width: 850px) {
        .profile-grid {
          grid-template-columns: 1fr;
        }
        .identity-card {
          display: grid;
          grid-template-columns: auto 1fr;
          column-gap: 20px;
          text-align: left;
        }
        .identity-card > img {
          grid-row: span 3;
          margin: 0;
        }
        .discord-identity,
        .identity-note {
          grid-column: 1 / -1;
        }
        .role-badge {
          justify-self: start;
        }
      }
      @media (max-width: 560px) {
        .details-card > header {
          flex-direction: column;
        }
        .header-actions {
          width: 100%;
          justify-content: space-between;
        }
        .field-grid,
        .personal-details,
        .status-card dl {
          grid-template-columns: 1fr;
        }
        .personal-details div:first-child {
          grid-column: auto;
        }
        .form-actions {
          align-items: stretch;
          flex-direction: column;
        }
      }
    `,
  ],
})
export class ProfileComponent {
  readonly auth = inject(AuthService);
  readonly fallback = 'https://api.dicebear.com/9.x/initials/svg?seed=Imperial';
  readonly roleLabel = (role: keyof typeof USER_ROLES) => USER_ROLES[role];
  readonly phonePlaceholder = PHONE_NUMBER_PLACEHOLDER;
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly failed = signal(false);
  readonly editing = signal(false);
  readonly form = new FormGroup({
    fullName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2), Validators.maxLength(160)],
    }),
    cid: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(1),
      Validators.pattern(/^\d+$/),
    ]),
    phoneNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(PHONE_NUMBER_PATTERN)],
    }),
  });

  constructor() {
    effect(() => {
      const user = this.auth.user();
      if (!user || this.form.dirty) return;
      this.form.reset({
        fullName: user.fullName ?? '',
        cid: user.cid ?? null,
        phoneNumber: user.phoneNumber ?? '',
      });
    });
  }

  invalid(name: 'fullName' | 'cid' | 'phoneNumber'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.dirty || control.touched);
  }

  edit() {
    if (this.auth.isReadOnly()) return;
    const user = this.auth.user();
    if (!user) return;

    this.form.reset({
      fullName: user.fullName ?? '',
      cid: user.cid ?? null,
      phoneNumber: user.phoneNumber ?? '',
    });
    this.saved.set(false);
    this.failed.set(false);
    this.editing.set(true);
  }

  cancel() {
    const user = this.auth.user();
    if (user) {
      this.form.reset({
        fullName: user.fullName ?? '',
        cid: user.cid ?? null,
        phoneNumber: user.phoneNumber ?? '',
      });
    }
    this.failed.set(false);
    this.editing.set(false);
  }

  save() {
    if (this.auth.isReadOnly()) return;
    if (this.form.invalid || !this.auth.isApproved()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.saved.set(false);
    this.failed.set(false);
    this.auth
      .updateProfile({
        fullName: value.fullName.trim(),
        cid: value.cid!,
        phoneNumber: value.phoneNumber.trim(),
      })
      .subscribe({
        next: (user) => {
          this.form.reset({
            fullName: user.fullName ?? '',
            cid: user.cid ?? null,
            phoneNumber: user.phoneNumber ?? '',
          });
          this.saving.set(false);
          this.saved.set(true);
          this.editing.set(false);
          window.setTimeout(() => this.saved.set(false), 2200);
        },
        error: () => {
          this.saving.set(false);
          this.failed.set(true);
        },
      });
  }
}
