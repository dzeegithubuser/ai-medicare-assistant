/** Drug name suggestion types (Step 1 of drug search) */
export interface DrugNameSuggestionResult {
  suggestions: DrugNameSuggestion[];
}

export interface DrugNameSuggestion {
  inputName: string;
  candidates: DrugCandidate[];
}

export interface DrugCandidate {
  name: string;
  type: 'Brand' | 'Generic';
  confidence: number;
}

export interface DrugInteraction {
  drugA: string;
  drugB: string;
  severity: 'High' | 'Moderate' | 'Low';
  description: string;
  clinicalConsequence: string;
  recommendation: string;
}

export interface DuplicateTherapy {
  drugs: string[];
  therapeuticClass: string;
  message: string;
}

// ── Financial Planner Pharmacy Lookup ──

export interface PharmacyLookupEntry {
  pharmacyNumber: string;
  pharmacyName: string;
  latitude: string;
  longitude: string;
  address: string;
  distance: string;
  zipcode: string;
}

export interface PharmacyLookupResponse {
  webServiceTransactionId: string;
  webServiceStatus: string;
  latitude: string;
  longitude: string;
  searchRadiusInMiles: string;
  pharmacyName: string;
  page: number;
  size: number;
  totalPages: number;
  totalPharmacies: number;
  pharmacies: PharmacyLookupEntry[];
}

// ── Financial Planner Drug Search ──

export interface DrugListItem {
  rxcui: string;
  displayName: string;
  prescription: boolean;
}

export interface DrugSearchResponse {
  webServiceTransactionId: string;
  webServiceStatus: string;
  drugName: string;
  drugList: DrugListItem[];
  messages: any[];
}

export interface DrugDetailAdvanceItem {
  drugName: string;
  rxcui: string;
  genericDrugName: string;
  genericRxcui: string;
  newDoseForm: string;
  rxnDoseForm: string;
  strength: string;
  brandName: string;
  prescription: boolean;
  drugType: string;
}

export interface DrugDetailResponse {
  webServiceTransactionId: string;
  webServiceStatus: string;
  rxcui: string;
  drugDetailAdvanceList: DrugDetailAdvanceItem[];
}

export interface DrugSearchResult {
  drugName: string;
  search: DrugSearchResponse;
  matchedDrug: DrugListItem | null;
  detail: DrugDetailResponse | null;
}

export interface BulkDrugSearchResponse {
  results: DrugSearchResult[];
  interactions: DrugInteraction[];
  duplicateTherapies: DuplicateTherapy[];
}
