import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AvailableDrugSummary {
  name: string;
  types: string[];
  dosageForms: Record<string, string[]>;
  strengths: Record<string, string[]>;
}

export interface DrugSelectionExtractRequest {
  message: string;
  availableDrugs: AvailableDrugSummary[];
}

export interface DrugSelectionExtractResponse {
  drugName: string | null;
  type: string | null;
  dosageForm: string | null;
  strength: string | null;
  quantity: number | null;
  action: 'select' | 'options' | 'confirm_all' | 'remove' | 'edit';
  reply: string;
}

/** In-chat chip rows for drug formulation selection (fp-drugs step). */
export interface PendingDrugChatCards {
  drugName: string;
  partial: {
    type: string | null;
    dosageForm: string | null;
    strength: string | null;
    quantity: number | null;
  };
  typeOptions?: string[];
  formOptions?: string[];
  strengthOptions?: string[];
  quantityOptions?: number[];
}

@Injectable({ providedIn: 'root' })
export class ChatDrugSelectionService {
  constructor(private http: HttpClient) {}

  extractSelection(request: DrugSelectionExtractRequest): Observable<DrugSelectionExtractResponse> {
    return this.http.post<DrugSelectionExtractResponse>(
      `${environment.apiUrl}/api/chat/extract-drug-selection`,
      request
    );
  }
}
