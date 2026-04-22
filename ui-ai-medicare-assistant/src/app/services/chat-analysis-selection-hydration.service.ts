import { Injectable, inject } from '@angular/core';
import { finalize, map, Observable, of, shareReplay, switchMap, tap, catchError } from 'rxjs';
import { MedicareStateService } from './drug-state.service';
import { DrugService } from './drug.service';
import { ProfileService } from './profile.service';
import {
  PrescriptionService,
  PrescriptionResponse,
  PrescriptionDrugDto,
  SelectedPlanSnapshotDto,
} from './prescription.service';
import { PharmacyLookupEntry } from '../models/drug.model';
import { DRUG_MESSAGES, PHARMACY_MESSAGES, PLAN_MESSAGES } from '../constants/chat-messages';

/**
 * Restores analysis step selections (drugs/pharmacy/plans) from active saved recommendation.
 * Extracted from ChatRouterService to keep routing logic focused and maintainable.
 */
@Injectable({ providedIn: 'root' })
export class ChatAnalysisSelectionHydrationService {
  private state = inject(MedicareStateService);
  private drugService = inject(DrugService);
  private profileService = inject(ProfileService);
  private prescriptionService = inject(PrescriptionService);

  /** Avoid duplicate identical plan-restore chat lines when hydration runs more than once (e.g. double navigation). */
  private lastPlanListRestoreChatFingerprint: string | null = null;
  private lastSavedPlansPendingChatFingerprint: string | null = null;
  private cachedSelectionDocId: string | null = null;
  private cachedSelections: PrescriptionResponse | null = null;
  private inFlightSelections$: Observable<PrescriptionResponse | null> | null = null;

  /** Returns the cached saved drug list from userAnalysisSelections (available after drugs hydration). */
  getSavedDrugs$(): Observable<PrescriptionDrugDto[]> {
    return this.getCurrentSelections$().pipe(
      map((selections) => selections?.drugs ?? []),
    );
  }

  private getCurrentSelections$(): Observable<PrescriptionResponse | null> {
    const docId = this.profileService.profile()?.currentPrescriptionDocumentId ?? null;
    if (!docId) {
      this.cachedSelectionDocId = null;
      this.cachedSelections = null;
      this.inFlightSelections$ = null;
      return of(null);
    }
    if (this.cachedSelectionDocId === docId && this.cachedSelections) {
      return of(this.cachedSelections);
    }
    if (this.cachedSelectionDocId === docId && this.inFlightSelections$) {
      return this.inFlightSelections$;
    }
    this.cachedSelectionDocId = docId;
    this.inFlightSelections$ = this.prescriptionService.getById(docId).pipe(
      map((res) => res ?? null),
      tap((res) => {
        this.cachedSelections = res;
      }),
      catchError(() => of(null)),
      finalize(() => {
        this.inFlightSelections$ = null;
      }),
      shareReplay(1),
    );
    return this.inFlightSelections$;
  }

  hydrateDrugsFromActiveRecommendationSelection(silent = false): void {
    this.hydrateDrugsFromActiveRecommendationSelection$({ silent }).subscribe({ error: () => {} });
  }

  hydrateDrugsFromActiveRecommendationSelection$(options?: { silent?: boolean }): Observable<void> {
    const silent = !!options?.silent;
    return this.getCurrentSelections$().pipe(
      switchMap((selections) => {
        const selected = selections?.drugs ?? [];
        const names = Array.from(
          new Set(
            selected
              .map((d) => (d.drugInput || d.selectedName || d.normalizedDrugName || '').trim())
              .filter(Boolean),
          ),
        );
        if (names.length === 0) return of(void 0);

        this.state.confirmedDrugNames.set(new Set(names));
        try {
          sessionStorage.setItem(MedicareStateService.FP_CONFIRMED_DRUGS_SESSION_KEY, JSON.stringify(names));
        } catch {
          // non-blocking: in-memory selection is already updated
        }

        this.state.setDrugDetailsLoading(true);
        return this.drugService.searchDrugsBulk(names).pipe(
          map((response) => {
            this.state.setDrugDetails(response);
            const resolvedNames = (response.results ?? []).map((r) => r.drugName).filter(Boolean);
            if (resolvedNames.length > 0 && resolvedNames.length !== names.length) {
              this.state.confirmedDrugNames.set(new Set(resolvedNames));
              try {
                sessionStorage.setItem(
                  MedicareStateService.FP_CONFIRMED_DRUGS_SESSION_KEY,
                  JSON.stringify(resolvedNames),
                );
              } catch {
                // non-blocking
              }
            }
            if (!silent) {
              const count = resolvedNames.length || names.length;
              this.state.addAssistantMessage(DRUG_MESSAGES.RESTORED_FROM_SAVED_ANALYSIS(count));
            }
          }),
          finalize(() => this.state.setDrugDetailsLoading(false)),
        );
      }),
    );
  }

  hydratePharmacyFromActiveRecommendationSelection(silent = false): void {
    this.getCurrentSelections$().subscribe({
      next: (selections) => {
        const pharmacies = selections?.selectedPharmacies ?? [];
        if (pharmacies.length === 0) return;
        const restored: PharmacyLookupEntry[] = pharmacies.map((p) => ({
          pharmacyNumber: p.pharmacyNumber || p.pharmacyName,
          pharmacyName: p.pharmacyName,
          latitude: '0',
          longitude: '0',
          address: p.address,
          distance: p.distance ?? '0',
          zipcode: p.zipcode,
        }));
        this.state.selectedLookupPharmacies.set(restored);
        if (!silent) {
          const names = restored.map((r) => r.pharmacyName).join(', ');
          this.state.addAssistantMessage(PHARMACY_MESSAGES.RESTORED_FROM_SAVED_ANALYSIS(names));
        }
      },
      error: () => {},
    });
  }

  hydratePlansFromActiveRecommendationSelection(silent = false): void {
    this.getCurrentSelections$().subscribe({
      next: (selections) => {
        const selected = selections?.selectedPlans ?? [];
        if (selected.length === 0) return;

        const restoredLines: string[] = [];
        const pendingLines: string[] = [];

        for (const saved of selected) {
          const slot = (saved.slot ?? '').toLowerCase();

          if (slot === 'partd' || slot === 'magappartd') {
            const partD = (this.state.partDPlans()?.recommendationList ?? []).find(
              (p) => p.planId === saved.planId || p.planName.toLowerCase() === (saved.planName ?? '').toLowerCase(),
            );
            if (partD) {
              if (slot === 'magappartd') {
                this.state.selectMAGapPartDPlan(partD);
                restoredLines.push(`• **Part D (gap):** ${partD.planName}`);
              } else {
                this.state.selectPartDPlan(partD);
                restoredLines.push(`• **Part D:** ${partD.planName}`);
              }
            } else {
              pendingLines.push(`• **${slot === 'magappartd' ? 'Part D (gap)' : 'Part D'}:** ${saved.planName?.trim() || 'Unknown plan'}`);
            }
            continue;
          }

          if (slot === 'medigap') {
            const medigap = (this.state.medigapQuotes()?.planList ?? []).find(
              (p) =>
                (saved.medigapPlanType && p.plan === saved.medigapPlanType) ||
                ((p.company_base?.name ?? '').toLowerCase() === (saved.companyName ?? '').toLowerCase()),
            );
            if (medigap) {
              this.state.selectMedigapPlan(medigap);
              const label = `${medigap.company_base?.name ?? 'Carrier'} — Plan ${medigap.plan}`;
              restoredLines.push(`• **Medigap:** ${label}`);
            } else {
              const label = [saved.companyName, saved.medigapPlanType ? `Plan ${saved.medigapPlanType}` : null]
                .filter(Boolean)
                .join(', ') || saved.planName?.trim() || 'Unknown plan';
              pendingLines.push(`• **Medigap:** ${label}`);
            }
            continue;
          }

          if (slot === 'ma') {
            const ma = (this.state.maPlans()?.recommendationList ?? []).find(
              (p) => p.planId === saved.planId || p.planName.toLowerCase() === (saved.planName ?? '').toLowerCase(),
            );
            if (ma) {
              this.state.selectMAPlan(ma);
              restoredLines.push(`• **Medicare Advantage:** ${ma.planName}`);
            } else {
              pendingLines.push(`• **Medicare Advantage:** ${saved.planName?.trim() || 'Unknown plan'}`);
            }
          }
        }

        if (restoredLines.length === 0) {
          this.hydratePlansFallbackFromSavedSelection(selected);
          return;
        }
        if (silent) return;

        const restoreMessage =
          pendingLines.length === 0
            ? PLAN_MESSAGES.RESTORE_ALL_MATCHED(restoredLines.join('\n'))
            : PLAN_MESSAGES.RESTORE_PARTIAL_DETAIL(restoredLines.join('\n'), pendingLines.join('\n'));

        const fingerprint = `${restoreMessage.length}:${restoredLines.join('|')}::${pendingLines.join('|')}`;
        if (this.lastPlanListRestoreChatFingerprint === fingerprint) {
          return;
        }
        this.lastPlanListRestoreChatFingerprint = fingerprint;
        this.state.addAssistantMessage(restoreMessage);
      },
      error: () => {},
    });
  }

  hydrateAllFromActiveRecommendationSelectionForBootstrap$(): Observable<void> {
    this.hydratePharmacyFromActiveRecommendationSelection(true);
    this.hydratePlansFromActiveRecommendationSelection(true);
    // Drugs are intentionally not auto-applied at bootstrap.
    // Chat asks user consent on first arrival to /analysis/fp-drugs.
    return of(void 0);
  }

  private hydratePlansFallbackFromSavedSelection(selected: SelectedPlanSnapshotDto[]): void {
    const partD = selected.find((s) => (s.slot ?? '').toLowerCase() === 'partd');
    const medigap = selected.find((s) => (s.slot ?? '').toLowerCase() === 'medigap');
    const ma = selected.find((s) => (s.slot ?? '').toLowerCase() === 'ma');
    const maGapPartD = selected.find((s) => (s.slot ?? '').toLowerCase() === 'magappartd');

    if (ma) {
      this.state.setActiveSection('ma');
      this.state.selectMAPlan(this.toRecommendationListItemFromSnapshot(ma));
      if (maGapPartD) {
        this.state.selectMAGapPartDPlan(this.toRecommendationListItemFromSnapshot(maGapPartD));
      }
      this.postSavedPlansPendingMessageOnce(selected);
      return;
    }

    if (partD || medigap) {
      this.state.setActiveSection('partd');
      if (partD) this.state.selectPartDPlan(this.toRecommendationListItemFromSnapshot(partD));
      if (medigap) this.state.selectMedigapPlan(this.toMedigapPlanFromSnapshot(medigap));
      this.postSavedPlansPendingMessageOnce(selected);
    }
  }

  private postSavedPlansPendingMessageOnce(selected: SelectedPlanSnapshotDto[]): void {
    const fingerprint = JSON.stringify(selected.map((s) => [s.planId, s.slot, s.planName]));
    if (this.lastSavedPlansPendingChatFingerprint === fingerprint) {
      return;
    }
    this.lastSavedPlansPendingChatFingerprint = fingerprint;
    this.state.addAssistantMessage(PLAN_MESSAGES.SAVED_PLANS_PENDING);
  }

  private toRecommendationListItemFromSnapshot(plan: SelectedPlanSnapshotDto) {
    return {
      contractId: '',
      planName: plan.planName ?? '',
      planId: plan.planId ?? '',
      segmentId: '',
      pharmacyWiseRecommendations: [{
        contractId: '',
        planName: plan.planName ?? '',
        planType: plan.slot ?? '',
        planId: plan.planId ?? '',
        segmentId: '',
        pharmacyNumber: '',
        pharmacyName: '',
        pharmacyRetailType: '',
        dispenseFee: 0,
        premium: 0,
        deductible: 0,
        icl: 0,
        starRating: 0,
        websiteLink: '',
        contactTitle: '',
        phone: '',
        ext: '',
        fax: '',
        email: '',
        drugPriceCosts: [],
        totalPremiumToPay: 0,
        totalPrescriptionCost: 0,
        totalPrescriptionCostFullYear: 0,
        totalPlanCost: 0,
        prescriptionDrugCovered: false,
        partAandBBenefitServiceCost: 0,
        partABenefitServiceCost: 0,
        partBBenefitServiceCost: 0,
        planExpenses: [],
        planExpensesFullYear: [],
        unavailableDrugs: [],
        pharmacyNetworks: [],
        lName: '',
        fName: '',
        mName: '',
      }]
    };
  }

  private toMedigapPlanFromSnapshot(plan: SelectedPlanSnapshotDto) {
    return {
      key: '',
      age: 0,
      archive: null,
      company_base: {
        ambest_outlook: '',
        ambest_rating: '',
        business_type: '',
        company_image_url: '',
        customer_complaint_ratio: null,
        customer_satisfaction_ratio: 0,
        established_year: 0,
        last_modified: '',
        med_supp_national_market_data: null,
        naic: '',
        name: plan.companyName ?? '',
        name_full: plan.companyName ?? '',
        parent_company_base: null,
        sp_rating: '',
        state_marketing_data: [],
        type: '',
        underwriting_data: [],
      },
      contextual_data: null,
      discount_category: '',
      discounts: [],
      e_app_link: '',
      effective_date: '',
      expires_date: '',
      fees: [],
      gender: '',
      has_brochure: false,
      has_pdf_app: false,
      is_open_rate: false,
      last_modified: '',
      legacy_id: null,
      plan: plan.medigapPlanType ?? '',
      rate: { annual: 0, month: 0, quarter: 0, semi_annual: 0 },
      rate_increases: [],
      rate_type: '',
      rating_class: '',
      related_data: {},
      riders: [],
      select: false,
      tobacco: false,
      view_type: [],
      partBPremium: 0,
      partBPremiumSurcharge: 0,
      monthsUsedForExpenseCalc: 12,
      yearForPartBData: 0,
      medigapOOP: 0,
      partAServiceOOP: 0,
      partBServiceOOP: 0,
      naic: '',
    };
  }
}
