import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/services/theme.service';
import { DataRefreshStatusComponent } from './shared/components/data-refresh-status/data-refresh-status.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, DataRefreshStatusComponent],
  template: '<router-outlet /><app-data-refresh-status />',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly theme = inject(ThemeService);
}
