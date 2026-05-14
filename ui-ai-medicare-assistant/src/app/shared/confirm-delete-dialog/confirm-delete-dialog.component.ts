import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

export interface ConfirmDeleteDialogData {
  /** Dialog title — e.g. "Delete FPG admin". */
  title: string;
  /** Bold subject line shown above the warning — usually a name/email. */
  subject: string;
  /** Free-form warning text describing what will be removed. */
  warning: string;
  /** Token the user must type verbatim to enable the confirm button (case-insensitive). */
  confirmationToken: string;
  /** Optional label shown above the input field. Defaults to: "Type <token> to confirm". */
  inputLabel?: string;
  /** Optional confirm-button label. Defaults to "Delete". */
  confirmLabel?: string;
}

@Component({
  selector: 'app-confirm-delete-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, FormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatIconModule, MatInputModule,
  ],
  templateUrl: './confirm-delete-dialog.component.html',
})
export class ConfirmDeleteDialogComponent {
  protected data = inject<ConfirmDeleteDialogData>(MAT_DIALOG_DATA);
  private dialogRef = inject(MatDialogRef<ConfirmDeleteDialogComponent, boolean>);

  protected typedValue = signal('');
  protected canConfirm = computed(() =>
    this.typedValue().trim().toLowerCase() === this.data.confirmationToken.trim().toLowerCase()
  );

  protected get inputLabel(): string {
    return this.data.inputLabel ?? `Type "${this.data.confirmationToken}" to confirm`;
  }

  protected get confirmLabel(): string {
    return this.data.confirmLabel ?? 'Delete';
  }

  protected confirm() {
    if (this.canConfirm()) this.dialogRef.close(true);
  }

  protected cancel() {
    this.dialogRef.close(false);
  }
}
