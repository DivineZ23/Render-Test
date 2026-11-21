import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';

@Component({
  selector: 'app-copy-property-rent-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatDialogClose,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Rent for property message</h2>
    <form (submit)="$event.preventDefault(); confirm()">
      <mat-dialog-content>
        <p>
          {{ data.propertyName }} has no active bookings. Enter the rent to include in the copied
          message. This will not change the property's saved rent.
        </p>
        <label class="field"
          ><span>Rent ($)</span>
          <input type="number" min="0" step="0.01" [formControl]="rent" autofocus />
        </label>
        @if (rent.invalid && rent.touched) {
          <p class="error" role="alert">Enter a non-negative rent with up to two decimal places.</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button class="btn btn-secondary" type="button" mat-dialog-close>Cancel</button>
        <button class="btn btn-primary" type="submit" [disabled]="rent.invalid">
          Copy to clipboard
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      mat-dialog-content {
        max-width: 440px;
      }
      p {
        color: var(--muted);
      }
      .error {
        color: var(--danger);
      }
    `,
  ],
})
export class CopyPropertyRentDialogComponent {
  readonly data = inject<{ propertyName: string; rent?: number }>(MAT_DIALOG_DATA);
  private readonly dialog = inject(MatDialogRef<CopyPropertyRentDialogComponent, number>);
  readonly rent = new FormControl<number | null>(
    this.data.rent && this.data.rent > 0 ? this.data.rent : null,
    [Validators.required, Validators.min(0), Validators.pattern(/^\d+(\.\d{1,2})?$/)],
  );

  confirm() {
    this.rent.markAsTouched();
    if (this.rent.valid && this.rent.value !== null && Number.isFinite(this.rent.value)) {
      this.dialog.close(this.rent.value);
    }
  }
}
