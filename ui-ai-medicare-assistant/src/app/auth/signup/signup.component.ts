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

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule
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
    phone: ['', [Validators.required, Validators.pattern(/^\+?[\d\s-]{7,20}$/)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  });

  loading = signal(false);
  error = signal('');
  hidePassword = signal(true);
  hideConfirm = signal(true);

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
          this.auth.handleAuthSuccess(res);
          this.router.navigate(['/']);
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
}
