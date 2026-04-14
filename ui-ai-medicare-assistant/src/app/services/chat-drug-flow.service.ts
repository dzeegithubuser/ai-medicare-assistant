import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { DrugService } from './drug.service';
import { DrugStateService } from './drug-state.service';
import { ProfileService } from './profile.service';
import { DrugNameSuggestion } from '../models/drug.model';
import { DRUG_MESSAGES } from '../constants/chat-messages';
import { AppRoutes } from '../app-routes.const';

/**
 * Handles the drug name suggestion → verification → bulk search flow,
 * extracted from ChatComponent.
 */
@Injectable({ providedIn: 'root' })
export class ChatDrugFlowService {
  private drugService    = inject(DrugService);
  private state          = inject(DrugStateService);
  private profileService = inject(ProfileService);
  private router         = inject(Router);

  /** Tracks which candidate is selected per input drug name. */
  readonly selectedNames = new Map<string, string>();

  /**
   * Start the drug suggestion flow — calls AI to identify drug names,
   * then shows the verification panel.
   */
  runDrugFlow(text: string): void {
    if (!this.profileService.isProfileComplete()) {
      this.state.addAssistantMessage(
        DRUG_MESSAGES.NEED_PROFILE_FIRST
      );
      this.router.navigate([AppRoutes.abs.PROFILE]);
      return;
    }

    this.state.setLoading(true);

    this.drugService.suggestNames(text).subscribe({
      next: (result) => {
        const suggestions = result.suggestions ?? [];
        if (suggestions.length === 0) {
          this.state.addAssistantMessage(
            DRUG_MESSAGES.NO_RECOGNIZABLE_DRUGS
          );
          this.state.setLoading(false);
          return;
        }

        this.selectedNames.clear();
        for (const s of suggestions) {
          if (s.candidates.length === 1) {
            this.selectedNames.set(s.inputName, s.candidates[0].name);
          } else if (s.candidates.length > 1 && s.candidates[0].confidence >= 0.95) {
            this.selectedNames.set(s.inputName, s.candidates[0].name);
          }
        }

        this.state.setDrugSuggestions(suggestions);
        this.state.setVerifyingNames(true);
        this.state.addAssistantMessage(
          `I found ${suggestions.length} drug${suggestions.length !== 1 ? 's' : ''} in your input. ` +
          `Please verify the correct name for each drug below, then click "Confirm & Analyze".`
        );
        this.state.setLoading(false);
      },
      error: () => {
        this.state.addAssistantMessage(DRUG_MESSAGES.IDENTIFY_ERROR);
        this.state.setLoading(false);
      },
    });
  }

  // ── Suggestion UI helpers ───────────────────────────────────────────────

  selectCandidate(suggestion: DrugNameSuggestion, candidateName: string): void {
    this.selectedNames.set(suggestion.inputName, candidateName);
  }

  isSelected(suggestion: DrugNameSuggestion, candidateName: string): boolean {
    return this.selectedNames.get(suggestion.inputName) === candidateName;
  }

  allSelected(): boolean {
    return this.state.drugSuggestions().every(s =>
      s.candidates.length === 0 || this.selectedNames.has(s.inputName)
    );
  }

  confirmAndAnalyze(): void {
    if (!this.allSelected() || this.state.isLoading()) return;

    const newlyConfirmedNames = this.state.drugSuggestions()
      .filter(s => this.selectedNames.has(s.inputName))
      .map(s => this.selectedNames.get(s.inputName)!);

    const prescription = newlyConfirmedNames.join(', ');
    this.state.clearSuggestions();
    this.selectedNames.clear();
    this.state.setLoading(true);
    this.state.addUserMessage(`Confirmed drugs: ${prescription}`);

    // Preserve existing FP drugs and recalculate details for the full combined set.
    const existingNames = (this.state.drugDetails()?.results ?? []).map(r => r.drugName);
    const allNames = Array.from(new Set([...existingNames, ...newlyConfirmedNames]));

    this.state.setDrugDetailsLoading(true);
    this.drugService.searchDrugsBulk(allNames).subscribe({
      next: (response) => {
        const results = response.results ?? [];
        if (results.length === 0) {
          this.state.setDrugDetails(null);
          this.state.addAssistantMessage(
            DRUG_MESSAGES.DETAILS_NOT_FOUND
          );
        } else {
          this.state.setDrugDetails(response);
          const addedCount = allNames.length - existingNames.length;
          const interactions = response.interactions ?? [];
          const duplicates   = response.duplicateTherapies ?? [];
          const parts: string[] = [
            `Drug details retrieved — ${results.length} drug${results.length !== 1 ? 's' : ''} found.`
          ];
          if (existingNames.length > 0 && addedCount > 0) {
            parts.push(`Added ${addedCount} new drug${addedCount !== 1 ? 's' : ''} and kept your previous selections.`);
          }
          if (interactions.length > 0) {
            const highCount = interactions.filter(i => i.severity === 'High').length;
            parts.push(`⚠️ ${interactions.length} interaction${interactions.length !== 1 ? 's' : ''} detected${highCount > 0 ? ` (${highCount} HIGH severity)` : ''}.`);
          }
          if (duplicates.length > 0) {
            parts.push(`🔄 ${duplicates.length} duplicate therapy warning${duplicates.length !== 1 ? 's' : ''}.`);
          }
          parts.push('Review drug formulations in the Drugs step and select your preferred options.');
          this.state.addAssistantMessage(parts.join(' '));
        }
        this.state.setDrugDetailsLoading(false);
        this.state.setLoading(false);
      },
      error: () => {
        this.state.addAssistantMessage(DRUG_MESSAGES.DETAILS_ERROR);
        this.state.setDrugDetailsLoading(false);
        this.state.setLoading(false);
      },
    });
  }

  cancelSuggestions(): void {
    this.state.clearSuggestions();
    this.selectedNames.clear();
    this.state.addAssistantMessage(DRUG_MESSAGES.VERIFY_CANCELLED);
  }
}
