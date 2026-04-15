import { Injectable, signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  DrugNameSuggestion,
  PharmacyLookupResponse, PharmacyLookupEntry,
  BulkDrugSearchResponse
} from '../models/drug.model';
import { EvaluateCostsResponse } from '../models/cost-projection.model';
import { PartDPlanRecommendationResponse, RecommendationListItem } from '../models/part-d-plan.model';
import { MedigapPlanQuotesResponse, MedigapPlan } from '../models/medigap-plan.model';
import { MedicareAdvantagePlanResponse } from '../models/medicare-advantage-plan.model';
import { ChatSignalRService } from './chat-signal-r.service';
import {
  ChatDrugSelectionCommand,
  ChatMessage,
  ChatPharmacySelectionCommand,
  ChatPlanSelectionCommand,
  ChatRunAnalysisCommand,
  PendingDrugFollowupPrompt,
} from '../models/chat-state.model';
export type {
  ChatDrugSelectionCommand,
  ChatMessage,
  ChatPharmacySelectionCommand,
  ChatPlanSelectionCommand,
  ChatRunAnalysisCommand,
  PendingDrugFollowupPrompt,
} from '../models/chat-state.model';

@Injectable({ providedIn: 'root' })
export class DrugStateService {
  private chatSignalR = inject(ChatSignalRService);
  private router = inject(Router);

  private static readonly STORAGE_KEY = 'drug-analysis-state';
  /** Session key written by fp-drugs-step; must stay in sync for prerequisite checks when that step is not mounted. */
  static readonly FP_CONFIRMED_DRUGS_SESSION_KEY = 'confirmed-drugs';
  private runtimePersistenceStarted = false;

  readonly messages = signal<ChatMessage[]>([]);
  readonly isLoading = signal(false);
  /** True while persisting the FP drug list to the server before opening Pharmacies. */
  readonly isSavingCurrentPrescription = signal(false);

  // Wizard step tracking (analysis shell: 1=Profile, 2=Drugs, 3=Pharmacies, 4=Plans)
  readonly currentStep = signal<1 | 2 | 3 | 4>(1);

  /** Bumped when the analysis stepper gained a Profile step; persisted to migrate old session snapshots. */
  private static readonly ANALYSIS_STEP_SCHEMA_VERSION = 2;

  // Saved prescription name
  readonly prescriptionName = signal<string | null>(null);

  // Drug name suggestion signals (Step 1 of drug search)
  readonly drugSuggestions = signal<DrugNameSuggestion[]>([]);
  readonly hasSuggestions = computed(() => this.drugSuggestions().length > 0);
  readonly isVerifyingNames = signal(false);

  /** Chat-driven drug formulation selection command. DrugsStepComponent watches and applies. */
  readonly pendingDrugSelection = signal<ChatDrugSelectionCommand | null>(null);
  /** Tracks pending follow-up input requested by chat for a specific drug (e.g. quantity). */
  readonly pendingDrugFollowupPrompt = signal<PendingDrugFollowupPrompt | null>(null);

  /** Chat-driven pharmacy selection command. PharmacyStepComponent watches and applies. */
  readonly pendingPharmacySelection = signal<ChatPharmacySelectionCommand | null>(null);

  /**
   * Drug search text stored when the user requests a drug action from a non-drugs page
   * (e.g. "add metformin" on the pharmacies page). ChatComponent picks this up on
   * NavigationEnd to `/analysis/fp-drugs` and fires the drug name suggestion flow.
   */
  readonly pendingCrossPageDrugSearch = signal<string | null>(null);

  /** Chat-driven plan selection command. PlanRecommendationComponent watches and applies. */
  readonly pendingPlanSelection = signal<ChatPlanSelectionCommand | null>(null);

  /** Chat-driven cost analysis trigger. PlanRecommendationComponent watches and fires calculateLifetimeCost. */
  readonly pendingRunAnalysis = signal<ChatRunAnalysisCommand | null>(null);

  /**
   * Set before running cost evaluation; cleared after auto-save on cost-projections or manually.
   * Persisted so refresh mid-flow does not silently drop the name.
   */
  readonly pendingCostRunRecommendationName = signal<string | null>(null);

  /**
   * Incremented by resetAll(). ChatWizardService watches this via effect()
   * to reset the wizard state whenever the analysis is cleared.
   */
  readonly wizardResetTrigger = signal(0);

  /** Route to return to after navigating away from analysis (e.g. to profile) */
  readonly returnRoute = signal<string | null>(null);

  // Cost projection signals
  readonly costProjection = signal<EvaluateCostsResponse | null>(null);
  readonly hasCostProjection = computed(() => this.costProjection() !== null);

  // Financial Planner pharmacy lookup signals
  readonly pharmacyLookup = signal<PharmacyLookupResponse | null>(null);
  readonly isPharmacyLookupLoading = signal(false);
  /** True after a lookup API response exists (including zero matches). Used so UI can show filters + empty state + selected list. */
  readonly hasPharmacyLookup = computed(() => this.pharmacyLookup() !== null);
  /** Tracks selected pharmacy numbers from lookup results */
  readonly selectedLookupPharmacies = signal<PharmacyLookupEntry[]>([]);
  readonly hasSelectedLookupPharmacies = computed(() => this.selectedLookupPharmacies().length > 0);

  /** User explicitly confirmed their pharmacy selection (e.g. clicked "Go to Plans" button) */
  readonly pharmacySelectionConfirmed = signal(false);

  // Financial Planner Drug Detail signals
  readonly drugDetails = signal<BulkDrugSearchResponse | null>(null);
  readonly isDrugDetailsLoading = signal(false);
  readonly hasDrugDetails = computed(() =>
    (this.drugDetails()?.results?.length ?? 0) > 0
  );

  // FP confirmed drug selections (shared with analysis shell for navigation gating)
  readonly confirmedDrugNames = signal<Set<string>>(new Set());
  readonly hasConfirmedDrugs = computed(() => this.confirmedDrugNames().size > 0);

  // ─── Plan Recommendation Signals ────────────────────────────
  readonly partDPlans = signal<PartDPlanRecommendationResponse | null>(null);
  readonly isPartDLoading = signal(false);
  readonly hasPartDPlans = computed(() => (this.partDPlans()?.recommendationList?.length ?? 0) > 0);

  readonly medigapQuotes = signal<MedigapPlanQuotesResponse | null>(null);
  readonly isMedigapLoading = signal(false);
  readonly hasMedigapQuotes = computed(() => (this.medigapQuotes()?.planList?.length ?? 0) > 0);

  readonly maPlans = signal<MedicareAdvantagePlanResponse | null>(null);
  readonly isMALoading = signal(false);
  readonly hasMAPlans = computed(() => (this.maPlans()?.recommendationList?.length ?? 0) > 0);

  // Selected plans (one per section)
  readonly selectedPartDPlan = signal<RecommendationListItem | null>(null);
  readonly selectedMedigapPlan = signal<MedigapPlan | null>(null);
  readonly selectedMAPlan = signal<RecommendationListItem | null>(null);
  // "Fill the gap" Part D plan when MA doesn't include Part D
  readonly selectedMAGapPartDPlan = signal<RecommendationListItem | null>(null);

  // Which section the user is working in: 'partd' or 'ma'
  readonly activeSection = signal<'partd' | 'ma' | null>(null);

  /** True when the user has made a complete plan selection for the active section */
  readonly hasCompletePlanSelection = computed(() => {
    const section = this.activeSection();
    if (section === 'partd') {
      return this.selectedPartDPlan() !== null && this.selectedMedigapPlan() !== null;
    }
    if (section === 'ma') {
      const maPlan = this.selectedMAPlan();
      if (!maPlan) return false;
      const rec = maPlan.pharmacyWiseRecommendations?.[0];
      const maIncludesPartD = rec ? rec.prescriptionDrugCovered : false;
      if (maIncludesPartD) return true;
      return this.selectedMAGapPartDPlan() !== null;
    }
    return false;
  });

  /** Timer handle for debouncing rapid message bursts before sending over SignalR. */
  private syncTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // Do not auto-hydrate running-session cache on first load.
    // Initial state should come from explicit flows (e.g., backend hydration),
    // not from the `drug-analysis-state` session cache.
  }

  // ─── Session Storage Persistence ───────────────────────────────

  private persist() {
    if (!this.runtimePersistenceStarted) {
      return;
    }
    const snapshot = {
      pharmacyLookup: this.pharmacyLookup(),
      selectedLookupPharmacies: this.selectedLookupPharmacies(),
      drugDetails: this.drugDetails(),
      pharmacySelectionConfirmed: this.pharmacySelectionConfirmed(),
      returnRoute: this.returnRoute(),
      messages: this.messages(),
      currentStep: this.currentStep(),
      analysisStepSchemaVersion: DrugStateService.ANALYSIS_STEP_SCHEMA_VERSION,
      prescriptionName: this.prescriptionName(),
      partDPlans: this.partDPlans(),
      medigapQuotes: this.medigapQuotes(),
      maPlans: this.maPlans(),
      selectedPartDPlan: this.selectedPartDPlan(),
      selectedMedigapPlan: this.selectedMedigapPlan(),
      selectedMAPlan: this.selectedMAPlan(),
      selectedMAGapPartDPlan: this.selectedMAGapPartDPlan(),
      activeSection: this.activeSection(),
      confirmedDrugNames: Array.from(this.confirmedDrugNames()),
      pendingCostRunRecommendationName: this.pendingCostRunRecommendationName(),
      costProjection: this.costProjection(),
    };
    try {
      sessionStorage.setItem(DrugStateService.STORAGE_KEY, JSON.stringify(snapshot));
    } catch { /* quota exceeded — silently skip */ }
  }

  /**
   * Backfills `confirmedDrugNames` from session when the in-memory signal is empty
   * (e.g. full reload with old snapshots, or fp-drugs step not mounted yet).
   */
  hydrateConfirmedFromSessionStorage(): void {
    if (this.confirmedDrugNames().size > 0) return;
    try {
      const raw = sessionStorage.getItem(DrugStateService.FP_CONFIRMED_DRUGS_SESSION_KEY);
      if (!raw) return;
      const arr: string[] = JSON.parse(raw);
      if (Array.isArray(arr) && arr.length > 0) {
        this.confirmedDrugNames.set(new Set(arr));
      }
    } catch { /* ignore */ }
  }

  // ─── Messages ──────────────────────────────────────────────────

  addUserMessage(content: string) {
    this.runtimePersistenceStarted = true;
    const context = this.router.url;
    this.messages.update(msgs => [...msgs, { role: 'user', content, timestamp: new Date(), context }]);
    this.persist();
    this.syncMessagesToServer();
  }

  addAssistantMessage(content: string) {
    const context = this.router.url;
    this.messages.update(msgs => [...msgs, { role: 'assistant', content, timestamp: new Date(), context }]);
    this.persist();
    this.syncMessagesToServer();
  }

  replaceLastAssistantMessage(content: string) {
    const context = this.router.url;
    this.messages.update(msgs => {
      let idx = -1;
      for (let i = msgs.length - 1; i >= 0; i--) {
        if (msgs[i].role === 'assistant') { idx = i; break; }
      }
      if (idx >= 0) {
        const updated = [...msgs];
        updated[idx] = { ...updated[idx], content, timestamp: new Date(), context };
        return updated;
      }
      return [...msgs, { role: 'assistant', content, timestamp: new Date(), context }];
    });
    this.persist();
    this.syncMessagesToServer();
  }

  addSystemMessage(content: string) {
    const context = this.router.url;
    this.messages.update(msgs => [...msgs, { role: 'system', content, timestamp: new Date(), context }]);
    this.persist();
    this.syncMessagesToServer();
  }

  removeAssistantMessagesContaining(text: string) {
    if (!text?.trim()) return;
    this.messages.update(msgs =>
      msgs.filter(m => !(m.role === 'assistant' && m.content.includes(text)))
    );
    this.persist();
    this.syncMessagesToServer();
  }

  hydrateMessagesFromServer(messages: ChatMessage[]) {
    this.messages.set(messages);
    this.persist();
  }

  setLoading(loading: boolean) {
    this.isLoading.set(loading);
  }

  /** Persists recommendation name for the upcoming / in-flight cost run (Mongo auto-save on cost page). */
  setPendingCostRunRecommendationName(name: string | null) {
    this.pendingCostRunRecommendationName.set(name?.trim() ? name.trim() : null);
    this.persist();
  }

  setSavingCurrentPrescription(saving: boolean) {
    this.isSavingCurrentPrescription.set(saving);
  }

  setCostProjection(result: EvaluateCostsResponse | null) {
    this.costProjection.set(result);
    this.persist();
  }

  setPharmacyLookup(result: PharmacyLookupResponse | null) {
    this.pharmacyLookup.set(result);
    this.persist();
  }

  setPharmacyLookupLoading(loading: boolean) {
    this.isPharmacyLookupLoading.set(loading);
  }

  setDrugDetails(result: BulkDrugSearchResponse | null) {
    this.drugDetails.set(result);
    this.persist();
  }

  setDrugDetailsLoading(loading: boolean) {
    this.isDrugDetailsLoading.set(loading);
  }

  /** Toggle a lookup pharmacy selection (max 5). Returns false if limit reached. */
  toggleLookupPharmacy(pharmacy: PharmacyLookupEntry): boolean {
    const current = this.selectedLookupPharmacies();
    const idx = current.findIndex(p => String(p.pharmacyNumber) === String(pharmacy.pharmacyNumber));
    if (idx >= 0) {
      this.selectedLookupPharmacies.set(current.filter((_, i) => i !== idx));
      this.addSystemMessage(`Deselected pharmacy: ${pharmacy.pharmacyName}`);
      this.persist();
      return true;
    }
    if (current.length >= 5) return false;
    this.selectedLookupPharmacies.set([...current, pharmacy]);
    this.addSystemMessage(`Selected pharmacy: ${pharmacy.pharmacyName}`);
    this.persist();
    return true;
  }

  isLookupPharmacySelected(pharmacyNumber: string): boolean {
    return this.selectedLookupPharmacies().some(p => String(p.pharmacyNumber) === String(pharmacyNumber));
  }

  setDrugSuggestions(suggestions: DrugNameSuggestion[]) {
    this.drugSuggestions.set(suggestions);
  }

  setVerifyingNames(verifying: boolean) {
    this.isVerifyingNames.set(verifying);
  }

  clearSuggestions() {
    this.drugSuggestions.set([]);
    this.isVerifyingNames.set(false);
  }

  // ─── Plan Recommendation Setters ────────────────────────────

  setPartDPlans(result: PartDPlanRecommendationResponse | null) { this.partDPlans.set(result); this.persist(); }
  setPartDLoading(v: boolean) { this.isPartDLoading.set(v); }

  setMedigapQuotes(result: MedigapPlanQuotesResponse | null) { this.medigapQuotes.set(result); this.persist(); }
  setMedigapLoading(v: boolean) { this.isMedigapLoading.set(v); }

  setMAPlans(result: MedicareAdvantagePlanResponse | null) { this.maPlans.set(result); this.persist(); }
  setMALoading(v: boolean) { this.isMALoading.set(v); }

  selectPartDPlan(plan: RecommendationListItem | null) { this.selectedPartDPlan.set(plan); this.persist(); }
  selectMedigapPlan(plan: MedigapPlan | null) { this.selectedMedigapPlan.set(plan); this.persist(); }
  selectMAPlan(plan: RecommendationListItem | null) { this.selectedMAPlan.set(plan); this.persist(); }
  selectMAGapPartDPlan(plan: RecommendationListItem | null) { this.selectedMAGapPartDPlan.set(plan); this.persist(); }

  setActiveSection(section: 'partd' | 'ma' | null) { this.activeSection.set(section); this.persist(); }

  /** Reset all FP plan selections when switching sections */
  resetPlanSelections() {
    this.selectedPartDPlan.set(null);
    this.selectedMedigapPlan.set(null);
    this.selectedMAPlan.set(null);
    this.selectedMAGapPartDPlan.set(null);
    this.persist();
  }

  /** Persist current analysis/session state to session storage. */
  persistSelections() {
    this.runtimePersistenceStarted = true;
    this.persist();
  }

  /** Clear all analysis state and sessionStorage — starts a fresh session. Preserves chat history. */
  resetAll() {
    this.runtimePersistenceStarted = false;
    this.selectedLookupPharmacies.set([]);
    this.pharmacySelectionConfirmed.set(false);
    this.pharmacyLookup.set(null);
    this.isPharmacyLookupLoading.set(false);
    this.costProjection.set(null);
    this.drugDetails.set(null);
    this.isDrugDetailsLoading.set(false);
    this.confirmedDrugNames.set(new Set());
    this.isLoading.set(false);
    this.isSavingCurrentPrescription.set(false);
    this.currentStep.set(1);
    this.returnRoute.set(null);
    this.drugSuggestions.set([]);
    this.isVerifyingNames.set(false);
    // FP Plan selections
    this.partDPlans.set(null);
    this.isPartDLoading.set(false);
    this.medigapQuotes.set(null);
    this.isMedigapLoading.set(false);
    this.maPlans.set(null);
    this.isMALoading.set(false);
    this.selectedPartDPlan.set(null);
    this.selectedMedigapPlan.set(null);
    this.selectedMAPlan.set(null);
    this.selectedMAGapPartDPlan.set(null);
    this.activeSection.set(null);
    this.pendingCostRunRecommendationName.set(null);
    this.pendingRunAnalysis.set(null);
    // Clear FP drugs step local sessionStorage keys
    sessionStorage.removeItem('formulation-selections');
    sessionStorage.removeItem('fp-drug-selections');
    sessionStorage.removeItem('drug-quantities');
    sessionStorage.removeItem(DrugStateService.FP_CONFIRMED_DRUGS_SESSION_KEY);

    // Persist the cleared state so stale state is not reused later.
    this.persist();

    this.addAssistantMessage('🔄 Analysis reset. You can start a new prescription analysis — enter your drugs in the chat below.');

    // Signal wizard to reset
    this.wizardResetTrigger.update(v => v + 1);
  }

  /**
   * Invalidate downstream analysis state after impactful profile changes.
   * Keeps selected/confirmed drugs, but clears pharmacy, plans, and projections.
   */
  invalidateAfterProfileChange() {
    this.selectedLookupPharmacies.set([]);
    this.pharmacySelectionConfirmed.set(false);
    this.pharmacyLookup.set(null);
    this.isPharmacyLookupLoading.set(false);

    this.partDPlans.set(null);
    this.isPartDLoading.set(false);
    this.medigapQuotes.set(null);
    this.isMedigapLoading.set(false);
    this.maPlans.set(null);
    this.isMALoading.set(false);
    this.selectedPartDPlan.set(null);
    this.selectedMedigapPlan.set(null);
    this.selectedMAPlan.set(null);
    this.selectedMAGapPartDPlan.set(null);
    this.activeSection.set(null);

    this.costProjection.set(null);
    this.pendingCostRunRecommendationName.set(null);

    this.persist();
  }

  /**
   * Hard clear used by logout: wipes in-memory analysis/chat state and
   * removes persisted analysis keys without adding system/assistant messages.
   */
  clearForSignOut() {
    this.runtimePersistenceStarted = false;
    this.selectedLookupPharmacies.set([]);
    this.pharmacySelectionConfirmed.set(false);
    this.pharmacyLookup.set(null);
    this.isPharmacyLookupLoading.set(false);
    this.costProjection.set(null);
    this.drugDetails.set(null);
    this.isDrugDetailsLoading.set(false);
    this.confirmedDrugNames.set(new Set());
    this.isLoading.set(false);
    this.isSavingCurrentPrescription.set(false);
    this.currentStep.set(1);
    this.returnRoute.set(null);
    this.drugSuggestions.set([]);
    this.isVerifyingNames.set(false);
    this.pendingDrugSelection.set(null);
    this.pendingDrugFollowupPrompt.set(null);
    this.pendingPharmacySelection.set(null);
    this.pendingPlanSelection.set(null);
    this.pendingRunAnalysis.set(null);
    this.pendingCostRunRecommendationName.set(null);
    this.messages.set([]);
    this.prescriptionName.set(null);

    this.partDPlans.set(null);
    this.isPartDLoading.set(false);
    this.medigapQuotes.set(null);
    this.isMedigapLoading.set(false);
    this.maPlans.set(null);
    this.isMALoading.set(false);
    this.selectedPartDPlan.set(null);
    this.selectedMedigapPlan.set(null);
    this.selectedMAPlan.set(null);
    this.selectedMAGapPartDPlan.set(null);
    this.activeSection.set(null);

    sessionStorage.removeItem(DrugStateService.STORAGE_KEY);
    sessionStorage.removeItem('formulation-selections');
    sessionStorage.removeItem('fp-drug-selections');
    sessionStorage.removeItem('drug-quantities');
    sessionStorage.removeItem(DrugStateService.FP_CONFIRMED_DRUGS_SESSION_KEY);

    // Ensure chat wizard mode/step state is reset on next mount after sign-out.
    this.wizardResetTrigger.update(v => v + 1);
  }

  /**
   * Debounced SignalR sync — collapses rapid message bursts (e.g., greeting +
   * profile review on Medicare startup) into a single WebSocket invoke.
   * 500 ms is generous enough to absorb any synchronous burst while still
   * flushing well within one second of the last mutation.
   */
  private syncMessagesToServer() {
    if (!sessionStorage.getItem('auth_token')) return;
    if (this.syncTimer !== null) clearTimeout(this.syncTimer);
    this.syncTimer = setTimeout(() => {
      this.syncTimer = null;
      const payload = this.messages().map(m => ({
        role: m.role,
        content: m.content,
        timestamp: m.timestamp.toISOString(),
        context: m.context,
      }));
      this.chatSignalR.syncMessages(payload);
    }, 500);
  }
}
