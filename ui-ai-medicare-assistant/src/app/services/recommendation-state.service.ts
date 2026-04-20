import { Injectable, signal, computed, inject } from '@angular/core';
import { RecommendationResponse, UpdateDrugsRequest, UpdatePharmacyRequest } from '../models/recommendation.model';
import { RecommendationService } from './recommendation.service';
import { DrugStateService } from './drug-state.service';
import { buildCurrentPrescriptionDrugsFromState } from '../medicare-analysis/current-prescription.mapper';
import { Observable, of, tap, catchError, map } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class RecommendationStateService {
  readonly activeRecommendation = signal<RecommendationResponse | null>(null);
  readonly hasRecommendation = computed(() => this.activeRecommendation() !== null);
  readonly isLoading = signal(false);

  private readonly recommendationService = inject(RecommendationService);
  private readonly state = inject(DrugStateService);

  loadActiveRecommendation$(): Observable<RecommendationResponse> {
    this.isLoading.set(true);
    return this.recommendationService.getActive().pipe(
      tap({
        next: rec => {
          this.activeRecommendation.set(rec);
          this.isLoading.set(false);
        },
        error: () => {
          this.activeRecommendation.set(null);
          this.isLoading.set(false);
        }
      })
    );
  }

  loadActiveRecommendation(): void {
    this.loadActiveRecommendation$().subscribe({ error: () => {} });
  }

  refreshAfterUpdate(): void {
    this.recommendationService.getActive().subscribe({
      next: rec => this.activeRecommendation.set(rec),
      error: () => this.activeRecommendation.set(null)
    });
  }

  clear(): void {
    this.activeRecommendation.set(null);
  }

  /**
   * Persists the currently selected lookup pharmacy to the active recommendation in MongoDB.
   * Call this when the user explicitly advances from the Pharmacies step to Plans.
   * Returns an Observable<void> so callers can chain or fire-and-forget.
   */
  savePharmacySelection(): Observable<void> {
    if (!this.activeRecommendation()) return of(void 0);

    const pharmacies = this.state.selectedLookupPharmacies();
    const entry = pharmacies[0] ?? null;

    const request: UpdatePharmacyRequest = {
      pharmacy: entry
        ? {
            npi: entry.pharmacyNumber,
            name: entry.pharmacyName,
            address: entry.address,
            city: '',
            state: '',
            zipCode: entry.zipcode,
            phone: '',
            pharmacyType: '',
            distance: entry.distance ? parseFloat(entry.distance) || null : null,
          }
        : null,
      mailOrderPharmacy: null,
    };

    return this.recommendationService.updatePharmacy(request).pipe(
      tap(rec => this.activeRecommendation.set(rec)),
      map(() => void 0),
      catchError(() => of(void 0)),
    );
  }

  /**
   * Persists the currently confirmed drugs to recommendations.drugList in MongoDB.
   * Call this when the user advances from Drugs → Pharmacies so that future
   * "use stored drugs?" prompts reflect the actual current confirmed set — not
   * the stale list from when the recommendation was first created.
   */
  syncDrugsToRecommendation(): Observable<void> {
    if (!this.activeRecommendation()) return of(void 0);

    const prescriptionDrugs = buildCurrentPrescriptionDrugsFromState(this.state);
    if (prescriptionDrugs.length === 0) return of(void 0);

    const request: UpdateDrugsRequest = {
      drugs: prescriptionDrugs.map(p => ({
        drugName: p.drugInput,
        fullName: null,
        drugType: null,
        dosage: [p.dosageForm, p.strength].filter(Boolean).join(' '),
        quantity: p.quantityPerMonth ?? 30,
        refillFrequency: 'monthly',
        rxcui: p.rxNormId || null,
        ndcCode: p.ndcCode || null,
      })),
    };

    return this.recommendationService.updateDrugs(request).pipe(
      tap(rec => this.activeRecommendation.set(rec)),
      map(() => void 0),
      catchError(() => of(void 0)),
    );
  }
}
