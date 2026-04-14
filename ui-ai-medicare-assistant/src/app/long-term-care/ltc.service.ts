import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  LtcProjectionRequest,
  LtcProjectionResult,
  SaveLtcCurrentRequest,
  LtcCurrentResponse,
} from '../models/ltc.model';

@Injectable({ providedIn: 'root' })
export class LtcService {
  constructor(private http: HttpClient) {}

  getProjection(request: LtcProjectionRequest): Observable<LtcProjectionResult> {
    return this.http.post<LtcProjectionResult>(
      `${environment.apiUrl}/api/long-term-care`,
      request,
    );
  }

  saveCurrent(request: SaveLtcCurrentRequest): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/api/ltc/current`, request);
  }

  getCurrent(): Observable<LtcCurrentResponse> {
    return this.http.get<LtcCurrentResponse>(`${environment.apiUrl}/api/ltc/current`);
  }
}
