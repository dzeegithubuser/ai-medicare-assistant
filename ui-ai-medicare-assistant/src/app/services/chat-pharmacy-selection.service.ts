import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AvailablePharmacySummary {
  name: string;
  address: string;
  distance: string;
  zipcode: string;
}

export interface SelectedPharmacySummary {
  name: string;
  pharmacyNumber: string;
}

export interface PharmacySelectionExtractRequest {
  message: string;
  availablePharmacies: AvailablePharmacySummary[];
  selectedPharmacies: SelectedPharmacySummary[];
}

export interface PharmacySelectionExtractResponse {
  pharmacyName: string | null;
  /** AI-resolved full names for multi-select / multi-remove */
  pharmacyNames?: string[] | null;
  action: 'select' | 'remove' | 'list' | 'search' | 'clearFilter';
  searchTerm: string | null;
  reply: string;
}

@Injectable({ providedIn: 'root' })
export class ChatPharmacySelectionService {
  constructor(private http: HttpClient) {}

  extractSelection(request: PharmacySelectionExtractRequest): Observable<PharmacySelectionExtractResponse> {
    return this.http.post<PharmacySelectionExtractResponse>(
      `${environment.apiUrl}/api/chat/extract-pharmacy-selection`,
      request
    );
  }
}
