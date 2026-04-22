import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MedicareStateService, ChatDrugSelectionCommand } from './drug-state.service';
import { ProfileService } from './profile.service';
import { ChatIntentService } from './chat-intent.service';
import { ChatDrugSelectionService, AvailableDrugSummary, PendingDrugChatCards } from './chat-drug-selection.service';
import { ChatRouterSummaryService } from './chat-router-summary.service';
import { DRUG_MESSAGES } from '../constants/chat-messages';
import { AppRoutes } from '../app-routes.const';
import {
  ACTION_PATTERNS,
  INTENTS_BEFORE_DRUG_FORMULATION_ON_FP_DRUGS,
} from './chat-router.constants';

@Injectable({ providedIn: 'root' })
export class ChatDrugSelectionFlowService {
  private router = inject(Router);
  private state = inject(MedicareStateService);
  private profileService = inject(ProfileService);
  private chatIntentSvc = inject(ChatIntentService);
  private chatDrugSel = inject(ChatDrugSelectionService);
  private summaryBuilder = inject(ChatRouterSummaryService);

  routeToDrugSelection(
    text: string,
    onDrugFlow: ((text: string) => void) | undefined,
    onIntent: (text: string, onDrugFlow?: (text: string) => void) => void,
    setPendingDrugAction: (cmd: ChatDrugSelectionCommand) => void,
    setPendingDrugChatCards: (cards: PendingDrugChatCards | null) => void,
  ): boolean {
    if (this.router.url !== AppRoutes.abs.DRUGS || !this.state.drugDetails()) return false;
    if (ACTION_PATTERNS.some(p => p.test(text))) return false;

    const availableDrugs = this.summaryBuilder.buildAvailableDrugSummaries();
    if (availableDrugs.length === 0) return false;

    this.chatIntentSvc.classify(text, this.profileService.isProfileComplete(), this.router.url).subscribe({
      next: (result) => {
        if (INTENTS_BEFORE_DRUG_FORMULATION_ON_FP_DRUGS.has(result.intent)) {
          this.state.setLoading(false);
          onIntent(text, onDrugFlow);
          return;
        }
        this.runDrugFormulationExtraction(
          text,
          onDrugFlow,
          availableDrugs,
          setPendingDrugAction,
          setPendingDrugChatCards
        );
      },
      error: () => {
        this.runDrugFormulationExtraction(
          text,
          onDrugFlow,
          availableDrugs,
          setPendingDrugAction,
          setPendingDrugChatCards
        );
      },
    });
    return true;
  }

  private runDrugFormulationExtraction(
    text: string,
    onDrugFlow: ((text: string) => void) | undefined,
    availableDrugs: AvailableDrugSummary[],
    setPendingDrugAction: (cmd: ChatDrugSelectionCommand) => void,
    setPendingDrugChatCards: (cards: PendingDrugChatCards | null) => void,
  ): void {
    this.chatDrugSel.extractSelection({ message: text, availableDrugs }).subscribe({
      next: (res) => {
        this.state.setLoading(false);
        const hasRecognizedDrug =
          !!res.drugName &&
          availableDrugs.some(d =>
            d.name.toLowerCase() === res.drugName!.toLowerCase() ||
            d.name.toLowerCase().includes(res.drugName!.toLowerCase()) ||
            res.drugName!.toLowerCase().includes(d.name.toLowerCase())
          );
        // Reply patterns that indicate the drug was not found in the available list.
        const indicatesOutOfListInReply =
          /didn.t find|not found|not in your|available drug/i.test(res.reply ?? '');
        // Drug name was extracted but does not match any currently loaded drug, and this
        // is not a remove/edit command — user likely wants to search for a new drug.
        const drugExtractedNotAvailable =
          !!res.drugName &&
          !hasRecognizedDrug &&
          res.action !== 'remove' &&
          res.action !== 'edit';

        if (
          onDrugFlow &&
          !this.state.pendingDrugFollowupPrompt() &&
          (
            ((res.action === 'select' || res.action === 'options') && !hasRecognizedDrug) ||
            indicatesOutOfListInReply ||
            drugExtractedNotAvailable
          )
        ) {
          this.state.addAssistantMessage(DRUG_MESSAGES.OUT_OF_LIST_FALLBACK);
          onDrugFlow(text);
          return;
        }

        if (res.action === 'remove' || res.action === 'edit') {
          setPendingDrugAction({
            drugName: res.drugName,
            type: res.type,
            dosageForm: res.dosageForm,
            strength: res.strength,
            quantity: res.quantity,
            action: res.action,
          });
          const verb = res.action === 'remove' ? 'remove' : 'edit';
          this.state.addAssistantMessage(`Are you sure you want to **${verb}** **${res.drugName}**? (yes / no)`);
          return;
        }

        if (res.action === 'select' || res.action === 'confirm_all') {
          if (res.action === 'confirm_all') {
            this.state.pendingDrugSelection.set({
              drugName: res.drugName,
              type: res.type,
              dosageForm: res.dosageForm,
              strength: res.strength,
              quantity: res.quantity,
              action: res.action,
            });
            this.state.addAssistantMessage(res.reply);
            return;
          }
          if (res.drugName) {
            const summary = this.summaryBuilder.resolveSummaryForDrug(res.drugName, availableDrugs);
            const partialIn = {
              type: res.type,
              dosageForm: res.dosageForm,
              strength: res.strength,
              quantity: res.quantity,
            };
            const cards = summary ? this.summaryBuilder.computeDrugSelectionCards(summary, partialIn) : null;
            if (cards) {
              setPendingDrugChatCards(cards);
              this.state.pendingDrugSelection.set({
                drugName: res.drugName,
                type: cards.partial.type,
                dosageForm: cards.partial.dosageForm,
                strength: cards.partial.strength,
                quantity: cards.partial.quantity,
                action: 'select',
              });
              this.state.addAssistantMessage(DRUG_MESSAGES.FINISH_WITH_BUTTONS(res.drugName));
            } else {
              this.state.pendingDrugSelection.set({
                drugName: res.drugName,
                type: res.type,
                dosageForm: res.dosageForm,
                strength: res.strength,
                quantity: res.quantity,
                action: res.action,
              });
              this.state.addAssistantMessage(res.reply);
            }
            return;
          }
          this.state.pendingDrugSelection.set({
            drugName: res.drugName,
            type: res.type,
            dosageForm: res.dosageForm,
            strength: res.strength,
            quantity: res.quantity,
            action: res.action,
          });
          this.state.addAssistantMessage(res.reply);
          return;
        }

        this.state.addAssistantMessage(res.reply);
      },
      error: () => {
        this.state.setLoading(false);
        if (onDrugFlow) {
          this.state.addAssistantMessage(DRUG_MESSAGES.NOT_MATCHED_FALLBACK);
          onDrugFlow(text);
        } else {
          this.state.addAssistantMessage(DRUG_MESSAGES.FORMULATION_HELP);
        }
      },
    });
  }
}
