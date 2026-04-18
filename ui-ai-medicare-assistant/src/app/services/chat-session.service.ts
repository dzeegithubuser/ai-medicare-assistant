import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ChatSessionResponse {
  uiState: Record<string, unknown>;
}

@Injectable({ providedIn: 'root' })
export class ChatSessionService {
  constructor(private http: HttpClient) {}

  startNewSession(): Observable<ChatSessionResponse> {
    return this.http.post<ChatSessionResponse>(`${environment.apiUrl}/api/chat/session/start-new`, {});
  }
}
