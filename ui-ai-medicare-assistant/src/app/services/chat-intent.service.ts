import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ChatIntentParams {
  firstName?: string | null;
  lastName?: string | null;
  analysisName?: string | null;
  prescriptionName?: string | null;
  gender?: string | null;
  dateOfBirth?: string | null;
  tobaccoStatus?: number | null;
  healthCondition?: number | null;
  taxFilingStatus?: string | null;
  coverageYear?: number | null;
  zipCode?: string | null;
  addressLine1?: string | null;
  lifeExpectancy?: number | null;
  // LTC care-type params
  ltcHealthProfile?: number | null;
  ltcAdultDayYears?: number | null;
  ltcHomeCareYears?: number | null;
  ltcNursingCareYears?: number | null;
}

export interface ChatIntentResponse {
  intent: ChatIntent;
  params?: ChatIntentParams;
  confirmationMessage: string;
}

export type ChatIntent =
  | 'NAVIGATE_PROFILE'
  | 'NAVIGATE_ANALYSIS_DRUGS'
  | 'NAVIGATE_PHARMACIES'
  | 'NAVIGATE_PLANS'
  | 'NAVIGATE_COST_PROJECTIONS'
  | 'SWITCH_TO_PDP'
  | 'SWITCH_TO_MA'
  | 'ACTION_RESET_ANALYSIS'
  | 'ACTION_SIGN_OUT'
  | 'ACTION_LOAD_PRESCRIPTIONS'
  | 'ACTION_HELP'
  | 'ACTION_RUN_ANALYSIS'
  | 'ACTION_SAVE_ANALYSIS'
  | 'NAVIGATE_SAVED_ANALYSES'
  | 'DRUG_INPUT'
  // LTC intents
  | 'NAVIGATE_LTC_CARE_TYPE'
  | 'LTC_CARE_INPUT'
  | 'ACTION_RUN_LTC_PROJECTION'
  | 'UNKNOWN';

@Injectable({ providedIn: 'root' })
export class ChatIntentService {
  constructor(private http: HttpClient) {}

  classify(message: string, isProfileComplete: boolean, currentPage: string): Observable<ChatIntentResponse> {
    return this.http.post<ChatIntentResponse>(`${environment.apiUrl}/api/chat/intent`, {
      message,
      isProfileComplete,
      currentPage,
    });
  }
}
