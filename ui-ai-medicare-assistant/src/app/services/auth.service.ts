import { Injectable, signal, computed, inject, Injector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import {
  AuthResponse, AuthUser, SignUpRequest, SignInRequest,
  ForgotPasswordRequest, ResetPasswordRequest
} from '../models/auth.model';
import { DrugStateService } from './drug-state.service';
import { ProfileService } from './profile.service';
import { RecommendationStateService } from './recommendation-state.service';
import { ChatWizardService } from './chat-wizard.service';
import { ChatSignalRService } from './chat-signal-r.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';
  private readonly USER_KEY = 'auth_user';
  private readonly TOKEN_TS_KEY = 'auth_token_ts';
  private readonly TOKEN_MAX_AGE_MS = 60 * 60 * 1000; // 1 hour

  readonly currentUser = signal<AuthUser | null>(this.loadUser());
  readonly isAuthenticated = computed(() => !!this.currentUser() && !!this.getToken());

  private readonly injector = inject(Injector);

  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  signUp(req: SignUpRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/signup`, req);
  }

  signIn(req: SignInRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/signin`, req);
  }

  forgotPassword(req: ForgotPasswordRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/forgot-password`, req);
  }

  resetPassword(req: ResetPasswordRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/reset-password`, req);
  }

  handleAuthSuccess(res: AuthResponse) {
    if (res.token) {
      sessionStorage.setItem(this.TOKEN_KEY, res.token);
      sessionStorage.setItem(this.TOKEN_TS_KEY, Date.now().toString());
    }
    if (res.user) {
      sessionStorage.setItem(this.USER_KEY, JSON.stringify(res.user));
      this.currentUser.set(res.user);
    }
  }

  getToken(): string | null {
    const token = sessionStorage.getItem(this.TOKEN_KEY);
    if (!token) return null;

    const ts = sessionStorage.getItem(this.TOKEN_TS_KEY);
    if (!ts || Date.now() - Number(ts) > this.TOKEN_MAX_AGE_MS) {
      this.signOut();
      return null;
    }

    // Refresh timestamp on activity to keep session alive
    sessionStorage.setItem(this.TOKEN_TS_KEY, Date.now().toString());
    return token;
  }

  signOut() {
    this.finalizeSignOut();
  }

  private finalizeSignOut() {
    try {
      // Clear ALL sessionStorage (auth tokens + drug-analysis-state + FP keys)
      sessionStorage.clear();
      this.currentUser.set(null);

      // Reset in-memory service state to prevent data leaking to next user.
      const drugState = this.injector.get(DrugStateService);
      drugState.clearForSignOut();

      const profile = this.injector.get(ProfileService);
      profile.profile.set(null);
      profile.isProfileComplete.set(false);
      profile.profileLoadSettled.set(false);
      profile.pendingPrefill.set(null);

      const recState = this.injector.get(RecommendationStateService);
      recState.clear();

      const wizard = this.injector.get(ChatWizardService);
      wizard.reset();

      // Close the SignalR WebSocket so the next user gets a fresh connection.
      const signalR = this.injector.get(ChatSignalRService);
      signalR.disconnect();
    } finally {
      // Fail-safe logout destination: always leave protected area.
      this.router.navigateByUrl('/signin', { replaceUrl: true });
    }
  }

  private loadUser(): AuthUser | null {
    const json = sessionStorage.getItem(this.USER_KEY);
    if (!json) return null;

    const ts = sessionStorage.getItem(this.TOKEN_TS_KEY);
    if (!ts || Date.now() - Number(ts) > this.TOKEN_MAX_AGE_MS) {
      sessionStorage.clear();
      return null;
    }

    return JSON.parse(json);
  }
}
