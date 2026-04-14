import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AvailablePlanSummary {
  planName: string;
  contractId: string;
  planId: string;
  premium: number;
  starRating: number;
}

export interface AvailableMedigapSummary {
  company: string;
  plan: string;
  monthlyPremium: number;
}

export interface SelectedPlansSummary {
  partD: string | null;
  medigap: string | null;
  ma: string | null;
  maGapPartD: string | null;
}

export interface PlanSelectionExtractRequest {
  message: string;
  activeSection: string | null;
  availablePartDPlans: AvailablePlanSummary[];
  availableMedigapPlans: AvailableMedigapSummary[];
  availableMAPlans: AvailablePlanSummary[];
  selectedPlans: SelectedPlansSummary;
}

export interface PlanSelectionExtractResponse {
  planName: string | null;
  planCategory: 'partd' | 'medigap' | 'ma' | null;
  action: 'select' | 'remove' | 'list' | 'info';
  reply: string;
}

@Injectable({ providedIn: 'root' })
export class ChatPlanSelectionService {
  constructor(private http: HttpClient) {}

  extractSelection(request: PlanSelectionExtractRequest): Observable<PlanSelectionExtractResponse> {
    return this.http.post<PlanSelectionExtractResponse>(
      `${environment.apiUrl}/api/chat/extract-plan-selection`,
      request
    );
  }
}
