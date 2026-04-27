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
import { ChatSessionService } from '../../services/chat-session.service';
import { ErrorAlertComponent } from '../../shared/error-alert/error-alert.component';
import { AuthFormShellComponent } from '../../shared/auth-form-shell/auth-form-shell.component';

@Component({
  selector: 'app-signin',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule,
    ErrorAlertComponent, AuthFormShellComponent
  ],
  templateUrl: './signin.component.html',
  styleUrl: './signin.component.scss'
})
export class SigninComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private chatSession = inject(ChatSessionService);
  private router = inject(Router);

  form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  loading = signal(false);
  error = signal('');
  hidePassword = signal(true);

  submit() {
    if (this.form.invalid || this.loading()) return;
    this.loading.set(true);
    this.error.set('');

    this.auth.signIn({
      email: this.form.value.email!,
      password: this.form.value.password!
    }).subscribe({
      next: res => {
        if (res.success) {
          this.auth.handleAuthSuccess(res);
          // Navigate immediately after auth so the user is not blocked by chat session reset.
          this.loading.set(false);
          this.router.navigate(['/']);
          this.chatSession.startNewSession().subscribe({ error: () => {} });
        } else {
          this.loading.set(false);
          this.error.set(res.message);
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.message || 'Sign in failed. Please try again.');
      }
    });
  }
}
