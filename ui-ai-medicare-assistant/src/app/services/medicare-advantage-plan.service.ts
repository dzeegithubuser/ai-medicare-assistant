import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  MedicareAdvantagePlanRequest,
  MedicareAdvantagePlanResponse
} from '../models/medicare-advantage-plan.model';

@Injectable({ providedIn: 'root' })
export class MedicareAdvantagePlanService {

  constructor(private http: HttpClient) {}

  recommend(request: MedicareAdvantagePlanRequest): Observable<MedicareAdvantagePlanResponse> {
    return this.http.post<MedicareAdvantagePlanResponse>(
      `${environment.apiUrl}/api/MedicareAdvantagePlan/recommend`,
      request
    );
  }
}
