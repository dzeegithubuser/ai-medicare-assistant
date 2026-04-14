import { DrugStateService } from '../services/drug-state.service';
import {
  PrescriptionDrugDto,
  SaveCurrentPrescriptionRequest,
  SelectedPharmacySnapshotDto,
  SelectedPlanSnapshotDto,
} from '../services/prescription.service';
import { DrugDetailAdvanceItem } from '../models/drug.model';

/**
 * Builds the payload for POST /api/prescription/current from FP Drugs session state
 * (confirmed drugs + formulation selections in sessionStorage).
 *
 * Falls back to the first available formulation from drugDetails when a drug has no
 * explicit formulation selection in session — ensures the drugs array is never empty
 * when confirmed drugs exist, avoiding backend 400 validation errors.
 */
export function buildCurrentPrescriptionDrugsFromState(state: DrugStateService): PrescriptionDrugDto[] {
  const details = state.drugDetails();
  const confirmed = state.confirmedDrugNames();
  if (!confirmed.size) return [];

  let formulationEntries: [string, DrugDetailAdvanceItem][] = [];
  try {
    const raw = sessionStorage.getItem('formulation-selections');
    if (raw) formulationEntries = JSON.parse(raw);
  } catch { /* corrupt session — proceed with empty map, fallback will cover */ }
  const formulationMap = new Map(formulationEntries);

  // Build a fallback map: drug name → first available formulation from the API results.
  // Only include results whose drug name is in the confirmed set to avoid stale session
  // formulations for drugs that were removed from confirmed (e.g. a drug resolved by the
  // API under a different name, or one removed in a previous session but still in session storage).
  const detailFallbackMap = new Map<string, DrugDetailAdvanceItem>();
  for (const result of details?.results ?? []) {
    if (!confirmed.has(result.drugName)) continue;
    const first = result.detail?.drugDetailAdvanceList?.[0];
    if (first) detailFallbackMap.set(result.drugName, first);
  }

  let qtyEntries: [string, number][] = [];
  try {
    const qtyRaw = sessionStorage.getItem('drug-quantities');
    if (qtyRaw) qtyEntries = JSON.parse(qtyRaw);
  } catch { /* ignore */ }
  const qtyMap = new Map(qtyEntries);

  const drugs: PrescriptionDrugDto[] = [];

  for (const drugName of confirmed) {
    // Prefer explicit formulation selection; fall back to first detail item.
    const f = formulationMap.get(drugName) ?? detailFallbackMap.get(drugName);
    if (!f) {
      // Last resort: include drug with minimal data so the array is never empty.
      drugs.push({
        drugInput: drugName,
        normalizedDrugName: drugName,
        genericName: '',
        selectedName: drugName,
        nameType: '',
        dosageForm: '',
        strength: '',
        packaging: '',
        rxNormId: '',
        ndcCode: '',
        therapeuticCategory: '',
        drugClass: '',
        quantityPerMonth: qtyMap.get(drugName) ?? undefined,
      });
      continue;
    }
    const dosageForm = f.rxnDoseForm || f.newDoseForm || '';
    drugs.push({
      drugInput: drugName,
      normalizedDrugName: f.drugName || drugName,
      genericName: f.genericDrugName ?? '',
      selectedName: (f.brandName || f.drugName || drugName).trim(),
      nameType: f.drugType ?? '',
      dosageForm,
      strength: f.strength ?? '',
      packaging: '',
      rxNormId: f.rxcui ?? '',
      ndcCode: '',
      therapeuticCategory: '',
      drugClass: '',
      quantityPerMonth: qtyMap.get(drugName) ?? undefined,
    });
  }

  return drugs;
}

export function buildSelectedPharmaciesSnapshotFromState(state: DrugStateService): SelectedPharmacySnapshotDto[] {
  return state.selectedLookupPharmacies().map((p) => ({
    pharmacyNumber: String(p.pharmacyNumber ?? ''),
    pharmacyName: p.pharmacyName ?? '',
    address: p.address ?? '',
    distance: String(p.distance ?? ''),
    zipcode: String(p.zipcode ?? ''),
  }));
}

export function buildSelectedPlansSnapshotFromState(state: DrugStateService): SelectedPlanSnapshotDto[] {
  const section = state.activeSection();
  const plans: SelectedPlanSnapshotDto[] = [];

  if (section === 'partd') {
    const partD = state.selectedPartDPlan();
    if (partD) {
      plans.push({
        slot: 'partD',
        planId: partD.planId ?? '',
        planName: partD.planName ?? '',
        contractId: partD.contractId ?? '',
      });
    }
    const medigap = state.selectedMedigapPlan();
    if (medigap) {
      plans.push({
        slot: 'medigap',
        planId: medigap.key ?? '',
        planName: medigap.company_base?.name ?? 'Medigap',
        contractId: '',
        medigapKey: medigap.key,
        medigapPlanType: medigap.plan,
        companyName: medigap.company_base?.name,
      });
    }
  } else if (section === 'ma') {
    const ma = state.selectedMAPlan();
    if (ma) {
      plans.push({
        slot: 'ma',
        planId: ma.planId ?? '',
        planName: ma.planName ?? '',
        contractId: ma.contractId ?? '',
      });
    }
    const gap = state.selectedMAGapPartDPlan();
    if (gap) {
      plans.push({
        slot: 'maGapPartD',
        planId: gap.planId ?? '',
        planName: gap.planName ?? '',
        contractId: gap.contractId ?? '',
      });
    }
  }

  return plans;
}

/** Full payload for POST /api/prescription/current — drugs, selected pharmacies, and selected FP plans. */
export function buildCurrentPrescriptionSnapshotFromState(state: DrugStateService): SaveCurrentPrescriptionRequest {
  return {
    drugs: buildCurrentPrescriptionDrugsFromState(state),
    selectedPharmacies: buildSelectedPharmaciesSnapshotFromState(state),
    selectedPlans: buildSelectedPlansSnapshotFromState(state),
    activeSection: state.activeSection(),
  };
}
