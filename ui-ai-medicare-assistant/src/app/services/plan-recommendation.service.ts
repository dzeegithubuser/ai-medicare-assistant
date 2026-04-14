import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CalculateCostsRequest, EvaluateCostsResponse } from '../models/cost-projection.model';

@Injectable({ providedIn: 'root' })
export class PlanRecommendationService {

  constructor(private http: HttpClient) { }

  evaluateCosts(request: CalculateCostsRequest): Observable<EvaluateCostsResponse> {
    return this.http.post<EvaluateCostsResponse>(
      `${environment.apiUrl}/api/PlanRecommendation/evaluate-costs`,
      request
    );
  }
}
