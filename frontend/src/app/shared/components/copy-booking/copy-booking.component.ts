import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { LucideCopy } from '@lucide/angular';
import { Property, PropertyBooking } from '../../../core/models/property.models';
import { AuthService } from '../../../core/services/auth.service';
import { bookingMessage } from '../../utilities/booking-message';

@Component({
  selector: 'app-copy-booking',
  imports: [LucideCopy],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button
    type="button"
    class="btn btn-secondary"
    [disabled]="copying()"
    [attr.aria-label]="'Copy booking for ' + booking().fullName"
    (click)="copy()"
  >
    <svg lucideCopy></svg>{{ copying() ? 'Copying…' : 'Copy Booking' }}
  </button>`,
  styles: [
    `
      button {
        white-space: nowrap;
      }
      svg {
        width: 16px;
        height: 16px;
        flex-shrink: 0;
      }
    `,
  ],
})
export class CopyBookingComponent {
  readonly property = input.required<
    Pick<Property, 'propertyName' | 'blockName' | 'type'> & { images?: string[] }
  >();
  readonly booking = input.required<PropertyBooking>();
  readonly copying = signal(false);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  async copy() {
    if (this.copying()) return;
    this.copying.set(true);
    try {
      await navigator.clipboard.writeText(
        bookingMessage(this.property(), this.booking(), this.auth.user()?.discordUserId),
      );
      this.snackBar.open('Booking copied.', 'Dismiss', { duration: 2500 });
    } catch {
      this.snackBar.open(
        'Could not copy booking. Allow clipboard access and try again.',
        'Dismiss',
        {
          duration: 4500,
          panelClass: ['error-toast'],
        },
      );
    } finally {
      this.copying.set(false);
    }
  }
}
