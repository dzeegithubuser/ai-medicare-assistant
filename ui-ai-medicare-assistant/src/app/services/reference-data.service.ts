import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { ReferenceData, MagiTierOption } from '../models/reference-data.model';

@Injectable({ providedIn: 'root' })
export class ReferenceDataService {
  private data = signal<ReferenceData | null>(null);
  private loaded = signal(false);

  readonly referenceData = this.data.asReadonly();
  readonly isLoaded = this.loaded.asReadonly();

  // Convenience selectors
  readonly genders = computed(() => this.data()?.genders ?? []);
  readonly maritalStatuses = computed(() => this.data()?.maritalStatuses ?? []);
  readonly taxFilingStatuses = computed(() => this.data()?.taxFilingStatuses ?? []);
  readonly incomeFilingStatuses = computed(() => this.data()?.incomeFilingStatuses ?? []);
  readonly tobaccoStatuses = computed(() => this.data()?.tobaccoStatuses ?? []);
  readonly disabilityStatuses = computed(() => this.data()?.disabilityStatuses ?? []);
  readonly chronicConditions = computed(() => this.data()?.chronicConditions ?? []);
  readonly usStates = computed(() => this.data()?.usStates ?? []);
  readonly householdSizes = computed(() => this.data()?.householdSizes ?? []);
  readonly medigapDataSources = computed(() => this.data()?.medigapDataSources ?? []);
  readonly medigapPlanTypes = computed(() => this.data()?.medigapPlanTypes ?? []);

  constructor(private http: HttpClient) {}

  /** Loads reference data once — safe to call multiple times. */
  load(): void {
    if (this.loaded()) return;
    this.http.get<ReferenceData>(`${environment.apiUrl}/api/ReferenceData`).subscribe({
      next: (data) => {
        this.data.set(data);
        this.loaded.set(true);
      },
      error: () => console.error('Failed to load reference data')
    });
  }

  /** Returns MAGI tiers for a given tax filing status. */
  getMagiTiersForFiling(filingStatus: string): MagiTierOption[] {
    const tiers = this.data()?.magiTiersByFiling;
    if (!tiers || !filingStatus) return [];
    return tiers[filingStatus] ?? tiers['Single'] ?? [];
  }
}
