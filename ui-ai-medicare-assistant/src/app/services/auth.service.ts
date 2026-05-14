import { Injectable, signal, computed, inject, Injector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { tap } from 'rxjs/operators';
import {
  AuthResponse, AuthUser, ImpersonationResponse, SignInRequest,
  ForgotPasswordRequest, ResetPasswordRequest, ChangePasswordRequest,
  VerifyEmailRequest, ResendVerificationRequest
} from '../models/auth.model';
import { MedicareStateService } from './drug-state.service';
import { ProfileService } from './profile.service';
import { RecommendationStateService } from './recommendation-state.service';
import { ChatWizardService } from './chat-wizard.service';
import { ChatSignalRService } from './chat-signal-r.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';
  private readonly USER_KEY = 'auth_user';
  private readonly TOKEN_TS_KEY = 'auth_token_ts';
  private readonly TOKEN_KEY_ORIGINAL = 'auth_token_original';
  private readonly USER_KEY_ORIGINAL = 'auth_user_original';
  private readonly TOKEN_TS_KEY_ORIGINAL = 'auth_token_ts_original';
  private readonly IMPERSONATION_EXPIRES_KEY = 'auth_impersonation_expires';
  private readonly TOKEN_MAX_AGE_MS = 60 * 60 * 1000; // 1 hour

  // Impersonation state signal — initialized from sessionStorage so it survives reload.
  // The constructor-time call also gracefully restores FP credentials if a stale
  // impersonation token is found expired on page load.
  private readonly _impersonationExpiresAt = signal<Date | null>(this.loadImpersonationExpiry());
  readonly impersonationExpiresAt = this._impersonationExpiresAt.asReadonly();

  readonly currentUser = signal<AuthUser | null>(this.loadUser());
  readonly isAuthenticated = computed(() => !!this.currentUser() && !!this.getToken());
  readonly currentRole = computed(() => this.currentUser()?.role ?? null);

  private readonly injector = inject(Injector);

  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  signIn(req: SignInRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/signin`, req);
  }

  forgotPassword(req: ForgotPasswordRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/forgot-password`, req);
  }

  resetPassword(req: ResetPasswordRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/reset-password`, req);
  }

  changePassword(req: ChangePasswordRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/change-password`, req);
  }

  verifyEmail(token: string) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/verify-email`, { token } satisfies VerifyEmailRequest);
  }

  resendVerification(email: string) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/api/auth/resend-verification`, { email } satisfies ResendVerificationRequest);
  }

  impersonate(targetUserId: string) {
    return this.http
      .post<ImpersonationResponse>(`${environment.apiUrl}/api/impersonate`, { targetUserId })
      .pipe(tap(res => this.applyImpersonation(res)));
  }

  refreshImpersonation() {
    return this.http
      .post<ImpersonationResponse>(`${environment.apiUrl}/api/impersonate/refresh`, {})
      .pipe(tap(res => {
        sessionStorage.setItem(this.TOKEN_KEY, res.token);
        sessionStorage.setItem(this.TOKEN_TS_KEY, Date.now().toString());
        sessionStorage.setItem(this.IMPERSONATION_EXPIRES_KEY, res.expiresAt);
        this._impersonationExpiresAt.set(new Date(res.expiresAt));
      }));
  }

  isImpersonating(): boolean {
    return this._impersonationExpiresAt() !== null;
  }

  exitImpersonation() {
    if (!this.isImpersonating()) return;

    const fpToken = sessionStorage.getItem(this.TOKEN_KEY_ORIGINAL);
    const fpUser  = sessionStorage.getItem(this.USER_KEY_ORIGINAL);
    const fpTs    = sessionStorage.getItem(this.TOKEN_TS_KEY_ORIGINAL);

    if (fpToken) sessionStorage.setItem(this.TOKEN_KEY, fpToken);
    if (fpUser)  sessionStorage.setItem(this.USER_KEY, fpUser);
    if (fpTs)    sessionStorage.setItem(this.TOKEN_TS_KEY, fpTs);

    sessionStorage.removeItem(this.TOKEN_KEY_ORIGINAL);
    sessionStorage.removeItem(this.USER_KEY_ORIGINAL);
    sessionStorage.removeItem(this.TOKEN_TS_KEY_ORIGINAL);
    sessionStorage.removeItem(this.IMPERSONATION_EXPIRES_KEY);

    this._impersonationExpiresAt.set(null);
    this.currentUser.set(fpUser ? JSON.parse(fpUser) as AuthUser : null);
    this.router.navigateByUrl('/', { replaceUrl: true });
  }

  private applyImpersonation(res: ImpersonationResponse) {
    const fpToken = sessionStorage.getItem(this.TOKEN_KEY);
    const fpUser  = sessionStorage.getItem(this.USER_KEY);
    const fpTs    = sessionStorage.getItem(this.TOKEN_TS_KEY);

    if (fpToken) sessionStorage.setItem(this.TOKEN_KEY_ORIGINAL, fpToken);
    if (fpUser)  sessionStorage.setItem(this.USER_KEY_ORIGINAL, fpUser);
    if (fpTs)    sessionStorage.setItem(this.TOKEN_TS_KEY_ORIGINAL, fpTs);

    sessionStorage.setItem(this.TOKEN_KEY, res.token);
    sessionStorage.setItem(this.TOKEN_TS_KEY, Date.now().toString());
    sessionStorage.setItem(this.IMPERSONATION_EXPIRES_KEY, res.expiresAt);

    const impersonated: AuthUser = {
      id: res.targetUserId,
      email: res.targetEmail,
      phone: '',
      role: 'user',
      fpId: res.actingAsUserId,
      mustChangePassword: false
    };
    sessionStorage.setItem(this.USER_KEY, JSON.stringify(impersonated));
    this.currentUser.set(impersonated);
    this._impersonationExpiresAt.set(new Date(res.expiresAt));
  }

  /** Loads impersonation expiry from sessionStorage. If the stored value is already in
   *  the past, gracefully restores the FP's original credentials and returns null. */
  private loadImpersonationExpiry(): Date | null {
    const iso = sessionStorage.getItem(this.IMPERSONATION_EXPIRES_KEY);
    if (!iso) return null;
    const date = new Date(iso);
    if (date.getTime() <= Date.now()) {
      // Expired during a previous page-life — silently restore FP credentials.
      const fpToken = sessionStorage.getItem(this.TOKEN_KEY_ORIGINAL);
      const fpUser  = sessionStorage.getItem(this.USER_KEY_ORIGINAL);
      const fpTs    = sessionStorage.getItem(this.TOKEN_TS_KEY_ORIGINAL);
      if (fpToken && fpUser && fpTs) {
        sessionStorage.setItem(this.TOKEN_KEY, fpToken);
        sessionStorage.setItem(this.USER_KEY, fpUser);
        sessionStorage.setItem(this.TOKEN_TS_KEY, fpTs);
      }
      sessionStorage.removeItem(this.TOKEN_KEY_ORIGINAL);
      sessionStorage.removeItem(this.USER_KEY_ORIGINAL);
      sessionStorage.removeItem(this.TOKEN_TS_KEY_ORIGINAL);
      sessionStorage.removeItem(this.IMPERSONATION_EXPIRES_KEY);
      return null;
    }
    return date;
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
      const drugState = this.injector.get(MedicareStateService);
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
