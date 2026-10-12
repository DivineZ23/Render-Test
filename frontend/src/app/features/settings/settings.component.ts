import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucidePlus, LucideSave, LucideTrash2 } from '@lucide/angular';
import { TeamMember } from '../../core/models/user.models';
import { SettingsService } from '../../core/services/management.services';
import { AuthService } from '../../core/services/auth.service';
import { ThemeToggleComponent } from '../../shared/components/theme-toggle/theme-toggle.component';
@Component({
  selector: 'app-settings',
  imports: [FormsModule, LucidePlus, LucideSave, LucideTrash2, ThemeToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page-title">
      <div>
        <p class="eyebrow">Administration</p>
        <h1>Settings</h1>
        <p>Operational configuration is intentionally narrow and secure.</p>
      </div>
    </div>
    <div class="settings-grid">
      <section class="appearance-editor">
        <app-theme-toggle [inline]="true" />
      </section>
      <section class="panel team-editor">
        <header>
          <div>
            <h2>Public team</h2>
            <p>These cards appear on the About page.</p>
          </div>
          <button class="btn btn-secondary" [disabled]="!auth.isManager()" (click)="add()">
            <svg lucidePlus></svg>Add member
          </button>
        </header>
        <div class="members">
          @for (member of members(); track member.id; let index = $index) {
            <article>
              <div class="member-avatar"><img [src]="member.imageUrl" alt="" /></div>
              <div class="member-fields">
                <label class="field"><span>Name</span><input [readOnly]="!auth.isManager()" [(ngModel)]="member.name" /></label>
                <label class="field"><span>Title</span><input [readOnly]="!auth.isManager()" [(ngModel)]="member.title" /></label>
                <label class="field wide"
                  ><span>Biography</span
                  ><textarea rows="2" [readOnly]="!auth.isManager()" [(ngModel)]="member.biography"></textarea>
                </label>
                <label class="field wide"
                  ><span>Image URL</span><input [readOnly]="!auth.isManager()" [(ngModel)]="member.imageUrl"
                /></label>
              </div>
              <button
                class="remove"
                type="button"
                (click)="remove(index)"
                aria-label="Remove team member"
                [disabled]="!auth.isManager()"
              >
                <svg lucideTrash2></svg>
              </button>
            </article>
          }
        </div>
        <button class="btn btn-primary save" [disabled]="!auth.isManager() || saving()" (click)="save()">
          <svg lucideSave></svg>{{ saving() ? 'Saving…' : 'Save public team' }}
        </button>
      </section>
      <section class="panel">
        <div>
          <h2>Eviction reasons</h2>
          <p>Require a reason before completing every eviction workflow.</p>
        </div>
        <span class="enabled">Required</span>
      </section>
      <section class="panel">
        <div>
          <h2>File storage</h2>
          <p>
            Uses Zipline when server credentials are configured, with local storage as the
            development fallback.
          </p>
        </div>
        <span>Server-managed</span>
      </section>
      <section class="panel">
        <div>
          <h2>Discord identity</h2>
          <p>Employee identity is synchronized from the configured Discord OAuth application.</p>
        </div>
        <span>Server-managed</span>
      </section>
    </div>`,
  styles: [
    `
      .page-title {
        margin-bottom: 24px;
      }
      .page-title h1 {
        font-size: 2.5rem;
        margin: 4px 0;
      }
      .page-title p:last-child {
        color: var(--muted);
      }
      .settings-grid {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: 14px;
      }
      .settings-grid section {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 22px;
      }
      .settings-grid .team-editor {
        display: block;
        grid-column: 1 / -1;
      }
      .settings-grid .appearance-editor {
        display: block;
        grid-column: 1 / -1;
        padding: 0;
      }
      .team-editor header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 18px;
      }
      .members {
        display: grid;
        gap: 12px;
      }
      .members article {
        display: grid;
        grid-template-columns: 64px minmax(0, 1fr) 34px;
        gap: 12px;
        align-items: start;
        padding: 14px;
        border: 1px solid var(--border);
        border-radius: 12px;
      }
      .member-avatar img {
        width: 64px;
        height: 64px;
        border-radius: 50%;
        object-fit: cover;
      }
      .member-fields {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 12px;
        min-width: 0;
      }
      .member-fields .wide {
        grid-column: 1 / -1;
      }
      .member-fields input,
      .member-fields textarea {
        width: 100%;
      }
      .remove {
        width: 34px;
        height: 34px;
        border: 1px solid var(--border);
        border-radius: 8px;
        background: transparent;
        color: var(--danger);
        cursor: pointer;
        display: inline-flex;
        align-items: center;
        gap: 6px;
      }
      .remove svg {
        width: 15px;
        height: 15px;
      }
      .save {
        margin-top: 16px;
      }
      .settings-grid h2 {
        font-size: 1.05rem;
        margin-bottom: 5px;
      }
      .settings-grid p {
        color: var(--muted);
        font-size: 0.8rem;
        margin: 0;
      }
      .settings-grid span {
        color: var(--muted);
        font-size: 0.72rem;
      }
      .settings-grid .enabled {
        color: var(--forest);
        background: var(--forest-light);
        padding: 6px 9px;
        border-radius: 99px;
      }
      @media (max-width: 760px) {
        .settings-grid {
          grid-template-columns: 1fr;
        }
        .members article {
          grid-template-columns: 52px minmax(0, 1fr) 34px;
        }
        .members img {
          width: 52px;
          height: 52px;
        }
        .member-fields {
          grid-template-columns: 1fr;
        }
        .member-fields .wide {
          grid-column: 1;
        }
      }
    `,
  ],
})
export class SettingsComponent {
  readonly auth = inject(AuthService);
  private settings = inject(SettingsService);
  readonly members = signal<TeamMember[]>([]);
  readonly saving = signal(false);
  constructor() {
    this.settings.team().subscribe((members) => this.members.set(members));
  }
  add() {
    this.members.update((members) => [
      ...members,
      { id: crypto.randomUUID(), name: '', title: '', biography: '', imageUrl: '' },
    ]);
  }
  remove(index: number) {
    this.members.update((members) => members.filter((_, i) => i !== index));
  }
  save() {
    this.saving.set(true);
    this.settings.updateTeam(this.members()).subscribe({
      next: (members) => {
        this.members.set(members);
        this.saving.set(false);
      },
      error: () => this.saving.set(false),
    });
  }
}
