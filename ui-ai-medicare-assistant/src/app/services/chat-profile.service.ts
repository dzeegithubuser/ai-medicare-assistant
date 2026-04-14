import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ProfileExtractRequest {
  message: string;
  missingFields: string[];
}

export interface ProfileExtractResponse {
  extractedFields: Record<string, unknown>;
  reply: string;
}

@Injectable({ providedIn: 'root' })
export class ChatProfileService {
  constructor(private http: HttpClient) {}

  extractProfile(request: ProfileExtractRequest): Observable<ProfileExtractResponse> {
    return this.http.post<ProfileExtractResponse>(
      `${environment.apiUrl}/api/chat/extract-profile`,
      request
    );
  }
}
