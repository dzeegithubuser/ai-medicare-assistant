import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  CreateEndUserRequest, EndUserSummary, RecommendationByUser
} from '../models/role-management.model';

@Injectable({ providedIn: 'root' })
export class FinancialPlannerService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/api/financial-planner`;

  listEndUsers() {
    return this.http.get<EndUserSummary[]>(`${this.base}/me/end-users`);
  }

  createEndUser(req: CreateEndUserRequest) {
    return this.http.post<EndUserSummary>(`${this.base}/me/end-users`, req);
  }

  listRecommendations() {
    return this.http.get<RecommendationByUser[]>(`${this.base}/me/recommendations`);
  }

  deleteRecommendation(recommendationId: string) {
    return this.http.delete<void>(`${this.base}/me/recommendations/${recommendationId}`);
  }

  /** Cascade-deletes the end-user and all their per-user docs (profile, chat, recs, selections, LTC). */
  deleteEndUser(endUserId: string) {
    return this.http.delete<void>(`${this.base}/me/end-users/${endUserId}`);
  }
}
