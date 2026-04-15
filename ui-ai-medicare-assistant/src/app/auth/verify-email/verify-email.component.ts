import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatButtonModule, MatIconModule, MatProgressSpinnerModule,
    MatFormFieldModule, MatInputModule
  ],
  templateUrl: './verify-email.component.html',
  styleUrl: './verify-email.component.scss'
})
export class VerifyEmailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private auth = inject(AuthService);
  private fb = inject(FormBuilder);

  loading = signal(true);
  success = signal(false);
  error = signal('');

  // Resend flow (shown when verification fails)
  showResendForm = signal(false);
  resendLoading = signal(false);
  resendSuccess = signal('');
  resendCooldown = signal(0);
  private cooldownInterval: ReturnType<typeof setInterval> | null = null;

  resendForm = this.fb.group({
    email: ['', [Validators.required, Validators.email]]
  });

  ngOnInit() {
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!token) {
      this.loading.set(false);
      this.error.set('Invalid or missing verification link. Please check your email and try again.');
      return;
    }

    this.auth.verifyEmail(token).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success) {
          this.success.set(true);
          setTimeout(() => this.router.navigate(['/signin']), 3000);
        } else {
          this.error.set(res.message || 'Verification failed. The link may have expired or already been used.');
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.message || 'Verification failed. The link may have expired or already been used.');
      }
    });
  }

  submitResend() {
    if (this.resendForm.invalid || this.resendLoading() || this.resendCooldown() > 0) return;
    this.resendLoading.set(true);
    this.resendSuccess.set('');

    this.auth.resendVerification(this.resendForm.value.email!).subscribe({
      next: () => {
        this.resendLoading.set(false);
        this.resendSuccess.set('A new verification email has been sent. Please check your inbox.');
        this.startCooldown();
      },
      error: () => {
        this.resendLoading.set(false);
        this.resendSuccess.set('A new verification email has been sent. Please check your inbox.');
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
