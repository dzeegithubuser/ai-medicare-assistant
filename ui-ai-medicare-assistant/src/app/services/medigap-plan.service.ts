import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  MedigapPlanQuotesRequest,
  MedigapPlanQuotesResponse
} from '../models/medigap-plan.model';

@Injectable({ providedIn: 'root' })
export class MedigapPlanService {

  constructor(private http: HttpClient) {}

  getQuotes(request: MedigapPlanQuotesRequest): Observable<MedigapPlanQuotesResponse> {
    return this.http.post<MedigapPlanQuotesResponse>(
      `${environment.apiUrl}/api/MedigapPlan/quotes`,
      request
    );
  }
}
