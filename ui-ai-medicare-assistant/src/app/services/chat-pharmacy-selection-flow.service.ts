import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MedicareStateService } from './drug-state.service';
import { ProfileService } from './profile.service';
import { ChatIntentService, ChatIntentResponse } from './chat-intent.service';
import {
  ChatPharmacySelectionService,
  PharmacySelectionExtractResponse,
} from './chat-pharmacy-selection.service';
import { ChatRouterSummaryService } from './chat-router-summary.service';
import { ChatNavigationFlowService } from './chat-navigation-flow.service';
import { PHARMACY_MESSAGES, DRUG_MESSAGES, ANALYSIS_MESSAGES } from '../constants/chat-messages';
import { ACTION_PATTERNS, DRUG_KEYWORD_PATTERN, PLAN_KEYWORD_PATTERN } from './chat-router.constants';
import { AppRoutes } from '../app-routes.const';
import {
  parseLocalPharmacyIntent,
  parseOrdinalIndex1Based,
  resolvePharmacyHints,
} from '../utils/pharmacy-chat-resolve';

@Injectable({ providedIn: 'root' })
export class ChatPharmacySelectionFlowService {
  private router = inject(Router);
  private state = inject(MedicareStateService);
  private profileService = inject(ProfileService);
  private chatIntentSvc = inject(ChatIntentService);
  private chatPharmSel = inject(ChatPharmacySelectionService);
  private summaryBuilder = inject(ChatRouterSummaryService);
  private navigationFlow = inject(ChatNavigationFlowService);

  routePharmaciesStep(
    text: string,
    onIntent: (result: ChatIntentResponse, text: string) => void
  ): boolean {
    if (this.currentNavigationPath() !== AppRoutes.abs.PHARMACIES || !this.state.hasPharmacyLookup()) return false;
    if (ACTION_PATTERNS.some(p => p.test(text))) return false;

    this.chatIntentSvc.classify(text, this.profileService.isProfileComplete(), this.router.url).subscribe({
      next: (result) => {
        if (result.intent === 'NAVIGATE_PHARMACIES') {
          this.executePharmacySelectionChat(text);
        } else if (result.intent === 'UNKNOWN') {
          // UNKNOWN on pharmacy page — check for plan keywords before pharmacy extraction.
          if (PLAN_KEYWORD_PATTERN.test(text)) {
            this.navigationFlow.handleStepNavigation(4);
          } else {
            this.executePharmacySelectionChat(text);
          }
        } else if (result.intent === 'DRUG_INPUT') {
          // Drug name typed on the pharmacy page — only redirect to fp-drugs when
          // explicit drug keywords are present (e.g. "add drug eliquis").
          // Bare names ("metformin") get a guidance hint instead.
          if (DRUG_KEYWORD_PATTERN.test(text)) {
            this.state.pendingCrossPageDrugSearch.set(text);
            onIntent({ ...result, intent: 'NAVIGATE_ANALYSIS_DRUGS' }, text);
          } else {
            this.state.addAssistantMessage(DRUG_MESSAGES.NAVIGATE_TO_DRUGS_HINT);
            this.state.setLoading(false);
          }
        } else {
          // Pass the already-classified result directly — avoids a redundant second API call.
          onIntent(result, text);
        }
      },
      error: () => this.executePharmacySelectionChat(text),
    });
    return true;
  }

  private executePharmacySelectionChat(text: string): void {
    const { available, selected } = this.summaryBuilder.buildPharmacySummaries();
    this.chatPharmSel.extractSelection({ message: text, availablePharmacies: available, selectedPharmacies: selected }).subscribe({
      next: (res) => {
        const handled = this.applyPharmacyAiResponse(res, text);
        if (!handled && !this.applyLocalPharmacyFallback(text)) {
          this.state.setLoading(false);
          this.state.addAssistantMessage(PHARMACY_MESSAGES.APPLY_HINT);
        }
      },
      error: () => {
        if (!this.applyLocalPharmacyFallback(text)) {
          this.state.setLoading(false);
          this.state.addAssistantMessage(PHARMACY_MESSAGES.ASSISTANT_UNREACHABLE);
        }
      },
    });
  }

  private applyPharmacyAiResponse(res: PharmacySelectionExtractResponse, originalText: string): boolean {
    const action = (res.action || '').toLowerCase();

    if (action === 'clearfilter') {
      this.state.setLoading(false);
      this.state.pendingPharmacySelection.set({
        pharmacyName: null,
        pharmacyNames: null,
        action: 'clearFilter',
        searchTerm: null,
      });
      this.state.addAssistantMessage(res.reply || PHARMACY_MESSAGES.CLEARING_FILTERS);
      return true;
    }

    if (action === 'list') {
      this.state.setLoading(false);
      this.state.addAssistantMessage(res.reply);
      return true;
    }

    if (action === 'search') {
      const term = (res.searchTerm ?? '').trim();
      this.state.setLoading(false);
      if (term) {
        this.state.pendingPharmacySelection.set({
          pharmacyName: null,
          pharmacyNames: null,
          action: 'search',
          searchTerm: term,
        });
      }
      this.state.addAssistantMessage(
        res.reply || (term ? PHARMACY_MESSAGES.FILTERING_BY(term) : PHARMACY_MESSAGES.SEARCH_NOT_UNDERSTOOD)
      );
      return true;
    }

    const pharmacies = this.state.pharmacyLookup()?.pharmacies ?? [];
    const rawHints = [...(res.pharmacyNames ?? []), ...(res.pharmacyName ? [res.pharmacyName] : [])]
      .map(s => String(s).trim())
      .filter(Boolean);
    const uniqueHints = [...new Set(rawHints)];

    if (action === 'select' || action === 'remove') {
      if (uniqueHints.length > 0) {
        const resolved = resolvePharmacyHints(uniqueHints, pharmacies);
        const matched = resolved.filter(r => r.pharmacy).map(r => r.pharmacy!.pharmacyName);
        const unmatched = resolved.filter(r => !r.pharmacy).map(r => r.hint);

        if (matched.length > 0) {
          this.state.setLoading(false);
          this.state.pendingPharmacySelection.set({
            pharmacyName: null,
            pharmacyNames: matched,
            action: action === 'remove' ? 'remove' : 'select',
            searchTerm: null,
          });
          let msg = res.reply || '';
          if (unmatched.length) {
            msg += (msg ? ' ' : '') + `Could not match: ${unmatched.map(h => `"${h}"`).join(', ')}.`;
          }
          this.state.addAssistantMessage(
            msg || (action === 'remove' ? PHARMACY_MESSAGES.REMOVE_SELECTED : PHARMACY_MESSAGES.SELECTED)
          );
          return true;
        }
      }

      const ord = parseOrdinalIndex1Based(originalText);
      if (ord != null && ord >= 1 && ord <= pharmacies.length) {
        const p = pharmacies[ord - 1];
        this.state.setLoading(false);
        this.state.pendingPharmacySelection.set({
          pharmacyName: null,
          pharmacyNames: [p.pharmacyName],
          action: action === 'remove' ? 'remove' : 'select',
          searchTerm: null,
        });
        this.state.addAssistantMessage(
          res.reply?.trim() || PHARMACY_MESSAGES.ORDINAL_ACTION(action as 'remove' | 'select', p.pharmacyName, ord)
        );
        return true;
      }
    }

    return false;
  }

  private applyLocalPharmacyFallback(text: string): boolean {
    const localIntent = parseLocalPharmacyIntent(text);
    if (localIntent?.kind === 'list') {
      this.state.setLoading(false);
      const sel = this.state.selectedLookupPharmacies();
      const lines = sel.map((p, i) => `${i + 1}. **${p.pharmacyName}**`).join('\n');
      this.state.addAssistantMessage(sel.length ? PHARMACY_MESSAGES.LIST_SELECTED(lines, sel.length) : PHARMACY_MESSAGES.LIST_EMPTY);
      return true;
    }

    if (localIntent?.kind === 'search') {
      this.state.setLoading(false);
      this.state.pendingPharmacySelection.set({
        pharmacyName: null,
        pharmacyNames: null,
        action: 'search',
        searchTerm: localIntent.term,
      });
      this.state.addAssistantMessage(PHARMACY_MESSAGES.FILTERING_BY(localIntent.term));
      return true;
    }

    if (localIntent?.kind === 'clearFilter') {
      this.state.setLoading(false);
      this.state.pendingPharmacySelection.set({
        pharmacyName: null,
        pharmacyNames: null,
        action: 'clearFilter',
        searchTerm: null,
      });
      this.state.addAssistantMessage(PHARMACY_MESSAGES.REMOVED_FILTER);
      return true;
    }

    if (localIntent?.kind === 'select' || localIntent?.kind === 'remove') {
      const pharmacies = this.state.pharmacyLookup()?.pharmacies ?? [];
      const ord = parseOrdinalIndex1Based(text);
      if (ord != null && ord >= 1 && ord <= pharmacies.length) {
        const p = pharmacies[ord - 1];
        this.state.setLoading(false);
        const isRemove = localIntent.kind === 'remove';
        this.state.pendingPharmacySelection.set({
          pharmacyName: null,
          pharmacyNames: [p.pharmacyName],
          action: isRemove ? 'remove' : 'select',
          searchTerm: null,
        });
        this.state.addAssistantMessage(PHARMACY_MESSAGES.ORDINAL_ACTION(isRemove ? 'remove' : 'select', p.pharmacyName, ord));
        return true;
      }

      const resolved = resolvePharmacyHints(localIntent.hints, pharmacies);
      const matched = resolved.filter((r): r is typeof r & { pharmacy: NonNullable<typeof r.pharmacy> } => !!r.pharmacy);
      const unmatchedHints = resolved.filter(r => !r.pharmacy).map(r => r.hint);
      if (matched.length > 0) {
        this.state.setLoading(false);
        const names = matched.map(r => r.pharmacy.pharmacyName);
        const isRemove = localIntent.kind === 'remove';
        let reply = isRemove
          ? `Removed ${names.map(n => `**${n}**`).join(', ')} from your selection.`
          : `Selected ${names.map(n => `**${n}**`).join(', ')}.`;
        if (unmatchedHints.length) reply += ` I could not match: ${unmatchedHints.map(h => `"${h}"`).join(', ')}.`;
        if (!isRemove) reply += ' You can select up to five pharmacies in total.';
        this.state.addAssistantMessage(reply);
        this.state.pendingPharmacySelection.set({
          pharmacyName: null,
          pharmacyNames: names,
          action: isRemove ? 'remove' : 'select',
          searchTerm: null,
        });
        return true;
      }
    }

    return false;
  }

  private currentNavigationPath(): string {
    return this.router.url.split('?')[0].split('#')[0].replace(/\/+$/, '') || '/';
  }
}
