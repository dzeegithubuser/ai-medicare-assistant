import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ChatSessionMessageDto {
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: string;
}

export interface ChatSessionResponse {
  messages: ChatSessionMessageDto[];
  uiState: Record<string, unknown>;
}

@Injectable({ providedIn: 'root' })
export class ChatSessionService {
  constructor(private http: HttpClient) {}

  getSession(): Observable<ChatSessionResponse> {
    return this.http.get<ChatSessionResponse>(`${environment.apiUrl}/api/chat/session`);
  }

  updateMessages(messages: ChatSessionMessageDto[]): Observable<ChatSessionResponse> {
    return this.http.patch<ChatSessionResponse>(`${environment.apiUrl}/api/chat/session/messages`, { messages });
  }

  startNewSession(): Observable<ChatSessionResponse> {
    return this.http.post<ChatSessionResponse>(`${environment.apiUrl}/api/chat/session/start-new`, {});
  }

  clearSession(): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/api/chat/session`);
  }
}
