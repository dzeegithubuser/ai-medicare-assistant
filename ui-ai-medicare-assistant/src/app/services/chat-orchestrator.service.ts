import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Observable } from 'rxjs';
import { OrchestratorRequest, OrchestratorResponse } from '../models/orchestrator.model';

@Injectable({ providedIn: 'root' })
export class ChatOrchestratorService {
  private readonly baseUrl = `${environment.apiUrl}/api/chat`;

  constructor(private http: HttpClient) {}

  sendMessage(message: string, currentPage?: string): Observable<OrchestratorResponse> {
    const body: OrchestratorRequest = { message, currentPage };
    return this.http.post<OrchestratorResponse>(`${this.baseUrl}/orchestrate`, body);
  }
}
