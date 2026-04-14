import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  PartDPlanRecommendationRequest,
  PartDPlanRecommendationResponse
} from '../models/part-d-plan.model';

@Injectable({ providedIn: 'root' })
export class PartDPlanService {

  constructor(private http: HttpClient) {}

  recommend(request: PartDPlanRecommendationRequest): Observable<PartDPlanRecommendationResponse> {
    return this.http.post<PartDPlanRecommendationResponse>(
      `${environment.apiUrl}/api/PartDPlan/recommend`,
      request
    );
  }
}
