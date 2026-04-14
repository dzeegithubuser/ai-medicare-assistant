import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { UserProfileResponse, ProfileDto } from '../models/profile.model';
import { Observable, tap, finalize } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ProfileService {
  readonly profile = signal<UserProfileResponse | null>(null);
  readonly isProfileComplete = signal(false);
  /** True after the first GET /api/profile attempt finishes (success or error). Used to avoid wizard/chat races before `isProfileComplete` is trustworthy. */
  readonly profileLoadSettled = signal(false);
  /** Pre-fill data injected by chat intent before navigating to profile page */
  readonly pendingPrefill = signal<Record<string, unknown> | null>(null);
  /** Partial profile data extracted from chat. UserProfileComponent watches this and patches the form. */
  readonly pendingChatProfileData = signal<Record<string, unknown> | null>(null);
  /** Required fields still empty in the profile form. Set by UserProfileComponent. */
  readonly missingRequiredFields = signal<string[]>([]);
  /** Incremented when chat requests direct profile save. */
  readonly chatSaveRequestId = signal(0);
  /** True while a chat-triggered profile save is in progress. */
  readonly chatSaveInProgress = signal(false);
  /** Incremented when chat requests discarding unsaved profile edits. */
  readonly chatDiscardRequestId = signal(0);

  constructor(private http: HttpClient) {}

  loadProfile(): Observable<UserProfileResponse> {
    return this.http.get<UserProfileResponse>(`${environment.apiUrl}/api/profile`).pipe(
      tap(p => this.updateState(p)),
      finalize(() => this.profileLoadSettled.set(true))
    );
  }

  saveProfile(dto: ProfileDto): Observable<UserProfileResponse> {
    return this.http.post<UserProfileResponse>(`${environment.apiUrl}/api/profile`, dto).pipe(
      tap(p => this.updateState(p))
    );
  }

  updateState(p: UserProfileResponse) {
    this.profile.set(p);
    this.isProfileComplete.set(p.isProfileComplete);
  }

  requestSaveFromChat(): void {
    this.chatSaveRequestId.update(v => v + 1);
    this.chatSaveInProgress.set(true);
  }

  requestDiscardFromChat(): void {
    this.chatDiscardRequestId.update(v => v + 1);
  }
}
