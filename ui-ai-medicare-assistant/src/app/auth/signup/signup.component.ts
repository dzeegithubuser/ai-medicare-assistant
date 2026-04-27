import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';
import { ErrorAlertComponent } from '../../shared/error-alert/error-alert.component';
import { AuthFormShellComponent } from '../../shared/auth-form-shell/auth-form-shell.component';

const US_PHONE_PATTERN = /^(\+1[\s.\-]?)?(\(?\d{3}\)?[\s.\-]?)(\d{3}[\s.\-]?\d{4})$/;

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule,
    ErrorAlertComponent, AuthFormShellComponent
  ],
  templateUrl: './signup.component.html',
  styleUrl: './signup.component.scss'
})
export class SignupComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.required, Validators.pattern(US_PHONE_PATTERN)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  });

  loading = signal(false);
  error = signal('');
  hidePassword = signal(true);
  hideConfirm = signal(true);

  // Set after successful signup to show verification panel
  pendingEmail = signal('');

  // Resend cooldown state
  resendLoading = signal(false);
  resendSuccess = signal('');
  resendCooldown = signal(0);
  private cooldownInterval: ReturnType<typeof setInterval> | null = null;

  submit() {
    if (this.form.invalid || this.loading()) return;
    if (this.form.value.password !== this.form.value.confirmPassword) {
      this.error.set('Passwords do not match.');
      return;
    }
    this.loading.set(true);
    this.error.set('');

    this.auth.signUp({
      email: this.form.value.email!,
      phone: this.form.value.phone!,
      password: this.form.value.password!,
      confirmPassword: this.form.value.confirmPassword!
    }).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success) {
          this.pendingEmail.set(this.form.value.email!);
        } else {
          this.error.set(res.message);
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.message || 'Sign up failed. Please try again.');
      }
    });
  }

  resendEmail() {
    if (this.resendLoading() || this.resendCooldown() > 0) return;
    this.resendLoading.set(true);
    this.resendSuccess.set('');

    this.auth.resendVerification(this.pendingEmail()).subscribe({
      next: () => {
        this.resendLoading.set(false);
        this.resendSuccess.set('Verification email resent. Please check your inbox.');
        this.startCooldown();
      },
      error: () => {
        this.resendLoading.set(false);
        this.resendSuccess.set('Verification email resent. Please check your inbox.');
        this.startCooldown();
      }
    });
  }

  private startCooldown() {
    this.resendCooldown.set(60);
    if (this.cooldownInterval) clearInterval(this.cooldownInterval);
    this.cooldownInterval = setInterval(() => {
      const current = this.resendCooldown();
      if (current <= 1) {
        this.resendCooldown.set(0);
        clearInterval(this.cooldownInterval!);
        this.cooldownInterval = null;
      } else {
        this.resendCooldown.set(current - 1);
      }
    }, 1000);
  }
}
