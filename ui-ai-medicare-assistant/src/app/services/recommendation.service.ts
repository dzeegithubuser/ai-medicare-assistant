import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Observable } from 'rxjs';
import {
  RecommendationResponse,
  RecommendationSummaryResponse,
  CreateRecommendationRequest,
  UpdateProfileRequest,
  UpdateDrugsRequest,
  UpdatePharmacyRequest,
  UpdatePlansRequest
} from '../models/recommendation.model';

@Injectable({ providedIn: 'root' })
export class RecommendationService {
  private readonly baseUrl = `${environment.apiUrl}/api/recommendation`;

  constructor(private http: HttpClient) {}

  getActive(): Observable<RecommendationResponse> {
    return this.http.get<RecommendationResponse>(this.baseUrl);
  }

  getAll(): Observable<RecommendationSummaryResponse[]> {
    return this.http.get<RecommendationSummaryResponse[]>(`${this.baseUrl}/all`);
  }

  getById(id: string): Observable<RecommendationResponse> {
    return this.http.get<RecommendationResponse>(`${this.baseUrl}/${encodeURIComponent(id)}`);
  }

  create(request: CreateRecommendationRequest, force = false): Observable<RecommendationResponse> {
    const params = force ? new HttpParams().set('force', 'true') : undefined;
    return this.http.post<RecommendationResponse>(this.baseUrl, request, { params });
  }

  updateProfile(request: UpdateProfileRequest): Observable<RecommendationResponse> {
    return this.http.put<RecommendationResponse>(`${this.baseUrl}/profile`, request);
  }

  updateDrugs(request: UpdateDrugsRequest): Observable<RecommendationResponse> {
    return this.http.put<RecommendationResponse>(`${this.baseUrl}/drugs`, request);
  }

  updatePharmacy(request: UpdatePharmacyRequest): Observable<RecommendationResponse> {
    return this.http.put<RecommendationResponse>(`${this.baseUrl}/pharmacy`, request);
  }

  updatePlans(request: UpdatePlansRequest): Observable<RecommendationResponse> {
    return this.http.put<RecommendationResponse>(`${this.baseUrl}/plans`, request);
  }

  delete(): Observable<void> {
    const params = new HttpParams().set('confirmed', 'true');
    return this.http.delete<void>(this.baseUrl, { params });
  }
}
