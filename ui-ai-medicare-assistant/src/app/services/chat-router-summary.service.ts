import { Injectable, inject } from '@angular/core';
import { DrugStateService } from './drug-state.service';
import { AvailableDrugSummary, PendingDrugChatCards } from './chat-drug-selection.service';
import {
  AvailableMedigapSummary,
  AvailablePlanSummary,
  SelectedPlansSummary,
} from './chat-plan-selection.service';
import {
  AvailablePharmacySummary,
  SelectedPharmacySummary,
} from './chat-pharmacy-selection.service';

@Injectable({ providedIn: 'root' })
export class ChatRouterSummaryService {
  private state = inject(DrugStateService);

  resolveSummaryForDrug(
    drugName: string | null,
    summaries: AvailableDrugSummary[]
  ): AvailableDrugSummary | null {
    if (!drugName) return null;
    const lower = drugName.toLowerCase();
    return (
      summaries.find(
        d =>
          d.name.toLowerCase() === lower ||
          d.name.toLowerCase().includes(lower) ||
          lower.includes(d.name.toLowerCase())
      ) ?? null
    );
  }

  computeDrugSelectionCards(
    summary: AvailableDrugSummary,
    partialIn: {
      type: string | null;
      dosageForm: string | null;
      strength: string | null;
      quantity: number | null;
    }
  ): PendingDrugChatCards | null {
    const types = summary.types;
    if (!types.length) return null;

    const partial = { ...partialIn };
    let effType = this.normalizeTypeForFp(partial.type, types);
    if (!effType && types.length === 1) effType = types[0];
    partial.type = effType ?? partial.type;

    if (!effType && types.length > 1) {
      return {
        drugName: summary.name,
        partial,
        typeOptions: [...types].sort((a, b) => a.localeCompare(b)),
      };
    }
    if (!effType) return null;

    const forms = summary.dosageForms[effType] ?? [];
    let effForm = this.fuzzyMatchStringOption(partial.dosageForm, forms);
    if (!effForm && forms.length === 1) effForm = forms[0];
    partial.dosageForm = effForm ?? partial.dosageForm;

    if (!effForm && forms.length > 1) {
      return {
        drugName: summary.name,
        partial,
        formOptions: [...forms].sort((a, b) => a.localeCompare(b)),
      };
    }
    if (!effForm) return null;

    const strKey = `${effType}|${effForm}`;
    const strengths = summary.strengths[strKey] ?? [];
    let effStr = this.fuzzyMatchStrengthOption(partial.strength, strengths);
    if (!effStr && strengths.length === 1) effStr = strengths[0];
    partial.strength = effStr ?? partial.strength;

    if (!effStr && strengths.length > 1) {
      return {
        drugName: summary.name,
        partial,
        strengthOptions: [...strengths].sort((a, b) => a.localeCompare(b)),
      };
    }
    if (!effStr) return null;

    if (!partial.quantity || partial.quantity <= 0) {
      return {
        drugName: summary.name,
        partial,
        quantityOptions: [30, 60, 90, 120],
      };
    }

    return null;
  }

  buildAvailableDrugSummaries(): AvailableDrugSummary[] {
    const details = this.state.drugDetails();
    if (!details?.results) return [];

    return details.results
      .filter(r => r.detail?.drugDetailAdvanceList?.length)
      .map(r => {
        const formulations = r.detail!.drugDetailAdvanceList;
        const types = [...new Set(formulations.map(f => f.drugType).filter(Boolean))];
        const dosageForms: Record<string, string[]> = {};
        const strengths: Record<string, string[]> = {};

        for (const type of types) {
          const forType = formulations.filter(f => f.drugType === type);
          const forms = [...new Set(forType.map(f => f.rxnDoseForm || f.newDoseForm).filter(Boolean))];
          dosageForms[type] = forms;

          for (const form of forms) {
            const forForm = forType.filter(f => f.rxnDoseForm === form || f.newDoseForm === form);
            const strs = [...new Set(forForm.map(f => f.strength).filter(Boolean))];
            strengths[`${type}|${form}`] = strs;
          }
        }

        return { name: r.drugName, types, dosageForms, strengths };
      });
  }

  buildPharmacySummaries(): { available: AvailablePharmacySummary[]; selected: SelectedPharmacySummary[] } {
    const lookup = this.state.pharmacyLookup();
    const str = (v: string | number | null | undefined): string =>
      v === null || v === undefined ? '' : String(v);
    const available: AvailablePharmacySummary[] = (lookup?.pharmacies ?? []).map(p => ({
      name: str(p.pharmacyName),
      address: str(p.address),
      distance: str(p.distance as string | number),
      zipcode: str(p.zipcode as string | number),
    }));
    const selected: SelectedPharmacySummary[] = this.state.selectedLookupPharmacies().map(p => ({
      name: str(p.pharmacyName),
      pharmacyNumber: str(p.pharmacyNumber as string | number),
    }));
    return { available, selected };
  }

  buildPlanSummaries(): {
    partDPlans: AvailablePlanSummary[];
    medigapPlans: AvailableMedigapSummary[];
    maPlans: AvailablePlanSummary[];
    selectedPlans: SelectedPlansSummary;
  } {
    const partDData = this.state.partDPlans();
    const partDPlans: AvailablePlanSummary[] = (partDData?.recommendationList ?? []).map(p => {
      const rec = p.pharmacyWiseRecommendations?.[0];
      return {
        planName: p.planName,
        contractId: p.contractId,
        planId: p.planId,
        premium: rec?.totalPremiumToPay ?? 0,
        starRating: rec?.starRating ?? 0,
      };
    });

    const medigapData = this.state.medigapQuotes();
    const medigapPlans: AvailableMedigapSummary[] = (medigapData?.planList ?? []).map(p => ({
      company: p.company_base?.name ?? 'Unknown',
      plan: p.plan,
      monthlyPremium: p.rate?.month ?? 0,
    }));

    const maData = this.state.maPlans();
    const maPlans: AvailablePlanSummary[] = (maData?.recommendationList ?? []).map(p => {
      const rec = p.pharmacyWiseRecommendations?.[0];
      return {
        planName: p.planName,
        contractId: p.contractId,
        planId: p.planId,
        premium: rec?.premium ?? 0,
        starRating: rec?.starRating ?? 0,
      };
    });

    const selectedPlans: SelectedPlansSummary = {
      partD: this.state.selectedPartDPlan()?.planName ?? null,
      medigap: this.state.selectedMedigapPlan()?.company_base?.name ?? null,
      ma: this.state.selectedMAPlan()?.planName ?? null,
      maGapPartD: this.state.selectedMAGapPartDPlan()?.planName ?? null,
    };

    return { partDPlans, medigapPlans, maPlans, selectedPlans };
  }

  private normalizeTypeForFp(type: string | null, types: string[]): string | null {
    if (!type) return null;
    const t = type.trim().toLowerCase();
    if (t === 'brand' || t === 'branded' || t === 'brand name') {
      const m = types.find(x => /brand/i.test(x));
      return m ?? null;
    }
    if (t === 'generic') {
      const m = types.find(x => /generic/i.test(x));
      return m ?? null;
    }
    const exact = types.find(x => x.toLowerCase() === t);
    if (exact) return exact;
    return types.find(x => x.toLowerCase().includes(t) || t.includes(x.toLowerCase())) ?? null;
  }

  private fuzzyMatchStringOption(hint: string | null, options: string[]): string | null {
    if (!hint || !options.length) return null;
    const lower = hint.toLowerCase().trim();
    const exact = options.find(o => o.toLowerCase() === lower);
    if (exact) return exact;
    return options.find(o => o.toLowerCase().includes(lower) || lower.includes(o.toLowerCase())) ?? null;
  }

  private fuzzyMatchStrengthOption(hint: string | null, strengths: string[]): string | null {
    if (!hint || !strengths.length) return null;
    const normalized = hint.replace(/\s+/g, ' ').trim().toUpperCase();
    const exact = strengths.find(s => s.toUpperCase() === normalized);
    if (exact) return exact;
    const stripped = normalized.replace(/\s/g, '');
    return strengths.find(s => s.replace(/\s/g, '').toUpperCase() === stripped) ?? null;
  }
}
