import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AdminService } from '../services/admin.service';
import { UserSummary } from '../models/role-management.model';

@Component({
  selector: 'app-create-admin-user-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule,
  ],
  templateUrl: './create-admin-user-dialog.component.html',
})
export class CreateAdminUserDialogComponent {
  private fb = inject(FormBuilder);
  private adminService = inject(AdminService);
  private dialogRef = inject(MatDialogRef<CreateAdminUserDialogComponent, UserSummary | undefined>);

  protected submitting = signal(false);
  protected error = signal('');

  protected form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    firstName: ['', [Validators.required, Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.maxLength(50)]],
    phone: ['', [Validators.required, Validators.pattern(/^(\+1[\s.\-]?)?(\(?\d{3}\)?[\s.\-]?)(\d{3}[\s.\-]?\d{4})$/)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected submit() {
    if (this.form.invalid || this.submitting()) return;
    this.submitting.set(true);
    this.error.set('');
    this.adminService.createFpgAdminUser({
      email: this.form.value.email!,
      firstName: this.form.value.firstName!,
      lastName: this.form.value.lastName!,
      phone: this.form.value.phone!,
      password: this.form.value.password!,
    }).subscribe({
      next: user => this.dialogRef.close(user),
      error: err => {
        this.submitting.set(false);
        this.error.set(err.error?.message ?? 'Failed to create FPG admin.');
      },
    });
  }

  protected cancel() {
    this.dialogRef.close(undefined);
  }
}
