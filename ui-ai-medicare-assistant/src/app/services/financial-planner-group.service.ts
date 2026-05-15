import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  CreateFpRequest, EndUserSummary, FpgSummary, FpSummary,
  RecommendationByUser, UpdateFpRequest
} from '../models/role-management.model';

@Injectable({ providedIn: 'root' })
export class FinancialPlannerGroupService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/api/financial-planner-group`;

  /**
   * Returns the caller's group, or `null` when none exists.
   * The backend returns `200 OK` with a `null` body for the empty case
   * (legacy FPG admin whose auto-created group was deleted out of band).
   */
  getMyGroup() {
    return this.http.get<FpgSummary | null>(`${this.base}/me`);
  }

  listFinancialPlanners() {
    return this.http.get<FpSummary[]>(`${this.base}/me/financial-planners`);
  }

  createFinancialPlanner(req: CreateFpRequest) {
    return this.http.post<FpSummary>(`${this.base}/me/financial-planners`, req);
  }

  updateFinancialPlanner(fpUserId: string, req: UpdateFpRequest) {
    return this.http.put<FpSummary>(`${this.base}/me/financial-planners/${fpUserId}`, req);
  }

  deleteFinancialPlanner(fpUserId: string) {
    return this.http.delete<void>(`${this.base}/me/financial-planners/${fpUserId}`);
  }

  listGroupEndUsers() {
    return this.http.get<EndUserSummary[]>(`${this.base}/me/end-users`);
  }

  listGroupRecommendations() {
    return this.http.get<RecommendationByUser[]>(`${this.base}/me/recommendations`);
  }
}
