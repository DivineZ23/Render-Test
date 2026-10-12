import { DatePipe, TitleCasePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute } from '@angular/router';
import {
  LucideCheck,
  LucideCopy,
  LucideRotateCcw,
  LucideUserRoundCheck,
  LucideUserRoundX,
} from '@lucide/angular';
import { RecruitmentApplication, RecruitmentStatus } from '../../core/models/management.models';
import { RecruitmentService } from '../../core/services/management.services';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-recruitment-applications',
  imports: [
    DatePipe,
    TitleCasePipe,
    EmptyStateComponent,
    LucideCheck,
    LucideCopy,
    LucideRotateCcw,
    LucideUserRoundCheck,
    LucideUserRoundX,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page-title">
      <div>
        <p class="eyebrow">Recruitment</p>
        <div class="title-line">
          <h1>{{ pageTitle() }}</h1>
          <span class="count"
            >{{ total() }} {{ total() === 1 ? 'application' : 'applications' }}</span
          >
          <button
            type="button"
            class="recruitment-toggle"
            [class.closed]="!recruitmentEnabled()"
            [disabled]="!auth.isManager() || savingSettings()"
            [attr.aria-pressed]="recruitmentEnabled()"
            (click)="toggleRecruitment()"
          >
            <span class="switch" aria-hidden="true"><i></i></span>
            <span>{{ recruitmentEnabled() ? 'Recruitment open' : 'Recruitment closed' }}</span>
          </button>
        </div>
        <p>{{ pageDescription() }}</p>
      </div>
    </div>

    <div class="application-list">
      @for (application of applications(); track application.id) {
        <article class="application-card panel">
          <header>
            <div class="applicant">
              <span class="initials">{{ initials(application.characterName) }}</span>
              <div>
                <h2>{{ application.characterName }}</h2>
                <p>
                  CID {{ application.characterCid }} · Submitted
                  {{ application.createdAt | date: 'mediumDate' }}
                </p>
              </div>
            </div>
            <span class="status" [class]="'status ' + application.status">{{
              application.status | titlecase
            }}</span>
          </header>

          <div class="contact-strip">
            <div>
              <small>Character number</small><b>{{ application.characterPhoneNumber }}</b>
            </div>
            <div>
              <small>Discord ID</small>
              <b>{{ application.discordId }}</b>
              <button type="button" (click)="copy(application.discordId)" title="Copy Discord ID">
                <svg lucideCopy></svg>
              </button>
            </div>
            <div>
              <small>Total playtime</small><b>{{ application.totalPlaytime }}</b>
            </div>
          </div>

          <div class="answers">
            <section>
              <small>Why do you want to join Imperial Estates?</small>
              <p>{{ application.reasonToJoin }}</p>
            </section>
            <section>
              <small>Beneficial skills</small>
              <p>{{ application.beneficialSkills }}</p>
            </section>
            <section class="availability">
              <small>Usual availability</small>
              <p>{{ application.availability }}</p>
            </section>
          </div>

          @if (application.reviewedAt) {
            <div class="review-summary">
              <svg lucideCheck></svg>
              <span>
                Reviewed by <b>{{ application.reviewedByDisplayName || 'Management' }}</b> on
                {{ application.reviewedAt | date: 'medium' }}
                @if (application.reviewNotes) {
                  · {{ application.reviewNotes }}
                }
              </span>
            </div>
          }

          @if (auth.isManager()) { <footer>
            @if (application.status === 'pending') {
              <button class="btn accept" (click)="review(application, 'accepted')">
                <svg lucideUserRoundCheck></svg>Accept application
              </button>
              <button class="btn reject" (click)="review(application, 'rejected')">
                <svg lucideUserRoundX></svg>Reject
              </button>
            } @else {
              <button class="btn reopen" (click)="review(application, 'pending')">
                <svg lucideRotateCcw></svg>Return to pending
              </button>
            }
          </footer> }
        </article>
      } @empty {
        <app-empty-state
          [title]="'No ' + status() + ' applications'"
          message="Recruitment applications in this stage will appear here."
        />
      }
    </div>`,
  styles: [
    `
      .page-title {
        margin-bottom: 26px;
      }
      .title-line {
        display: flex;
        align-items: center;
        gap: 12px;
        flex-wrap: wrap;
      }
      .page-title h1 {
        margin: 4px 0;
        font-size: 2.5rem;
      }
      .page-title p:last-child {
        margin: 0;
        color: var(--muted);
      }
      .count {
        padding: 7px 11px;
        border: 1px solid var(--border);
        border-radius: 999px;
        background: var(--surface-subtle);
        color: var(--muted);
        font-size: 0.72rem;
        font-weight: 700;
      }
      .recruitment-toggle {
        display: inline-flex;
        align-items: center;
        min-height: 34px;
        gap: 9px;
        padding: 5px 10px 5px 7px;
        border: 1px solid var(--border);
        border-radius: 999px;
        background: var(--available-bg);
        color: var(--available-ink);
        font: inherit;
        font-size: 0.72rem;
        font-weight: 750;
        cursor: pointer;
      }
      .recruitment-toggle.closed {
        background: var(--surface-subtle);
        color: var(--muted);
      }
      .recruitment-toggle:disabled {
        cursor: wait;
        opacity: 0.65;
      }
      .switch {
        position: relative;
        width: 31px;
        height: 18px;
        border-radius: 999px;
        background: var(--available-ink);
      }
      .switch i {
        position: absolute;
        top: 3px;
        left: 16px;
        width: 12px;
        height: 12px;
        border-radius: 50%;
        background: var(--surface);
        transition: left 150ms ease;
      }
      .closed .switch {
        background: var(--muted);
      }
      .closed .switch i {
        left: 3px;
      }
      .application-list {
        display: grid;
        gap: 14px;
        max-width: 1320px;
      }
      .application-card {
        padding: 0;
        overflow: hidden;
      }
      .application-card > header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 20px;
        padding: 18px 20px;
        border-bottom: 1px solid var(--border);
      }
      .applicant {
        display: flex;
        align-items: center;
        min-width: 0;
        gap: 12px;
      }
      .initials {
        display: grid;
        flex: 0 0 44px;
        width: 44px;
        height: 44px;
        place-items: center;
        border-radius: 50%;
        background: var(--forest-light);
        color: var(--forest);
        font-weight: 800;
      }
      .applicant h2,
      .applicant p {
        margin: 0;
      }
      .applicant h2 {
        font-size: 1.08rem;
      }
      .applicant p {
        margin-top: 3px;
        color: var(--muted);
        font-size: 0.72rem;
      }
      .status {
        flex: 0 0 auto;
        padding: 5px 9px;
        border-radius: 999px;
        font-size: 0.65rem;
        font-weight: 800;
      }
      .status.pending {
        background: var(--warning-soft);
        color: var(--warning-ink);
      }
      .status.accepted {
        background: var(--available-bg);
        color: var(--available-ink);
      }
      .status.rejected {
        background: var(--danger-soft);
        color: var(--danger);
      }
      .contact-strip {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        border-bottom: 1px solid var(--border);
        background: var(--surface-subtle);
      }
      .contact-strip > div {
        min-width: 0;
        padding: 13px 20px;
        border-right: 1px solid var(--border);
      }
      .contact-strip > div:last-child {
        border-right: 0;
      }
      .contact-strip small,
      .answers small {
        display: block;
        margin-bottom: 4px;
        color: var(--muted);
        font-size: 0.66rem;
      }
      .contact-strip b {
        font-size: 0.8rem;
      }
      .contact-strip button {
        margin-left: 6px;
        padding: 2px;
        border: 0;
        background: none;
        color: var(--muted);
        cursor: pointer;
        vertical-align: middle;
      }
      .contact-strip button svg {
        width: 13px;
      }
      .answers {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 0;
        padding: 8px 20px;
      }
      .answers section {
        min-width: 0;
        padding: 14px 18px 14px 0;
      }
      .answers section:nth-child(even) {
        padding-left: 18px;
        border-left: 1px solid var(--border);
      }
      .answers .availability {
        grid-column: 1 / -1;
        padding-right: 0;
        border-top: 1px solid var(--border);
      }
      .answers p {
        margin: 0;
        color: var(--text);
        font-size: 0.8rem;
        line-height: 1.55;
        white-space: pre-wrap;
      }
      .review-summary {
        display: flex;
        align-items: center;
        gap: 8px;
        margin: 0 20px;
        padding: 10px 0;
        border-top: 1px solid var(--border);
        color: var(--muted);
        font-size: 0.7rem;
      }
      .review-summary svg {
        width: 14px;
        color: var(--forest);
      }
      footer {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
        padding: 13px 20px;
        border-top: 1px solid var(--border);
        background: var(--surface-subtle);
      }
      footer .btn {
        min-height: 34px;
        padding: 7px 10px;
        font-size: 0.72rem;
      }
      footer .btn svg {
        width: 14px;
      }
      .accept {
        background: var(--available-bg);
        color: var(--available-ink);
      }
      .reject {
        background: var(--danger-soft);
        color: var(--danger);
      }
      .reopen {
        background: var(--forest-light);
        color: var(--forest);
      }
      @media (max-width: 700px) {
        .contact-strip,
        .answers {
          grid-template-columns: 1fr;
        }
        .contact-strip > div,
        .contact-strip > div:last-child {
          border-right: 0;
          border-bottom: 1px solid var(--border);
        }
        .contact-strip > div:last-child {
          border-bottom: 0;
        }
        .answers section,
        .answers section:nth-child(even) {
          padding: 14px 0;
          border-left: 0;
        }
        .answers .availability {
          grid-column: auto;
        }
        footer {
          align-items: stretch;
          flex-direction: column;
        }
      }
    `,
  ],
})
export class RecruitmentApplicationsComponent {
  readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly recruitment = inject(RecruitmentService);
  private readonly dialog = inject(MatDialog);
  readonly status = signal<RecruitmentStatus>('pending');
  readonly applications = signal<RecruitmentApplication[]>([]);
  readonly total = signal(0);
  readonly recruitmentEnabled = signal(true);
  readonly savingSettings = signal(false);

  constructor() {
    this.route.data.subscribe((data) => {
      this.status.set(data['status'] as RecruitmentStatus);
      this.load();
    });
    this.recruitment
      .settings()
      .subscribe((settings) => this.recruitmentEnabled.set(settings.isEnabled));
  }

  pageTitle(): string {
    return this.status() === 'pending'
      ? 'Pending applications'
      : this.status() === 'accepted'
        ? 'Accepted applications'
        : 'Rejected applications';
  }

  pageDescription(): string {
    return this.status() === 'pending'
      ? 'Review new applicants and record a clear hiring decision.'
      : this.status() === 'accepted'
        ? 'Applicants approved by Imperial Estates management.'
        : 'Applications declined by management, including the recorded reason.';
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('');
  }

  copy(value: string) {
    void navigator.clipboard.writeText(value);
  }

  toggleRecruitment() {
    const nextValue = !this.recruitmentEnabled();
    this.savingSettings.set(true);
    this.recruitment.updateSettings(nextValue).subscribe({
      next: (settings) => {
        this.recruitmentEnabled.set(settings.isEnabled);
        this.savingSettings.set(false);
      },
      error: () => this.savingSettings.set(false),
    });
  }

  load() {
    this.recruitment.all(this.status()).subscribe((result) => {
      this.applications.set(result.items);
      this.total.set(result.totalItems);
    });
  }

  review(application: RecruitmentApplication, status: RecruitmentStatus) {
    const label = status === 'accepted' ? 'Accept' : status === 'rejected' ? 'Reject' : 'Reopen';
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: `${label} ${application.characterName}'s application?`,
          message:
            status === 'accepted'
              ? 'The application will move to the accepted queue and the decision will be audited.'
              : status === 'rejected'
                ? 'The application will move to the rejected queue. Add a clear reason for the decision.'
                : 'The application will return to the pending review queue.',
          requireReason: status === 'rejected',
          dangerous: status === 'rejected',
          confirmLabel: label,
        },
      })
      .afterClosed()
      .subscribe((result) => {
        if (!result?.confirmed) return;
        this.recruitment.review(application.id, status, result.reason).subscribe(() => this.load());
      });
  }
}
