import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Observable } from 'rxjs';

export interface SavePrescriptionRequest {
  name: string;
  drugs: PrescriptionDrugDto[];
}

export interface PrescriptionDrugDto {
  drugInput: string;
  normalizedDrugName: string;
  genericName: string;
  selectedName: string;
  nameType: string;
  dosageForm: string;
  strength: string;
  packaging: string;
  rxNormId: string;
  ndcCode: string;
  therapeuticCategory: string;
  drugClass: string;
  quantityPerMonth?: number;
}

/** Saved with current prescription (FP pharmacy lookup + plan picks). */
export interface SelectedPharmacySnapshotDto {
  pharmacyNumber: string;
  pharmacyName: string;
  address: string;
  distance: string;
  zipcode: string;
}

export interface SelectedPlanSnapshotDto {
  slot: 'partD' | 'medigap' | 'ma' | 'maGapPartD';
  planId: string;
  planName: string;
  contractId: string;
  medigapKey?: string;
  medigapPlanType?: string;
  companyName?: string;
}

export interface SaveCurrentPrescriptionRequest {
  drugs: PrescriptionDrugDto[];
  selectedPharmacies: SelectedPharmacySnapshotDto[];
  selectedPlans: SelectedPlanSnapshotDto[];
  /** FP plans UI section when selections were saved */
  activeSection?: 'partd' | 'ma' | null;
}

export interface PrescriptionResponse {
  id: string;
  name: string;
  createdDate: string;
  drugs: PrescriptionDrugDto[];
  selectedPharmacies?: SelectedPharmacySnapshotDto[];
  selectedPlans?: SelectedPlanSnapshotDto[];
  activeSection?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PrescriptionService {
  constructor(private http: HttpClient) {}

  save(request: SavePrescriptionRequest): Observable<PrescriptionResponse> {
    return this.http.post<PrescriptionResponse>(`${environment.apiUrl}/api/prescription`, request);
  }

  /** Upserts MongoDB current prescriptions and links the document id on the MySQL profile. */
  saveCurrent(request: SaveCurrentPrescriptionRequest): Observable<PrescriptionResponse> {
    return this.http.post<PrescriptionResponse>(`${environment.apiUrl}/api/prescription/current`, request);
  }

  /** Replaces only the drugs section. Pharmacies and plans are untouched. */
  saveCurrentDrugs(drugs: PrescriptionDrugDto[]): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/api/prescription/current/drugs`, { drugs });
  }

  /** Replaces only the pharmacies section. Drugs and plans are untouched. */
  saveCurrentPharmacy(selectedPharmacies: SelectedPharmacySnapshotDto[]): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/api/prescription/current/pharmacy`, { selectedPharmacies });
  }

  /** Replaces only the plans section and activeSection. Drugs and pharmacies are untouched. */
  saveCurrentPlans(selectedPlans: SelectedPlanSnapshotDto[], activeSection?: 'partd' | 'ma' | null): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/api/prescription/current/plans`, { selectedPlans, activeSection });
  }

  getAll(): Observable<PrescriptionResponse[]> {
    return this.http.get<PrescriptionResponse[]>(`${environment.apiUrl}/api/prescription`);
  }

  getById(id: string): Observable<PrescriptionResponse> {
    return this.http.get<PrescriptionResponse>(`${environment.apiUrl}/api/prescription/${encodeURIComponent(id)}`);
  }
}
