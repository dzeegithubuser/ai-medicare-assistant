import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  CreateFpgAdminUserRequest, CreateFpgRequest, FpgSummary, UserSummary
} from '../models/role-management.model';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/api/admin`;

  listGroups() {
    return this.http.get<FpgSummary[]>(`${this.base}/financial-planner-groups`);
  }

  createGroup(req: CreateFpgRequest) {
    return this.http.post<FpgSummary>(`${this.base}/financial-planner-groups`, req);
  }

  createGroupAdminUser(fpgId: string, req: CreateFpgAdminUserRequest) {
    return this.http.post<UserSummary>(
      `${this.base}/financial-planner-groups/${fpgId}/admin-user`, req);
  }

  /** List every user with role `financial_planner_group` (the group is hidden from the UI). */
  listFpgAdminUsers() {
    return this.http.get<UserSummary[]>(`${this.base}/fpg-admin-users`);
  }

  /** Create an FPG-admin user; the backend auto-creates the underlying group. */
  createFpgAdminUser(req: CreateFpgAdminUserRequest) {
    return this.http.post<UserSummary>(`${this.base}/fpg-admin-users`, req);
  }

  /** Delete an FPG-admin user. Backend rejects (409) if their group still has FPs. */
  deleteFpgAdminUser(userId: string) {
    return this.http.delete<void>(`${this.base}/fpg-admin-users/${userId}`);
  }
}
