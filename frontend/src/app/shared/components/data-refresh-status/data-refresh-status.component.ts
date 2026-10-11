import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DataModelService } from '../../../core/services/data-model.service';

@Component({
  selector: 'app-data-refresh-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (models.refreshing()) {
      <div class="refresh-status" role="status" aria-live="polite">
        Showing saved data · Updating…
      </div>
    } @else if (models.refreshFailed()) {
      <div class="refresh-status warning" role="status" aria-live="polite">
        <span>Saved data may be out of date. Refresh failed.</span>
        <button type="button" (click)="reload()">Reload</button>
      </div>
    }
  `,
  styles: `
    .refresh-status {
      position: fixed;
      inset: auto 16px 16px auto;
      z-index: 80;
      display: flex;
      align-items: center;
      gap: 12px;
      max-width: min(410px, calc(100vw - 32px));
      padding: 10px 14px;
      border: 1px solid var(--border);
      border-radius: 10px;
      background: var(--surface);
      color: var(--ink);
      box-shadow: var(--shadow-lg);
      font-size: 0.78rem;
    }
    .warning {
      border-color: var(--warning-ink);
    }
    button {
      border: 0;
      background: none;
      color: var(--forest);
      font: inherit;
      font-weight: 700;
      text-decoration: underline;
      cursor: pointer;
      padding: 5px;
    }
  `,
})
export class DataRefreshStatusComponent {
  readonly models = inject(DataModelService);
  reload(): void {
    window.location.reload();
  }
}
