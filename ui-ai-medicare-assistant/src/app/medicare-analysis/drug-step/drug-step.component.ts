import { Component, ChangeDetectionStrategy, inject, OnInit, computed, signal, ViewChildren, QueryList, effect, Injector } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatExpansionModule, MatExpansionPanel } from '@angular/material/expansion';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MedicareStateService, ChatDrugSelectionCommand } from '../../services/drug-state.service';
import { ChatRouterService } from '../../services/chat-router.service';
import { DrugService } from '../../services/drug.service';
import { ChatDrugFlowService } from '../../services/chat-drug-flow.service';
import { ChatAnalysisSelectionHydrationService } from '../../services/chat-analysis-selection-hydration.service';
import { PrescriptionDrugDto } from '../../services/prescription.service';
import { DrugSearchResult, DrugDetailAdvanceItem, DrugNameSuggestion } from '../../models/drug.model';
import { InteractionAlertsComponent } from './interaction-alerts/interaction-alerts.component';
import { DuplicateTherapyAlertsComponent } from './duplicate-therapy-alerts/duplicate-therapy-alerts.component';
import { DrugSelectionPanelComponent, DrugSelectionState } from './drug-selection-panel/drug-selection-panel.component';
import { SelectedDrugsSummaryComponent } from './selected-drugs-summary/selected-drugs-summary.component';

export interface FormulationSelection {
  drugName: string;
  selectedFormulation: DrugDetailAdvanceItem | null;
}

export interface ConfirmedDrug {
  drugName: string;
  formulation: DrugDetailAdvanceItem;
  quantity: number | null;
}

@Component({
  selector: 'app-drugs-step',
  templateUrl: './drug-step.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [
    FormsModule,
    MatIconModule, MatButtonModule, MatExpansionModule, MatProgressSpinnerModule,
    MatFormFieldModule, MatInputModule,
    InteractionAlertsComponent, DuplicateTherapyAlertsComponent,
    DrugSelectionPanelComponent, SelectedDrugsSummaryComponent,
  ],
})
export class DrugsStepComponent implements OnInit {
  protected state = inject(MedicareStateService);
  private drugService = inject(DrugService);
  private chatDrugFlow = inject(ChatDrugFlowService);
  private chatRouter = inject(ChatRouterService);
  private selectionHydrator = inject(ChatAnalysisSelectionHydrationService);
  private snackBar = inject(MatSnackBar);
  private savedDrugs = signal<PrescriptionDrugDto[]>([]);
  private injector = inject(Injector);
  protected drugInput = '';

  constructor() {
    // Watch for chat-driven drug formulation selection
    effect(() => {
      const cmd = this.state.pendingDrugSelection();
      if (!cmd) return;
      this.state.pendingDrugSelection.set(null);
      this.applyChatDrugSelection(cmd);
    });
  }

  @ViewChildren(MatExpansionPanel) panels!: QueryList<MatExpansionPanel>;

  /** Per-drug formulation selection map (drugName → selected formulation) */
  readonly formulationSelections = signal<Map<string, DrugDetailAdvanceItem>>(new Map());

  /** Per-drug step-by-step selection state */
  readonly drugSelections = signal<Map<string, DrugSelectionState>>(new Map());

  /** Per-drug quantity per month */
  readonly drugQuantities = signal<Map<string, number>>(new Map());

  /** Confirmed drug names (shared via DrugStateService for shell navigation gating) */
  get confirmedDrugNames() { return this.state.confirmedDrugNames; }

  readonly results = computed(() => this.state.drugDetails()?.results ?? []);
  readonly interactions = computed(() => this.state.drugDetails()?.interactions ?? []);
  readonly duplicateTherapies = computed(() => this.state.drugDetails()?.duplicateTherapies ?? []);

  readonly allSelected = computed(() => {
    const r = this.results();
    const sel = this.formulationSelections();
    if (r.length === 0) return false;
    return r.every(result => {
      if (!result.detail?.drugDetailAdvanceList?.length) return true; // skip drugs without detail
      return sel.has(result.drugName);
    });
  });

  readonly selectedCount = computed(() => this.formulationSelections().size);

  readonly confirmedCount = computed(() => this.confirmedDrugNames().size);

  readonly confirmedDrugsList = computed<ConfirmedDrug[]>(() => {
    const confirmed = this.confirmedDrugNames();
    const formulations = this.formulationSelections();
    const quantities = this.drugQuantities();
    return this.results()
      .filter(r => confirmed.has(r.drugName))
      .map(r => ({
        drugName: r.drugName,
        formulation: formulations.get(r.drugName) ?? this.fallbackFormulation(r.drugName),
        quantity: quantities.get(r.drugName) ?? null,
      }));
  });

  readonly hasAnyConfirmed = computed(() => this.confirmedCount() > 0);

  private fallbackFormulation(drugName: string): DrugDetailAdvanceItem {
    const fromDetail = this.results()
      .find(r => r.drugName === drugName)
      ?.detail?.drugDetailAdvanceList?.[0];
    if (fromDetail) return fromDetail;
    return {
      drugName,
      rxcui: '',
      genericDrugName: '',
      genericRxcui: '',
      newDoseForm: '',
      rxnDoseForm: '',
      strength: '',
      brandName: '',
      prescription: true,
      drugType: '',
    };
  }

  getCompletionCount(drugName: string): number {
    const sel = this.getDrugSelection(drugName);
    let count = 0;
    if (sel.type) count++;
    if (sel.dosageForm) count++;
    if (sel.strength) count++;
    if (this.getDrugQuantity(drugName)) count++;
    return count;
  }

  ngOnInit() {
    this.state.currentStep.set(2);

    // Restore selections from state if available
    this.restoreSelections();
    this.state.hydrateConfirmedFromSessionStorage();

    // Auto-fetch if not already loaded (important after hard refresh).
    if (!this.state.hasDrugDetails() && !this.state.isDrugDetailsLoading()) {
      this.fetchFpDrugDetailsFromConfirmedNames();
    }
    this.loadSavedDrugsAndHydrate();

    // Re-try auto-fill once async drug details OR saved drugs arrive.
    // Tracking both ensures hydration runs regardless of which resolves first.
    effect(() => {
      this.results();
      this.savedDrugs();
      this.hydrateSelectionsFromSavedDrugs();
    }, { injector: this.injector });

    // If confirmed drugs exist in the service signal but not in sessionStorage,
    // persist them so they survive subsequent re-navigations.
    if (this.confirmedDrugNames().size > 0 && !sessionStorage.getItem(MedicareStateService.FP_CONFIRMED_DRUGS_SESSION_KEY)) {
      this.persistSelections();
    }
  }

  private loadSavedDrugsAndHydrate(): void {
    this.selectionHydrator.getSavedDrugs$().subscribe({
      next: (drugs) => {
        this.savedDrugs.set(drugs);
        this.hydrateSelectionsFromSavedDrugs();
      },
      error: () => {},
    });
  }

  private hydrateSelectionsFromSavedDrugs(): void {
    const saved = this.savedDrugs();
    if (saved.length === 0) return;
    const confirmed = this.confirmedDrugNames();
    if (confirmed.size === 0) return;

    let changed = false;
    const nextSelections = new Map(this.drugSelections());
    const nextFormulations = new Map(this.formulationSelections());
    const nextQuantities = new Map(this.drugQuantities());

    for (const result of this.results()) {
      const name = result.drugName;
      if (!confirmed.has(name)) continue;

      const savedDrug = this.findSavedDrug(saved, name);

      // Always restore quantity from the saved drug when not already set locally.
      // This must run before the formulation guard so it is never skipped.
      if (savedDrug && !nextQuantities.has(name)) {
        const qty = savedDrug.quantityPerMonth ?? 0;
        if (qty > 0) {
          nextQuantities.set(name, qty);
          changed = true;
        }
      }

      if (nextFormulations.has(name)) continue; // Do not overwrite existing manual formulation picks

      const options = result.detail?.drugDetailAdvanceList ?? [];
      if (!savedDrug || options.length === 0) continue;

      const picked = this.pickBestFormulation(options, savedDrug);
      nextSelections.set(name, {
        type: picked.drugType || null,
        dosageForm: (picked.rxnDoseForm || picked.newDoseForm || '').trim() || null,
        strength: (picked.strength || '').trim() || null,
      });
      nextFormulations.set(name, picked);
      changed = true;
    }

    if (!changed) return;
    this.drugSelections.set(nextSelections);
    this.formulationSelections.set(nextFormulations);
    this.drugQuantities.set(nextQuantities);
    this.persistSelections();
  }

  private findSavedDrug(saved: PrescriptionDrugDto[], drugName: string): PrescriptionDrugDto | null {
    const lower = drugName.toLowerCase();
    // Check all name fields independently: normalizedDrugName (always the API drug name),
    // drugInput (what the user typed), and selectedName (which may be a brand name).
    return saved.find((d) =>
      (d.normalizedDrugName ?? '').trim().toLowerCase() === lower ||
      (d.drugInput ?? '').trim().toLowerCase() === lower ||
      (d.selectedName ?? '').trim().toLowerCase() === lower
    ) ?? null;
  }

  private pickBestFormulation(options: DrugDetailAdvanceItem[], saved: PrescriptionDrugDto): DrugDetailAdvanceItem {
    const savedStrength = (saved.strength ?? '').toLowerCase();
    const savedForm = (saved.dosageForm ?? '').toLowerCase();
    if (!savedStrength && !savedForm) return options[0];

    const withStrengthAndForm = options.find((o) => {
      const strength = (o.strength ?? '').toLowerCase();
      const form = ((o.rxnDoseForm || o.newDoseForm || '') ?? '').toLowerCase();
      return (!!savedStrength && !!strength && strength === savedStrength) &&
             (!!savedForm && !!form && form === savedForm);
    });
    if (withStrengthAndForm) return withStrengthAndForm;

    const withStrength = options.find((o) => {
      const strength = (o.strength ?? '').toLowerCase();
      return !!savedStrength && !!strength && strength === savedStrength;
    });
    if (withStrength) return withStrength;

    const withForm = options.find((o) => {
      const form = ((o.rxnDoseForm || o.newDoseForm || '') ?? '').toLowerCase();
      return !!savedForm && !!form && form === savedForm;
    });
    return withForm ?? options[0];
  }

  private fetchFpDrugDetailsFromConfirmedNames() {
    const names = Array.from(this.confirmedDrugNames()).map((n) => n.trim()).filter(Boolean);
    if (names.length === 0) return;

    this.state.setDrugDetailsLoading(true);
    this.drugService.searchDrugsBulk(names).subscribe({
      next: (response) => {
        this.state.setDrugDetails(response);
        this.state.setDrugDetailsLoading(false);
      },
      error: () => {
        this.state.setDrugDetailsLoading(false);
        this.snackBar.open('Failed to reload saved drugs. Please try again.', 'OK', { duration: 4000 });
      }
    });
  }

  selectFormulation(drugName: string, item: DrugDetailAdvanceItem) {
    const current = this.formulationSelections();
    const next = new Map(current);
    next.set(drugName, item);
    this.formulationSelections.set(next);
    this.persistSelections();
  }

  getSelectedFormulation(drugName: string): DrugDetailAdvanceItem | null {
    return this.formulationSelections().get(drugName) ?? null;
  }

  // ── Step-by-step selection (called from child panel outputs) ──

  getDrugSelection(drugName: string): DrugSelectionState {
    return this.drugSelections().get(drugName) ?? { type: null, dosageForm: null, strength: null };
  }

  getDrugQuantity(drugName: string): number | null {
    return this.drugQuantities().get(drugName) ?? null;
  }

  setDrugType(drugName: string, type: string) {
    const next = new Map(this.drugSelections());
    next.set(drugName, { type, dosageForm: null, strength: null });
    this.drugSelections.set(next);
    this.clearFormulationSelection(drugName);
  }

  setDrugDosageForm(drugName: string, form: string) {
    const next = new Map(this.drugSelections());
    const current = this.getDrugSelection(drugName);
    next.set(drugName, { ...current, dosageForm: form, strength: null });
    this.drugSelections.set(next);
    this.clearFormulationSelection(drugName);
  }

  setDrugStrength(drugName: string, strength: string) {
    const next = new Map(this.drugSelections());
    const current = this.getDrugSelection(drugName);
    next.set(drugName, { ...current, strength });
    this.drugSelections.set(next);
    this.autoSelectFormulation(drugName);
  }

  setDrugQuantity(drugName: string, event: Event) {
    const value = parseInt((event.target as HTMLInputElement).value, 10);
    if (isNaN(value) || value < 1) return;
    const next = new Map(this.drugQuantities());
    next.set(drugName, value);
    this.drugQuantities.set(next);
    this.persistSelections();
  }

  setDrugQuantityValue(drugName: string, qty: number) {
    const next = new Map(this.drugQuantities());
    next.set(drugName, qty);
    this.drugQuantities.set(next);
    this.persistSelections();
  }

  private autoSelectFormulation(drugName: string) {
    const sel = this.getDrugSelection(drugName);
    const result = this.results().find(r => r.drugName === drugName);
    if (!result || !sel.type || !sel.dosageForm || !sel.strength) return;

    const formulations = result.detail?.drugDetailAdvanceList ?? [];
    const match = formulations.find(f =>
      f.drugType === sel.type &&
      (f.rxnDoseForm === sel.dosageForm || f.newDoseForm === sel.dosageForm) &&
      f.strength === sel.strength
    );

    if (match) {
      this.selectFormulation(drugName, match);
    }
  }

  private clearFormulationSelection(drugName: string) {
    const current = this.formulationSelections();
    if (current.has(drugName)) {
      const next = new Map(current);
      next.delete(drugName);
      this.formulationSelections.set(next);
      this.persistSelections();
    }
  }

  // ── Confirm / Edit / Remove ──

  isDrugConfirmed(drugName: string): boolean {
    return this.confirmedDrugNames().has(drugName) && this.formulationSelections().has(drugName);
  }

  confirmDrug(drugName: string) {
    const next = new Set(this.confirmedDrugNames());
    next.add(drugName);
    this.confirmedDrugNames.set(next);
    this.persistSelections();
    this.state.addSystemMessage(`Confirmed drug: ${drugName}`);

    // Close the confirmed panel and open the next unconfirmed one
    const panels = this.panels?.toArray() ?? [];
    const resultsList = this.results();
    const idx = resultsList.findIndex(r => r.drugName === drugName);
    if (idx >= 0 && idx < panels.length) {
      panels[idx].close();
    }
    for (let i = 0; i < resultsList.length; i++) {
      if (!next.has(resultsList[i].drugName) && i < panels.length) {
        panels[i].open();
        break;
      }
    }
  }

  editDrug(drugName: string) {
    const next = new Set(this.confirmedDrugNames());
    next.delete(drugName);
    this.confirmedDrugNames.set(next);
    this.persistSelections();

    const panels = this.panels?.toArray() ?? [];
    const idx = this.results().findIndex(r => r.drugName === drugName);
    if (idx >= 0 && idx < panels.length) {
      panels[idx].open();
    }
  }

  removeDrug(drugName: string) {
    this.state.addSystemMessage(`Removed drug: ${drugName}`);
    const nextConfirmed = new Set(this.confirmedDrugNames());
    nextConfirmed.delete(drugName);
    this.confirmedDrugNames.set(nextConfirmed);

    const nextFormulations = new Map(this.formulationSelections());
    nextFormulations.delete(drugName);
    this.formulationSelections.set(nextFormulations);

    const nextSel = new Map(this.drugSelections());
    nextSel.delete(drugName);
    this.drugSelections.set(nextSel);

    const nextQty = new Map(this.drugQuantities());
    nextQty.delete(drugName);
    this.drugQuantities.set(nextQty);

    this.persistSelections();
  }

  // ── Persistence ──

  private persistSelections() {
    const entries = Array.from(this.formulationSelections().entries());
    const selEntries = Array.from(this.drugSelections().entries());
    const qtyEntries = Array.from(this.drugQuantities().entries());
    const confirmedArr = Array.from(this.confirmedDrugNames());
    try {
      sessionStorage.setItem('formulation-selections', JSON.stringify(entries));
      sessionStorage.setItem('fp-drug-selections', JSON.stringify(selEntries));
      sessionStorage.setItem('drug-quantities', JSON.stringify(qtyEntries));
      sessionStorage.setItem(MedicareStateService.FP_CONFIRMED_DRUGS_SESSION_KEY, JSON.stringify(confirmedArr));
    } catch { /* quota exceeded */ }
    this.state.persistSelections();
  }

  private restoreSelections() {
    try {
      const raw = sessionStorage.getItem('formulation-selections');
      const nextFormulations = new Map<string, DrugDetailAdvanceItem>();
      if (raw) {
        const entries: [string, DrugDetailAdvanceItem][] = JSON.parse(raw);
        for (const [k, v] of entries) nextFormulations.set(k, v);
      }
      this.formulationSelections.set(nextFormulations);

      const nextSelections = new Map<string, DrugSelectionState>();
      const selRaw = sessionStorage.getItem('fp-drug-selections');
      if (selRaw) {
        const selEntries: [string, DrugSelectionState][] = JSON.parse(selRaw);
        for (const [k, v] of selEntries) nextSelections.set(k, v);
      }
      // Rebuild missing drugSelections entries from their stored formulation data.
      // This handles the case where formulationSelections was saved but drugSelections was not.
      for (const [name, formulation] of nextFormulations) {
        if (!nextSelections.has(name)) {
          nextSelections.set(name, {
            type: formulation.drugType || null,
            dosageForm: (formulation.rxnDoseForm || formulation.newDoseForm || '').trim() || null,
            strength: (formulation.strength || '').trim() || null,
          });
        }
      }
      this.drugSelections.set(nextSelections);

      const qtyRaw = sessionStorage.getItem('drug-quantities');
      if (qtyRaw) {
        const qtyEntries: [string, number][] = JSON.parse(qtyRaw);
        this.drugQuantities.set(new Map(qtyEntries));
      }
      // Only restore confirmed drugs from sessionStorage if the service signal is empty.
      // When loading from saved data, the signal is already populated — don't overwrite it.
      const confirmedRaw = sessionStorage.getItem(MedicareStateService.FP_CONFIRMED_DRUGS_SESSION_KEY);
      if (confirmedRaw && this.confirmedDrugNames().size === 0) {
        const confirmedArr: string[] = JSON.parse(confirmedRaw);
        this.confirmedDrugNames.set(new Set(confirmedArr));
      }
    } catch { /* corrupt data */ }
  }

  // ── Chat-Driven Drug Selection ─────────────────────────────────────────────

  private applyChatDrugSelection(cmd: ChatDrugSelectionCommand): void {
    if (cmd.action === 'confirm_all') {
      this.confirmAllReadyDrugs();
      return;
    }

    if (cmd.action === 'remove' && cmd.drugName) {
      const name = this.findMatchingDrugName(cmd.drugName);
      if (name) this.removeDrug(name);
      return;
    }

    if (cmd.action === 'edit' && cmd.drugName) {
      const name = this.findMatchingDrugName(cmd.drugName);
      if (name) this.editDrug(name);
      return;
    }

    // action === 'select' or 'options' (options just shows info via reply, no state change)
    if (cmd.action !== 'select') return;

    const drugNames = cmd.drugName
      ? [this.findMatchingDrugName(cmd.drugName)]
      : this.results().map(r => r.drugName); // apply to all if no specific drug

    for (const name of drugNames) {
      if (!name) continue;
      this.applySelectionToDrug(name, cmd);
    }
  }

  private findMatchingDrugName(input: string): string | null {
    const lower = input.toLowerCase();
    const match = this.results().find(r => r.drugName.toLowerCase().includes(lower));
    return match?.drugName ?? null;
  }

  private applySelectionToDrug(
    drugName: string,
    cmd: { type: string | null; dosageForm: string | null; strength: string | null; quantity: number | null }
  ): void {
    const result = this.results().find(r => r.drugName === drugName);
    if (!result?.detail?.drugDetailAdvanceList?.length) return;
    const formulations = result.detail.drugDetailAdvanceList;

    // Apply type
    if (cmd.type) {
      this.setDrugType(drugName, cmd.type);
    }

    // If type is missing, auto-pick only when there is exactly one available type.
    let selection = this.getDrugSelection(drugName);
    if (!selection.type) {
      const uniqueTypes = [...new Set(formulations.map(f => f.drugType).filter(Boolean))];
      if (uniqueTypes.length === 1) {
        this.setDrugType(drugName, uniqueTypes[0]);
        selection = this.getDrugSelection(drugName);
      }
    }

    // Apply dosage form (fuzzy match against available forms for the selected type)
    if (cmd.dosageForm) {
      const type = selection.type;
      if (type) {
        const available = formulations.filter(f => f.drugType === type);
        const matchedForm = this.fuzzyMatchForm(cmd.dosageForm, available);
        if (matchedForm) {
          this.setDrugDosageForm(drugName, matchedForm);
          selection = this.getDrugSelection(drugName);
        }
      }
    }

    // If dosage form is still missing, infer it when uniquely determinable.
    if (!selection.dosageForm && selection.type) {
      const typed = formulations.filter(f => f.drugType === selection.type);
      const forms = [...new Set(typed.map(f => f.rxnDoseForm || f.newDoseForm).filter(Boolean))];

      // If strength is provided, infer the form by matching strength candidates.
      if (cmd.strength) {
        const strengthMatch = this.fuzzyMatchStrength(cmd.strength, typed);
        if (strengthMatch) {
          const formsForStrength = [...new Set(
            typed
              .filter(f => f.strength === strengthMatch)
              .map(f => f.rxnDoseForm || f.newDoseForm)
              .filter(Boolean)
          )];
          if (formsForStrength.length === 1) {
            this.setDrugDosageForm(drugName, formsForStrength[0]);
            selection = this.getDrugSelection(drugName);
          }
        }
      }

      // If still not selected, auto-pick only when single available form.
      if (!selection.dosageForm && forms.length === 1) {
        this.setDrugDosageForm(drugName, forms[0]);
        selection = this.getDrugSelection(drugName);
      }
    }

    // Apply strength (fuzzy match)
    if (cmd.strength) {
      const type = selection.type;
      const form = selection.dosageForm;
      if (type) {
        const available = form
          ? formulations.filter(f =>
              f.drugType === type && (f.rxnDoseForm === form || f.newDoseForm === form)
            )
          : formulations.filter(f => f.drugType === type);
        const matchedStr = this.fuzzyMatchStrength(cmd.strength, available);
        if (matchedStr) {
          this.setDrugStrength(drugName, matchedStr);
          selection = this.getDrugSelection(drugName);
        }
      }
    }

    // If strength missing, auto-pick only when exactly one strength is available for chosen type+form.
    if (!selection.strength && selection.type && selection.dosageForm) {
      const filtered = formulations.filter(f =>
        f.drugType === selection.type &&
        (f.rxnDoseForm === selection.dosageForm || f.newDoseForm === selection.dosageForm)
      );
      const uniqueStrengths = [...new Set(filtered.map(f => f.strength).filter(Boolean))];
      if (uniqueStrengths.length === 1) {
        this.setDrugStrength(drugName, uniqueStrengths[0]);
        selection = this.getDrugSelection(drugName);
      }
    }

    // Apply quantity
    if (cmd.quantity && cmd.quantity > 0) {
      this.setDrugQuantityValue(drugName, cmd.quantity);
    }

    // Auto-confirm if all 4 selections are complete
    const sel = this.getDrugSelection(drugName);
    const qty = this.getDrugQuantity(drugName);
    const hasFormulation = this.formulationSelections().has(drugName);
    if (sel.type && sel.dosageForm && sel.strength && hasFormulation && qty && qty > 0) {
      this.confirmDrug(drugName);
      this.state.pendingDrugFollowupPrompt.set(null);
      this.chatRouter.clearPendingDrugChatCards();
      this.state.addAssistantMessage(`Selected and confirmed **${drugName}**.`);
      return;
    }

    const missing = this.getMissingSelectionParts(drugName);
    if (missing.length > 0) {
      this.state.pendingDrugFollowupPrompt.set({ drugName, missingFields: missing });
      const cards = this.chatRouter.pendingDrugChatCards();
      if (cards?.drugName !== drugName) {
        this.state.addAssistantMessage(
          `I applied available values for **${drugName}**. Please provide: **${missing.join(', ')}**.`
        );
      }
    }
  }

  private getMissingSelectionParts(drugName: string): string[] {
    const sel = this.getDrugSelection(drugName);
    const qty = this.getDrugQuantity(drugName);
    const missing: string[] = [];
    if (!sel.type) missing.push('type (Generic or Branded)');
    if (!sel.dosageForm) missing.push('dosage form');
    if (!sel.strength) missing.push('strength');
    if (!qty || qty <= 0) missing.push('quantity per month');
    return missing;
  }

  private fuzzyMatchForm(input: string, formulations: DrugDetailAdvanceItem[]): string | null {
    const lower = input.toLowerCase();
    const forms = [...new Set(formulations.map(f => f.rxnDoseForm || f.newDoseForm).filter(Boolean))];
    // Exact match first
    const exact = forms.find(f => f.toLowerCase() === lower);
    if (exact) return exact;
    // Partial match
    const partial = forms.find(f => f.toLowerCase().includes(lower) || lower.includes(f.toLowerCase()));
    return partial ?? null;
  }

  private fuzzyMatchStrength(input: string, formulations: DrugDetailAdvanceItem[]): string | null {
    const normalized = input.replace(/\s+/g, ' ').toUpperCase().replace('MG', 'MG');
    const strengths = [...new Set(formulations.map(f => f.strength).filter(Boolean))];
    // Exact match
    const exact = strengths.find(s => s.toUpperCase() === normalized);
    if (exact) return exact;
    // Normalize both sides (strip spaces)
    const stripped = normalized.replace(/\s/g, '');
    const match = strengths.find(s => s.replace(/\s/g, '').toUpperCase() === stripped);
    return match ?? null;
  }

  private confirmAllReadyDrugs(): void {
    for (const result of this.results()) {
      const name = result.drugName;
      const sel = this.getDrugSelection(name);
      const qty = this.getDrugQuantity(name);
      const hasFormulation = this.formulationSelections().has(name);
      if (sel.type && sel.dosageForm && sel.strength && hasFormulation && qty && qty > 0) {
        if (!this.confirmedDrugNames().has(name)) {
          this.confirmDrug(name);
        }
      }
    }
  }

  // ── FP-page drug search (same capability as chat) ────────────────────────

  protected searchFromPage(): void {
    const text = this.drugInput.trim();
    if (!text || this.state.isLoading() || this.state.isVerifyingNames()) return;
    this.chatDrugFlow.runDrugFlow(text);
    this.drugInput = '';
  }

  protected selectCandidate(suggestion: DrugNameSuggestion, candidateName: string): void {
    this.chatDrugFlow.selectCandidate(suggestion, candidateName);
  }

  protected isSelected(suggestion: DrugNameSuggestion, candidateName: string): boolean {
    return this.chatDrugFlow.isSelected(suggestion, candidateName);
  }

  protected allCandidatesSelected(): boolean {
    return this.chatDrugFlow.allSelected();
  }

  protected confirmAndAnalyze(): void {
    this.chatDrugFlow.confirmAndAnalyze();
  }

  protected cancelSuggestions(): void {
    this.chatDrugFlow.cancelSuggestions();
  }
}
