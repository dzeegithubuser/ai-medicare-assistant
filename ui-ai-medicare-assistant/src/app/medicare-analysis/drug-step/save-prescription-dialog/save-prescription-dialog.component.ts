import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialogRef, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface SavePrescriptionDialogData {
  title?: string;
  subtitle?: string;
  icon?: string;
}

@Component({
  selector: 'app-save-prescription-dialog',
  templateUrl: './save-prescription-dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule, MatDialogModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule
  ],
  standalone: true,
})
export class SavePrescriptionDialogComponent {
  private dialogRef = inject(MatDialogRef<SavePrescriptionDialogComponent>);
  private data: SavePrescriptionDialogData = inject(MAT_DIALOG_DATA, { optional: true }) ?? {};

  title = this.data.title ?? 'Save Prescription';
  subtitle = this.data.subtitle ?? 'Enter a name for this prescription to save it';
  icon = this.data.icon ?? 'save';

  prescriptionName = signal('');

  submit() {
    const name = this.prescriptionName().trim();
    if (name) {
      this.dialogRef.close(name);
    }
  }

  cancel() {
    this.dialogRef.close(null);
  }
}
