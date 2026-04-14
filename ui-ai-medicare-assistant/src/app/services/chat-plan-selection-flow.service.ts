import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { DrugStateService, ChatPlanSelectionCommand } from './drug-state.service';
import { ProfileService } from './profile.service';
import { ChatIntentService, ChatIntentResponse } from './chat-intent.service';
import {
  ChatPlanSelectionService,
  AvailablePlanSummary,
  AvailableMedigapSummary,
  SelectedPlansSummary,
} from './chat-plan-selection.service';
import { ChatRouterSummaryService } from './chat-router-summary.service';
import { ChatIntentPhraseService } from './chat-intent-phrase.service';
import { ChatNavigationFlowService } from './chat-navigation-flow.service';
import { ACTION_PATTERNS, INTENTS_BEFORE_DRUG_FORMULATION_ON_FP_DRUGS, DRUG_KEYWORD_PATTERN, PHARMACY_KEYWORD_PATTERN } from './chat-router.constants';
import { PLAN_MESSAGES, DRUG_MESSAGES, PHARMACY_MESSAGES } from '../constants/chat-messages';
import { parseOrdinalIndex1Based } from '../utils/pharmacy-chat-resolve';
import { AppRoutes } from '../app-routes.const';

@Injectable({ providedIn: 'root' })
export class ChatPlanSelectionFlowService {
  private router = inject(Router);
  private state = inject(DrugStateService);
  private profileService = inject(ProfileService);
  private chatIntentSvc = inject(ChatIntentService);
  private chatPlanSel = inject(ChatPlanSelectionService);
  private summaryBuilder = inject(ChatRouterSummaryService);
  private intentPhrases = inject(ChatIntentPhraseService);
  private navigationFlow = inject(ChatNavigationFlowService);

  routeToPlanSelection(
    text: string,
    onDrugFlow: ((text: string) => void) | undefined,
    onIntent: (result: ChatIntentResponse, text: string, onDrugFlow?: (text: string) => void) => void,
    setPendingPlanAction: (cmd: ChatPlanSelectionCommand) => void,
  ): boolean {
    const currentPath = this.router.url.split('?')[0].split('#')[0].replace(/\/+$/, '') || '/';
    if (currentPath !== AppRoutes.abs.PLANS) return false;
    if (ACTION_PATTERNS.some(p => p.test(text))) return false;
    if (!this.state.hasPartDPlans() && !this.state.hasMAPlans()) return false;

    const { partDPlans, medigapPlans, maPlans, selectedPlans } = this.summaryBuilder.buildPlanSummaries();
    if (partDPlans.length === 0 && medigapPlans.length === 0 && maPlans.length === 0) return false;

    // Fast-path: resolve ordinal selection locally ("select 2nd", "pick 3rd", etc.)
    if (this.tryOrdinalPlanSelection(text, partDPlans, medigapPlans, maPlans)) return true;

    this.chatIntentSvc.classify(text, this.profileService.isProfileComplete(), this.router.url).subscribe({
      next: (result) => {
        if (INTENTS_BEFORE_DRUG_FORMULATION_ON_FP_DRUGS.has(result.intent)) {
          onIntent(result, text, onDrugFlow);
          return;
        }
        if (
          (result.intent === 'UNKNOWN' || result.intent === 'DRUG_INPUT') &&
          this.intentPhrases.looksLikeCostEvaluationRequest(text)
        ) {
          onIntent(
            { intent: 'NAVIGATE_COST_PROJECTIONS', confirmationMessage: 'Taking you to your cost evaluation.' },
            text,
            onDrugFlow
          );
          return;
        }
        // Drug name typed on the plans page — only redirect to fp-drugs when
        // explicit drug keywords are present (e.g. "add drug eliquis").
        // Bare names ("metformin") get a guidance hint instead.
        if (result.intent === 'DRUG_INPUT') {
          if (DRUG_KEYWORD_PATTERN.test(text)) {
            this.state.pendingCrossPageDrugSearch.set(text);
            onIntent({ ...result, intent: 'NAVIGATE_ANALYSIS_DRUGS' }, text, onDrugFlow);
          } else {
            this.state.addAssistantMessage(DRUG_MESSAGES.NAVIGATE_TO_DRUGS_HINT);
            this.state.setLoading(false);
          }
          return;
        }
        // UNKNOWN on the plans page — pharmacy keywords trigger actual navigation.
        if (result.intent === 'UNKNOWN' && PHARMACY_KEYWORD_PATTERN.test(text)) {
          this.navigationFlow.handleStepNavigation(3);
          return;
        }
        this.runPlanSelectionExtract(text, partDPlans, medigapPlans, maPlans, selectedPlans, setPendingPlanAction);
      },
      error: () => this.runPlanSelectionExtract(text, partDPlans, medigapPlans, maPlans, selectedPlans, setPendingPlanAction),
    });
    return true;
  }

  private runPlanSelectionExtract(
    text: string,
    partDPlans: AvailablePlanSummary[],
    medigapPlans: AvailableMedigapSummary[],
    maPlans: AvailablePlanSummary[],
    selectedPlans: SelectedPlansSummary,
    setPendingPlanAction: (cmd: ChatPlanSelectionCommand) => void,
  ): void {
    this.chatPlanSel.extractSelection({
      message: text,
      activeSection: this.state.activeSection(),
      availablePartDPlans: partDPlans,
      availableMedigapPlans: medigapPlans,
      availableMAPlans: maPlans,
      selectedPlans,
    }).subscribe({
      next: (res) => {
        this.state.setLoading(false);
        if (res.action === 'remove') {
          setPendingPlanAction({ planName: res.planName, planCategory: res.planCategory, action: 'remove' });
          const label = res.planCategory === 'medigap' ? 'Medigap' : res.planCategory === 'ma' ? 'MA' : 'Part D';
          this.state.addAssistantMessage(PLAN_MESSAGES.REMOVE_CONFIRM(label, res.planName));
        } else if (res.action === 'select') {
          this.state.pendingPlanSelection.set({
            planName: res.planName,
            planCategory: res.planCategory,
            action: 'select',
          });
          this.state.addAssistantMessage(res.reply);
        } else {
          this.state.addAssistantMessage(res.reply);
        }
      },
      error: () => {
        this.state.setLoading(false);
        this.state.addAssistantMessage(PLAN_MESSAGES.SELECTION_HELP);
      },
    });
  }

  private tryOrdinalPlanSelection(
    text: string,
    partDPlans: AvailablePlanSummary[],
    medigapPlans: AvailableMedigapSummary[],
    maPlans: AvailablePlanSummary[],
  ): boolean {
    const ord = parseOrdinalIndex1Based(text);
    if (ord == null) return false;

    const isRemove = /\b(?:remove|deselect|unselect|drop)\b/i.test(text);
    const action = isRemove ? 'remove' : 'select';
    const section = this.state.activeSection();

    if (section === 'partd') {
      if (ord >= 1 && ord <= partDPlans.length) {
        const plan = partDPlans[ord - 1];
        this.state.pendingPlanSelection.set({ planName: plan.planName, planCategory: 'partd', action });
        this.state.addAssistantMessage(`${action === 'select' ? 'Selecting' : 'Removing'} **${plan.planName}** (${ord} on this list).`);
        return true;
      }
    } else if (section === 'ma') {
      if (ord >= 1 && ord <= maPlans.length) {
        const plan = maPlans[ord - 1];
        this.state.pendingPlanSelection.set({ planName: plan.planName, planCategory: 'ma', action });
        this.state.addAssistantMessage(`${action === 'select' ? 'Selecting' : 'Removing'} **${plan.planName}** (${ord} on this list).`);
        return true;
      }
    }

    return false;
  }
}
