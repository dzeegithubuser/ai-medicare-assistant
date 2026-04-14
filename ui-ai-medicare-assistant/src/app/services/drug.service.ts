
import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { DrugNameSuggestionResult, BulkDrugSearchResponse, PharmacyLookupResponse } from '../models/drug.model';

@Injectable({ providedIn: 'root' })
export class DrugService {

  constructor(private http: HttpClient) { }

  suggestNames(input: string) {
    return this.http.post<DrugNameSuggestionResult>(`${environment.apiUrl}/api/drug/suggest-names`, {
      input
    });
  }

  /** Pharmacy lookup via Financial Planner API — paginated, filterable */
  lookupPharmacies(params: { page?: number; size?: number; radius?: string; name?: string } = {}) {
    const queryParams: Record<string, string> = {};
    if (params.page) queryParams['page'] = params.page.toString();
    if (params.size) queryParams['size'] = params.size.toString();
    if (params.radius) queryParams['radius'] = params.radius;
    if (params.name) queryParams['name'] = params.name;
    return this.http.get<PharmacyLookupResponse>(
      `${environment.apiUrl}/api/pharmacy/lookup`,
      { params: queryParams }
    );
  }

  /** Financial Planner bulk drug search — searches all drugs, matches, fetches details + AI interactions */
  searchDrugsBulk(drugNames: string[]) {
    return this.http.post<BulkDrugSearchResponse>(
      `${environment.apiUrl}/api/FinancialPlannerDrug/search-bulk`,
      { drugNames }
    );
  }

}
