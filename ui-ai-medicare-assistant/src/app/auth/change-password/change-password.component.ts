import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';
import { ErrorAlertComponent } from '../../shared/error-alert/error-alert.component';
import { AuthFormShellComponent } from '../../shared/auth-form-shell/auth-form-shell.component';
import { passwordMatchValidator } from '../../shared/validators/password-match.validator';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule,
    ErrorAlertComponent, AuthFormShellComponent
  ],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss'
})
export class ChangePasswordComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  form = this.fb.group({
    oldPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: passwordMatchValidator });

  loading = signal(false);
  error = signal('');
  successMessage = signal('');
  hideOldPassword = signal(true);
  hideNewPassword = signal(true);
  hideConfirmPassword = signal(true);

  submit() {
    if (this.form.invalid || this.loading()) return;
    this.loading.set(true);
    this.error.set('');
    this.successMessage.set('');

    this.auth.changePassword({
      oldPassword: this.form.value.oldPassword!,
      newPassword: this.form.value.newPassword!,
      confirmPassword: this.form.value.confirmPassword!
    }).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success) {
          this.auth.handleAuthSuccess(res);
          this.successMessage.set('Password changed successfully!');
          this.form.reset();
          setTimeout(() => this.router.navigate(['/']), 2000);
        } else {
          this.error.set(res.message);
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.message || 'Failed to change password. Please try again.');
      }
    });
  }

  cancel() {
    this.router.navigate(['/']);
  }
}
