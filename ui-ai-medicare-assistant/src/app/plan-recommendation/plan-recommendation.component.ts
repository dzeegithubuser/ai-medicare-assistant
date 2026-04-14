import { Component, ChangeDetectionStrategy, inject, signal, OnInit, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DrugStateService, ChatPlanSelectionCommand } from '../services/drug-state.service';
import { PartDPlanService } from '../services/part-d-plan.service';
import { MedigapPlanService } from '../services/medigap-plan.service';
import { MedicareAdvantagePlanService } from '../services/medicare-advantage-plan.service';
import { PlanRecommendationService } from '../services/plan-recommendation.service';
import { ProfileService } from '../services/profile.service';
import { ReferenceDataService } from '../services/reference-data.service';
import { PlanCardEnrichmentService } from '../services/plan-card-enrichment.service';
import { PartDPlanRecommendationRequest, RecommendationListItem, PharmacyWiseRecommendation, EnrichedPartDCard, EnrichedMACard } from '../models/part-d-plan.model';
import { MedigapPlanQuotesRequest, MedigapPlan, EnrichedMedigapCard } from '../models/medigap-plan.model';
import { MedicareAdvantagePlanRequest } from '../models/medicare-advantage-plan.model';
import { finalize } from 'rxjs/operators';
import { CalculateCostsRequest } from '../models/cost-projection.model';
import { PrescriptionService } from '../services/prescription.service';
import { buildSelectedPlansSnapshotFromState } from '../medicare-analysis/current-prescription.mapper';
import { COST_PROJECTION_IMMUTABILITY_WARNING } from '../medicare-analysis/cost-projection-messages';
import { PLAN_MESSAGES } from '../constants/chat-messages';
import { PlanDetailDialogComponent, PlanDetailData } from './plan-detail-dialog/plan-detail-dialog.component';
import { RecommendationCardComponent } from './recommendation-card/recommendation-card.component';

import { MedigapGapSectionComponent } from './medigap-gap-section/medigap-gap-section.component';
import { PartDGapSectionComponent } from './partd-gap-section/partd-gap-section.component';
import { SelectedPlansSummaryComponent } from './selected-plans-summary/selected-plans-summary.component';
import { SavePrescriptionDialogComponent } from '../medicare-analysis/drug-step/save-prescription-dialog/save-prescription-dialog.component';
import { AppRoutes } from '../app-routes.const';

@Component({
  selector: 'app-plan-recommendation',
  templateUrl: './plan-recommendation.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatIconModule, MatButtonModule, MatProgressSpinnerModule,
    MatSelectModule, MatFormFieldModule, MatDialogModule, MatTooltipModule,
    RecommendationCardComponent,
    MedigapGapSectionComponent, PartDGapSectionComponent,
    SelectedPlansSummaryComponent,
  ],
})
export class PlanRecommendationComponent implements OnInit {
  protected state = inject(DrugStateService);
  private partDService = inject(PartDPlanService);
  private medigapService = inject(MedigapPlanService);
  private maService = inject(MedicareAdvantagePlanService);
  private planService = inject(PlanRecommendationService);
  private profileService = inject(ProfileService);
  private prescriptionService = inject(PrescriptionService);
  private refData = inject(ReferenceDataService);
  private enrichmentService = inject(PlanCardEnrichmentService);
  private dialog = inject(MatDialog);
  private router = inject(Router);

  // Medigap filter controls
  selectedMedigapDataSource = signal<'AIVANTE' | 'MEDICARE_GOV' | null>(null);   // null = CSG
  selectedMedigapPlanType = signal<string>('G');             // default Plan G
  get medigapDataSources() { return this.refData.medigapDataSources(); }
  get medigapPlanTypes() { return this.refData.medigapPlanTypes(); }

  // Section switch warning
  showSectionWarning = signal(false);
  pendingSection = signal<'partd' | 'ma' | null>(null);

  // Cost projection loading
  costLoading = signal(false);
  private lastCurrentSelectionSnapshotFingerprint: string | null = null;

  // Computed: does selected MA plan include Part D?
  readonly maIncludesPartD = computed(() => {
    const ma = this.state.selectedMAPlan();
    if (!ma) return false;
    const rec = ma.pharmacyWiseRecommendations?.[0];
    return rec ? rec.prescriptionDrugCovered : false;
  });

  // Has user made a complete selection? (delegated to state service)
  readonly hasCompleteSelection = this.state.hasCompletePlanSelection;

  // ─── Enrichment Maps ────────────────────────────────────────────

  private get selectedPharmacyNumbers(): string[] {
    return (this.state.selectedLookupPharmacies() ?? []).map(p => p.pharmacyNumber);
  }

  private get totalDrugs(): number {
    return this.state.confirmedDrugNames()?.size ?? 0;
  }

  readonly partDEnrichmentMap = computed(() => {
    const response = this.state.partDPlans();
    if (!response) return new Map<string, EnrichedPartDCard>();
    const pharmaNumbers = this.selectedPharmacyNumbers;
    const drugs = this.totalDrugs;
    const map = new Map<string, EnrichedPartDCard>();
    for (const plan of response.recommendationList) {
      map.set(plan.contractId + '-' + plan.planId, this.enrichmentService.enrichPartD(plan, response, pharmaNumbers, drugs));
    }
    return map;
  });

  readonly maEnrichmentMap = computed(() => {
    const response = this.state.maPlans();
    if (!response) return new Map<string, EnrichedMACard>();
    const pharmaNumbers = this.selectedPharmacyNumbers;
    const drugs = this.totalDrugs;
    const map = new Map<string, EnrichedMACard>();
    for (const plan of response.recommendationList) {
      map.set(plan.contractId + '-' + plan.planId, this.enrichmentService.enrichMA(plan, response, pharmaNumbers, drugs));
    }
    return map;
  });

  readonly medigapEnrichmentMap = computed(() => {
    const response = this.state.medigapQuotes();
    if (!response) return new Map<string, EnrichedMedigapCard>();
    const map = new Map<string, EnrichedMedigapCard>();
    for (const quote of (response.planList ?? [])) {
      if (quote.key) {
        map.set(quote.key, this.enrichmentService.enrichMedigap(quote, response));
      }
    }
    return map;
  });

  getPartDEnriched(plan: RecommendationListItem): EnrichedPartDCard | null {
    return this.partDEnrichmentMap().get(plan.contractId + '-' + plan.planId) ?? null;
  }

  getMAEnriched(plan: RecommendationListItem): EnrichedMACard | null {
    return this.maEnrichmentMap().get(plan.contractId + '-' + plan.planId) ?? null;
  }

  getMedigapEnriched(plan: MedigapPlan): EnrichedMedigapCard | null {
    return plan.key ? (this.medigapEnrichmentMap().get(plan.key) ?? null) : null;
  }

  constructor() {
    // Watch for chat-driven plan selection commands
    effect(() => {
      const cmd = this.state.pendingPlanSelection();
      if (!cmd) return;
      this.state.pendingPlanSelection.set(null);
      this.applyChatPlanSelection(cmd);
    });

    // Watch for chat-driven "run analysis" trigger
    effect(() => {
      const trigger = this.state.pendingRunAnalysis();
      if (!trigger) return;
      this.state.pendingRunAnalysis.set(null);
      this.calculateLifetimeCost(true);
    });

    // Load Part D / MA when section becomes active (UI `switchSection`, chat SWITCH_TO_PDP / SWITCH_TO_MA,
    // or session restore). Chat previously only set `activeSection` without calling the loaders.
    // Also depend on `profile()` so we retry after profile finishes loading.
    effect(() => {
      this.profileService.profile();
      const section = this.state.activeSection();
      if (section === 'partd') {
        if (!this.state.hasPartDPlans() && !this.state.isPartDLoading()) this.loadPartDPlans();
      } else if (section === 'ma') {
        if (!this.state.hasMAPlans() && !this.state.isMALoading()) this.loadMAPlans();
      }
    });

    // Hydration / saved-analysis stubs use empty contractId or medigap key — match to live API rows so cards show selected.
    effect(() => {
      this.state.partDPlans();
      this.state.selectedPartDPlan();
      this.reconcilePartDSelectionWithList();
    });
    effect(() => {
      this.state.medigapQuotes();
      this.state.selectedMedigapPlan();
      this.selectedMedigapPlanType();
      this.reconcileMedigapSelectionWithList();
    });
    effect(() => {
      this.state.maPlans();
      this.state.selectedMAPlan();
      this.reconcileMASelectionWithList();
    });
    effect(() => {
      this.state.partDPlans();
      this.state.selectedMAGapPartDPlan();
      this.reconcileMAGapPartDSelectionWithList();
    });

    // Keep userAnalysisSelections in sync as plans/pharmacies/section change on this step.
    effect(() => {
      this.state.activeSection();
      this.state.selectedPartDPlan();
      this.state.selectedMedigapPlan();
      this.state.selectedMAPlan();
      this.state.selectedMAGapPartDPlan();
      this.state.selectedLookupPharmacies();
      this.state.confirmedDrugNames();
      this.persistCurrentSelectionSnapshot();
    });
  }

  ngOnInit() {
    this.refData.load();
  }

  // ─── Load Data ──────────────────────────────────────────────────

  loadPartDPlans() {
    const profile = this.profileService.profile()?.profile;
    if (!profile) return;

    this.state.setPartDLoading(true);
    const request = this.buildPartDRequest(profile, false);

    this.partDService.recommend(request).subscribe({
      next: (result) => {
        this.state.setPartDPlans(result);
        this.state.setPartDLoading(false);
      },
      error: () => this.state.setPartDLoading(false),
    });
  }

  loadMAPlans() {
    const profile = this.profileService.profile()?.profile;
    if (!profile) return;

    this.state.setMALoading(true);
    const request: MedicareAdvantagePlanRequest = {
      ...this.buildPartDRequest(profile, false),
      medicareAdvantage: true,
    };

    this.maService.recommend(request).subscribe({
      next: (result) => {
        this.state.setMAPlans(result);
        this.state.setMALoading(false);
      },
      error: () => this.state.setMALoading(false),
    });
  }

  loadMedigapQuotes() {
    const profile = this.profileService.profile()?.profile;
    if (!profile) return;

    this.state.setMedigapLoading(true);
    const dob = profile.dateOfBirth; // YYYY-MM-DD
    const parts = dob.split('-');
    const birthDateMmYyyy = parts.length >= 2 ? `${parts[1]}-${parts[0]}` : dob;

    const request: MedigapPlanQuotesRequest = {
      zip5: profile.zipCode,
      gender: profile.gender === 'Male' ? 'M' : 'F',
      tobacco: profile.tobaccoStatus,
      birthDate: birthDateMmYyyy,
      plan: this.selectedMedigapPlanType(),
      county: profile.county,
      taxFilingStatus: profile.taxFilingStatus,
      magiTier: parseInt(profile.magiTier, 10) || 0,
      healthProfile: profile.healthCondition,
      coverageYear: String(profile.coverageYear),
      versionId: this.selectedMedigapDataSource(),
    };

    this.medigapService.getQuotes(request).subscribe({
      next: (result) => {
        this.state.setMedigapQuotes(result);
        this.state.setMedigapLoading(false);
      },
      error: () => this.state.setMedigapLoading(false),
    });
  }

  // ─── Section Switching ──────────────────────────────────────────

  activateSection(section: 'partd' | 'ma') {
    const current = this.state.activeSection();
    if (current === section) return;

    // If user already has selections in the other section, warn
    if (current !== null && this.hasAnySelection(current)) {
      this.pendingSection.set(section);
      this.showSectionWarning.set(true);
      return;
    }

    this.switchSection(section);
  }

  confirmSectionSwitch() {
    const section = this.pendingSection();
    if (section) {
      this.state.resetPlanSelections();
      this.switchSection(section);
    }
    this.showSectionWarning.set(false);
    this.pendingSection.set(null);
  }

  cancelSectionSwitch() {
    this.showSectionWarning.set(false);
    this.pendingSection.set(null);
  }

  private switchSection(section: 'partd' | 'ma') {
    this.state.addSystemMessage(`Switched to ${section === 'partd' ? 'Part D + Medigap' : 'Medicare Advantage'} plans`);
    this.state.setActiveSection(section);
  }

  /** Reload plans for the currently active section */
  refreshPlans() {
    const section = this.state.activeSection();
    if (section === 'partd') {
      this.loadPartDPlans();
    } else if (section === 'ma') {
      this.loadMAPlans();
    }
  }

  private hasAnySelection(section: 'partd' | 'ma'): boolean {
    if (section === 'partd') {
      return this.state.selectedPartDPlan() !== null || this.state.selectedMedigapPlan() !== null;
    }
    return this.state.selectedMAPlan() !== null || this.state.selectedMAGapPartDPlan() !== null;
  }

  // ─── Plan Selection ─────────────────────────────────────────────

  selectPartDPlan(plan: RecommendationListItem, opts?: { fromChat?: boolean }) {
    this.state.selectPartDPlan(plan);
    if (!opts?.fromChat) {
      this.state.addAssistantMessage(PLAN_MESSAGES.SELECTED_PART_D(plan.planName));
    }
    // Trigger medigap load as the fill-the-gap step
    if (!this.state.hasMedigapQuotes() && !this.state.isMedigapLoading()) {
      this.loadMedigapQuotes();
    }
  }

  selectMedigapPlan(plan: MedigapPlan, opts?: { fromChat?: boolean }) {
    this.state.selectMedigapPlan(plan);
    if (!opts?.fromChat) {
      const carrier = plan.company_base?.name ?? 'Plan';
      this.state.addAssistantMessage(PLAN_MESSAGES.SELECTED_MEDIGAP(carrier, plan.plan));
    }
  }

  selectMAPlan(plan: RecommendationListItem, opts?: { fromChat?: boolean }) {
    this.state.selectMAPlan(plan);
    if (!opts?.fromChat) {
      this.state.addAssistantMessage(PLAN_MESSAGES.SELECTED_MA(plan.planName));
    }
    // If it doesn't cover Part D, need to load Part D as fill-the-gap
    const rec = plan.pharmacyWiseRecommendations?.[0];
    if (rec && !rec.prescriptionDrugCovered) {
      if (!this.state.hasPartDPlans() && !this.state.isPartDLoading()) {
        this.loadPartDPlans();
      }
    }
  }

  selectMAGapPartDPlan(plan: RecommendationListItem, opts?: { fromChat?: boolean }) {
    this.state.selectMAGapPartDPlan(plan);
    if (!opts?.fromChat) {
      this.state.addAssistantMessage(PLAN_MESSAGES.SELECTED_MA_GAP_PART_D(plan.planName));
    }
  }

  isPartDSelected(plan: RecommendationListItem): boolean {
    return this.state.selectedPartDPlan()?.contractId === plan.contractId
        && this.state.selectedPartDPlan()?.planId === plan.planId;
  }

  isMedigapSelected(plan: MedigapPlan): boolean {
    return this.state.selectedMedigapPlan()?.key === plan.key;
  }

  isMASelected(plan: RecommendationListItem): boolean {
    return this.state.selectedMAPlan()?.contractId === plan.contractId
        && this.state.selectedMAPlan()?.planId === plan.planId;
  }

  isMAGapPartDSelected(plan: RecommendationListItem): boolean {
    return this.state.selectedMAGapPartDPlan()?.contractId === plan.contractId
        && this.state.selectedMAGapPartDPlan()?.planId === plan.planId;
  }

  // ─── Chat-driven Plan Selection ─────────────────────────────────

  private applyChatPlanSelection(cmd: ChatPlanSelectionCommand): void {
    const cat = cmd.planCategory;
    const name = cmd.planName?.toLowerCase() ?? '';

    if (cmd.action === 'select') {
      if (cat === 'partd' || (!cat && this.state.activeSection() === 'partd')) {
        const plan = this.findPartDPlanByName(name);
        if (plan) this.selectPartDPlan(plan, { fromChat: true });
      } else if (cat === 'medigap') {
        const plan = this.findMedigapPlanByName(name);
        if (plan) this.selectMedigapPlan(plan, { fromChat: true });
      } else if (cat === 'ma' || (!cat && this.state.activeSection() === 'ma')) {
        const plan = this.findMAPlanByName(name);
        if (plan) this.selectMAPlan(plan, { fromChat: true });
      }
    } else if (cmd.action === 'remove') {
      if (cat === 'partd') {
        const removedName = this.state.selectedPartDPlan()?.planName ?? null;
        this.state.selectPartDPlan(null);
        this.state.addAssistantMessage(PLAN_MESSAGES.REMOVED_FROM_SELECTION(removedName, 'Part D'));
      } else if (cat === 'medigap') {
        const mg = this.state.selectedMedigapPlan();
        const removedName = mg ? `${mg.company_base?.name ?? ''} Plan ${mg.plan}`.trim() : null;
        this.state.selectMedigapPlan(null);
        this.state.addAssistantMessage(PLAN_MESSAGES.REMOVED_FROM_SELECTION(removedName, 'Medigap'));
      } else if (cat === 'ma') {
        const removedName = this.state.selectedMAPlan()?.planName ?? null;
        this.state.selectMAPlan(null);
        this.state.selectMAGapPartDPlan(null);
        this.state.addAssistantMessage(PLAN_MESSAGES.REMOVED_FROM_SELECTION(removedName, 'Medicare Advantage'));
      }
    }
  }

  /** Keep Part D selection only if it exists in the current Part D list; otherwise clear (and Medigap). */
  private reconcilePartDSelectionWithList(): void {
    if (this.state.activeSection() !== 'partd') return;
    const sel = this.state.selectedPartDPlan();
    if (!sel) return;
    if (this.state.isPartDLoading()) return;

    const partDResponse = this.state.partDPlans();
    if (partDResponse === null) return;

    const list = partDResponse.recommendationList ?? [];
    if (list.length === 0) {
      this.state.addAssistantMessage(PLAN_MESSAGES.CLEARED_PART_D_NO_LIST);
      this.state.selectPartDPlan(null);
      this.state.selectMedigapPlan(null);
      return;
    }

    const alreadyInList = list.some(
      p => p.contractId === sel.contractId && p.planId === sel.planId && !!sel.contractId,
    );
    if (alreadyInList) return;

    const match =
      (sel.planId ? list.find(p => p.planId === sel.planId) : undefined) ??
      (sel.planName ? list.find(p => p.planName.toLowerCase() === sel.planName.toLowerCase()) : undefined);

    if (match) {
      const wasStub = !String(sel.contractId ?? '').trim();
      this.state.selectPartDPlan(match);
      if (wasStub) {
        this.state.addAssistantMessage(PLAN_MESSAGES.MATCHED_SAVED_PART_D(match.planName));
      }
      if (!this.state.hasMedigapQuotes() && !this.state.isMedigapLoading()) {
        this.loadMedigapQuotes();
      }
      return;
    }

    this.state.addAssistantMessage(PLAN_MESSAGES.CLEARED_PART_D_NOT_IN_LIST(sel.planName ?? null));
    this.state.selectPartDPlan(null);
    this.state.selectMedigapPlan(null);
  }

  /**
   * Align Medigap plan-type filter with a hydrated stub (e.g. saved Plan B while UI defaulted to G),
   * then keep Medigap only if it appears in the current quote list.
   */
  private reconcileMedigapSelectionWithList(): void {
    if (this.state.activeSection() !== 'partd') return;
    const sel = this.state.selectedMedigapPlan();
    if (!sel) return;

    if (!sel.key && sel.plan) {
      const stubLetter = sel.plan.trim().toUpperCase();
      const filterLetter = this.selectedMedigapPlanType().trim().toUpperCase();
      if (stubLetter && stubLetter !== filterLetter) {
        this.selectedMedigapPlanType.set(stubLetter);
        if (!this.state.isMedigapLoading()) {
          this.loadMedigapQuotes();
        }
        return;
      }
    }

    if (this.state.isMedigapLoading()) return;

    const quotesResponse = this.state.medigapQuotes();
    if (quotesResponse === null) return;

    const list = quotesResponse.planList ?? [];
    if (list.length === 0) {
      this.state.addAssistantMessage(PLAN_MESSAGES.CLEARED_MEDIGAP_NO_LIST);
      this.state.selectMedigapPlan(null);
      return;
    }

    if (sel.key && list.some(p => p.key === sel.key)) return;

    const carrier = (sel.company_base?.name ?? '').trim().toLowerCase();
    const letter = (sel.plan ?? '').trim().toUpperCase();
    const match =
      list.find(
        p =>
          (p.company_base?.name ?? '').trim().toLowerCase() === carrier &&
          (p.plan ?? '').trim().toUpperCase() === letter,
      ) ??
      list.find(
        p =>
          carrier.length > 0 &&
          (p.company_base?.name ?? '').toLowerCase().includes(carrier) &&
          (p.plan ?? '').trim().toUpperCase() === letter,
      );

    if (match) {
      const wasStub = !String(sel.key ?? '').trim();
      this.state.selectMedigapPlan(match);
      if (wasStub) {
        this.state.addAssistantMessage(
          PLAN_MESSAGES.MATCHED_SAVED_MEDIGAP(match.company_base?.name ?? 'Plan', match.plan),
        );
      }
      return;
    }

    const label = `${sel.company_base?.name ?? 'Medigap'} — Plan ${sel.plan}`.trim();
    this.state.addAssistantMessage(PLAN_MESSAGES.CLEARED_MEDIGAP_NOT_IN_LIST(label));
    this.state.selectMedigapPlan(null);
  }

  private reconcileMASelectionWithList(): void {
    if (this.state.activeSection() !== 'ma') return;
    const sel = this.state.selectedMAPlan();
    if (!sel) return;
    if (this.state.isMALoading()) return;

    const maResponse = this.state.maPlans();
    if (maResponse === null) return;

    const list = maResponse.recommendationList ?? [];
    if (list.length === 0) {
      this.state.addAssistantMessage(PLAN_MESSAGES.CLEARED_MA_NO_LIST);
      this.state.selectMAPlan(null);
      this.state.selectMAGapPartDPlan(null);
      return;
    }

    const alreadyInList = list.some(
      p => p.contractId === sel.contractId && p.planId === sel.planId && !!sel.contractId,
    );
    if (alreadyInList) {
      // Ensure Part D gap plans are loaded if this MA plan doesn't cover prescriptions
      this.ensurePartDGapLoadForMA(sel);
      return;
    }

    const match =
      (sel.planId ? list.find(p => p.planId === sel.planId) : undefined) ??
      (sel.planName ? list.find(p => p.planName.toLowerCase() === sel.planName.toLowerCase()) : undefined);

    if (match) {
      const wasStub = !String(sel.contractId ?? '').trim();
      this.state.selectMAPlan(match);
      if (wasStub) {
        this.state.addAssistantMessage(PLAN_MESSAGES.MATCHED_SAVED_MA(match.planName));
      }
      // If matched plan doesn't include Part D, load Part D plans for gap selection
      this.ensurePartDGapLoadForMA(match);
      return;
    }

    this.state.addAssistantMessage(PLAN_MESSAGES.CLEARED_MA_NOT_IN_LIST(sel.planName ?? null));
    this.state.selectMAPlan(null);
    this.state.selectMAGapPartDPlan(null);
  }

  /** If the selected MA plan doesn't include prescription drug coverage, load Part D plans for gap fill. */
  private ensurePartDGapLoadForMA(plan: RecommendationListItem): void {
    const rec = plan.pharmacyWiseRecommendations?.[0];
    if (rec && !rec.prescriptionDrugCovered) {
      if (!this.state.hasPartDPlans() && !this.state.isPartDLoading()) {
        this.loadPartDPlans();
      }
    }
  }

  private reconcileMAGapPartDSelectionWithList(): void {
    if (this.state.activeSection() !== 'ma') return;
    const gap = this.state.selectedMAGapPartDPlan();
    if (!gap) return;
    if (this.state.isPartDLoading()) return;

    const partDResponse = this.state.partDPlans();
    if (partDResponse === null) return;

    const list = partDResponse.recommendationList ?? [];
    if (list.length === 0) {
      this.state.addAssistantMessage(PLAN_MESSAGES.CLEARED_MA_GAP_PART_D_NO_LIST);
      this.state.selectMAGapPartDPlan(null);
      return;
    }

    const alreadyInList = list.some(
      p => p.contractId === gap.contractId && p.planId === gap.planId && !!gap.contractId,
    );
    if (alreadyInList) return;

    const match =
      (gap.planId ? list.find(p => p.planId === gap.planId) : undefined) ??
      (gap.planName ? list.find(p => p.planName.toLowerCase() === gap.planName.toLowerCase()) : undefined);

    if (match) {
      const wasStub = !String(gap.contractId ?? '').trim();
      this.state.selectMAGapPartDPlan(match);
      if (wasStub) {
        this.state.addAssistantMessage(PLAN_MESSAGES.MATCHED_SAVED_MA_GAP_PART_D(match.planName));
      }
      return;
    }

    this.state.addAssistantMessage(PLAN_MESSAGES.CLEARED_MA_GAP_PART_D_NOT_IN_LIST(gap.planName ?? null));
    this.state.selectMAGapPartDPlan(null);
  }

  private findPartDPlanByName(name: string): RecommendationListItem | null {
    const plans = this.state.partDPlans()?.recommendationList ?? [];
    return plans.find(p => p.planName.toLowerCase() === name)
        ?? plans.find(p => p.planName.toLowerCase().includes(name))
        ?? null;
  }

  private findMedigapPlanByName(name: string): MedigapPlan | null {
    const plans = this.state.medigapQuotes()?.planList ?? [];
    return plans.find(p => (p.company_base?.name ?? '').toLowerCase() === name)
        ?? plans.find(p => (p.company_base?.name ?? '').toLowerCase().includes(name))
        ?? null;
  }

  private findMAPlanByName(name: string): RecommendationListItem | null {
    const plans = this.state.maPlans()?.recommendationList ?? [];
    return plans.find(p => p.planName.toLowerCase() === name)
        ?? plans.find(p => p.planName.toLowerCase().includes(name))
        ?? null;
  }

  // ─── Detail Dialog ──────────────────────────────────────────────

  openPlanDetail(data: PlanDetailData) {
    this.dialog.open(PlanDetailDialogComponent, {
      data,
      width: '600px',
      maxHeight: '90vh',
    });
  }

  // ─── Medigap Filter Change ─────────────────────────────────────

  onMedigapFilterChange() {
    this.state.selectMedigapPlan(null);
    this.loadMedigapQuotes();
  }

  // ─── Cost Projection ───────────────────────────────────────────

  /**
   * @param fromChatConfirmation When true, chat already showed immutability + name dialog in `resolveRunAnalysisConfirmation`.
   */
  calculateLifetimeCost(fromChatConfirmation = false) {
    if (this.costLoading()) return;
    if (!fromChatConfirmation) {
      const ref = this.dialog.open(SavePrescriptionDialogComponent, {
        width: '420px',
        data: {
          title: 'Name this recommendation',
          subtitle: 'We will save your profile, drugs, pharmacies, and plans under this name after cost evaluation.',
          icon: 'bookmark',
        },
      });
      ref.afterClosed().subscribe((name: string | null) => {
        const trimmed = name?.trim() ?? '';
        if (!trimmed) return;
        this.state.setPendingCostRunRecommendationName(trimmed);
        this.state.addAssistantMessage(COST_PROJECTION_IMMUTABILITY_WARNING);
        this.runLifetimeCostEvaluation();
      });
      return;
    }
    this.runLifetimeCostEvaluation();
  }

  private runLifetimeCostEvaluation() {
    this.state.addSystemMessage('Calculating lifetime cost projection');
    this.costLoading.set(true);

    const section = this.state.activeSection();
    let planName = '';
    let planId = '';
    let bundleCode = '';
    let maPremium = 0;
    let maWithRx = false;
    let partDOOP = 0;
    let partAService = 0;
    let partBService = 0;
    let partDPremium = 0;
    let adjustedMonth = 12;
    let partDOOPFullYear = 0;

    if (section === 'partd') {
      const partD = this.state.selectedPartDPlan()!;
      const rec = partD.pharmacyWiseRecommendations?.[0];
      const medigap = this.state.selectedMedigapPlan();
      planName = partD.planName;
      planId = partD.planId;
      bundleCode = 'PDP_MEDIGAP';
      partDOOP = rec?.totalPrescriptionCost ?? 0;
      partDOOPFullYear = rec?.totalPrescriptionCostFullYear ?? 0;
      partAService = medigap?.partAServiceOOP ?? 0;
      partBService = medigap?.partBServiceOOP ?? 0;
      partDPremium = rec?.totalPremiumToPay ?? 0;
      adjustedMonth = medigap?.monthsUsedForExpenseCalc ?? 12;
    } else if (section === 'ma') {
      const ma = this.state.selectedMAPlan()!;
      const rec = ma.pharmacyWiseRecommendations?.[0];
      const maResponse = this.state.maPlans();
      planName = ma.planName;
      planId = ma.planId;
      maPremium = rec?.premium ?? 0;
      maWithRx = rec?.prescriptionDrugCovered ?? false;
      partDOOP = rec?.totalPrescriptionCost ?? 0;
      partDOOPFullYear = rec?.totalPrescriptionCostFullYear ?? 0;
      partAService = rec?.partABenefitServiceCost ?? 0;
      partBService = rec?.partBBenefitServiceCost ?? 0;
      bundleCode = this.state.selectedMAGapPartDPlan() ? 'MA_PDP' : 'MA_ONLY';
      adjustedMonth = maResponse?.monthsUsedForExpenseCalc || rec?.planExpenses?.length || 12;
      const gapPartD = this.state.selectedMAGapPartDPlan();
      if (gapPartD) {
        const gapRec = gapPartD.pharmacyWiseRecommendations?.[0];
        partDPremium = gapRec?.totalPremiumToPay ?? 0;
        partDOOP = gapRec?.totalPrescriptionCost ?? 0;
        partDOOPFullYear = gapRec?.totalPrescriptionCostFullYear ?? 0;
      }
    }

    const request: CalculateCostsRequest = {
      planBundleCode: bundleCode,
      medicareAdvantagePremium: maPremium,
      maWithPrescriptionBenefit: maWithRx,
      partDOOP: partDOOP,
      partDOOPFullYear: partDOOPFullYear,
      partABenefitServiceCost: partAService,
      partBBenefitServiceCost: partBService,
      planRecommendName: planName,
      recommendationListId: planId,
      supplementDataProvided: section === 'partd' && this.state.selectedMedigapPlan() !== null,
      partDDataProvided: section === 'partd' || this.state.selectedMAGapPartDPlan() !== null,
      reserveDaysUsed: 0,
      dental: true,
      dentalHealthGrade: 1,
      boughtPlanA: false,
      medicareAdvantageDataProvided: section === 'ma',
      partDPremium,
      calculateForAdjustedMonth: adjustedMonth,
      supplementPlanType: section === 'partd' ? (this.state.selectedMedigapPlan()?.plan ?? '') : '',
    };

    this.planService.evaluateCosts(request).subscribe({
      next: (result) => {
        this.state.setCostProjection(result);
        this.costLoading.set(false);
        // Save only plans after cost evaluation — drugs and pharmacies are already saved.
        const plans = buildSelectedPlansSnapshotFromState(this.state);
        this.prescriptionService
          .saveCurrentPlans(plans, this.state.activeSection())
          .pipe(finalize(() => this.router.navigate([AppRoutes.abs.COST_PROJECTIONS])))
          .subscribe({
            next: () => this.profileService.loadProfile().subscribe({ error: () => {} }),
          });
      },
      error: () => this.costLoading.set(false),
    });
  }

  // ─── Helpers ────────────────────────────────────────────────────

  getFirstRec(plan: RecommendationListItem): PharmacyWiseRecommendation | null {
    return plan.pharmacyWiseRecommendations?.[0] ?? null;
  }

  private persistCurrentSelectionSnapshot(): void {
    const plans = buildSelectedPlansSnapshotFromState(this.state);
    const activeSection = this.state.activeSection();
    const fingerprint = JSON.stringify({
      section: activeSection ?? null,
      plans: plans.map((p) => [p.slot, p.planId, p.contractId, p.planName]),
    });
    if (this.lastCurrentSelectionSnapshotFingerprint === fingerprint) return;
    this.lastCurrentSelectionSnapshotFingerprint = fingerprint;
    this.prescriptionService.saveCurrentPlans(plans, activeSection).subscribe({ error: () => {} });
  }

  private buildPartDRequest(profile: any, _ma: boolean): PartDPlanRecommendationRequest {
    // Build prescription list from FP confirmed drugs
    const prescriptions = this.buildPrescriptions();
    const pharmacies = this.buildPharmacies();

    const dob = profile.dateOfBirth;
    const parts = dob.split('-');
    const birthDate = parts.length >= 2 ? `${parts[1]}-${parts[0]}` : dob;

    return {
      userId: '',
      sortRecommendations: 'PREMIUM',
      countycodeModel: {
        zipcode: profile.zipCode,
        state: this.refData.usStates().find(s => s.value === profile.state)?.label ?? profile.state,
        stateCode: profile.state,
        city: profile.city,
        latitude: profile.latitude ?? 0,
        longitude: profile.longitude ?? 0,
        countyCode: profile.countyCode,
        countyName: profile.county,
      },
      prescriptions,
      beneficiaryCostDataRequired: true,
      pharmacyNetworkDataRequired: true,
      pharmacies,
      planRecommendName: '',
      planRecommendEmail: '',
      drugListingName: '',
      recommendationListId: '',
      taxFilingStatus: profile.taxFilingStatus,
      magiTier: parseInt(profile.magiTier, 10) || 0,
      healthGrade: profile.healthCondition,
      birthDate,
      fullYearOOPCost: true,
      coverageYear: String(profile.coverageYear),
      includePlanExpensesFullYear: true,
      planPage: 1,
      planPageSize: 50,
      recommendationPage: 1,
      recommendationPageSize: 50,
      starRatingFilter: null,
      prescriptionCoverageFilter: null,
      contractIdFilter: null,
      mailOrderPharmacy: false,
    };
  }

  private buildPrescriptions(): { rxcui: string; refillDuration: string; prescriptionCount: number; ndc: string }[] {
    const details = this.state.drugDetails();
    const confirmed = this.state.confirmedDrugNames();
    if (!details || confirmed.size === 0) return [];

    const selRaw = sessionStorage.getItem('formulation-selections');
    const selMap: Map<string, any> = selRaw ? new Map(JSON.parse(selRaw)) : new Map();

    const result: { rxcui: string; refillDuration: string; prescriptionCount: number; ndc: string }[] = [];
    for (const drugResult of details.results) {
      if (!confirmed.has(drugResult.drugName)) continue;
      const sel = selMap.get(drugResult.drugName);
      if (!sel) continue;
      result.push({
        rxcui: sel.rxcui || drugResult.matchedDrug?.rxcui || '',
        refillDuration: '30',
        prescriptionCount: 30,
        ndc: '',
      });
    }
    return result;
  }

  private buildPharmacies(): { pharmacyNumber: string; pharmacyName: string; latitude: string; longitude: string; address: string; distance: string; zipcode: string }[] {
    const selected = this.state.selectedLookupPharmacies();
    return selected.map(p => ({
      pharmacyNumber: String(p.pharmacyNumber),
      pharmacyName: p.pharmacyName,
      latitude: String(p.latitude ?? ''),
      longitude: String(p.longitude ?? ''),
      address: p.address ?? '',
      distance: String(p.distance ?? ''),
      zipcode: String(p.zipcode ?? ''),
    }));
  }
}
