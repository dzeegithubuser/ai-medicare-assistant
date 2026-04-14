import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ChatOrchestratorService } from './chat-orchestrator.service';
import { RecommendationStateService } from './recommendation-state.service';
import { DrugStateService } from './drug-state.service';
import { DeltaResult, DisplayData, OrchestratorResponse } from '../models/orchestrator.model';
import { APP_MESSAGES } from '../constants/chat-messages';
import { AppRoutes } from '../app-routes.const';

@Injectable({ providedIn: 'root' })
export class ChatOrchestratorFlowService {
  private orchestrator = inject(ChatOrchestratorService);
  private recState = inject(RecommendationStateService);
  private state = inject(DrugStateService);
  private router = inject(Router);

  readonly pendingDelta = signal<DeltaResult | null>(null);
  readonly awaitingConfirmation = signal(false);
  readonly activeDisplayData = signal<DisplayData | null>(null);
  readonly deleteConfirmMode = signal(false);

  routeToOrchestrator(text: string): boolean {
    if (!this.recState.hasRecommendation()) return false;

    const url = this.router.url;
    if (
      url.startsWith('/profile') ||
      url.startsWith(AppRoutes.abs.PROFILE) ||
      url.startsWith(AppRoutes.abs.DRUGS) ||
      url.startsWith(AppRoutes.abs.PHARMACIES) ||
      url.startsWith(AppRoutes.abs.PLANS)
    ) {
      return false;
    }

    this.orchestrator.sendMessage(text, this.router.url).subscribe({
      next: (res) => this.handleOrchestratorResponse(res),
      error: (err) => {
        this.state.setLoading(false);
        const msg = err?.status === 0 ? APP_MESSAGES.REQUEST_TIMEOUT : APP_MESSAGES.GENERIC_ERROR;
        this.state.addAssistantMessage(msg);
      },
    });
    return true;
  }

  handleOrchestratorResponse(res: OrchestratorResponse): void {
    this.state.setLoading(false);
    this.state.addAssistantMessage(res.message);

    this.pendingDelta.set(res.delta ?? null);
    this.awaitingConfirmation.set(res.requiresConfirmation);
    this.activeDisplayData.set(res.displayData ?? null);
    this.deleteConfirmMode.set(res.message.includes('DELETE MY RECOMMENDATION'));

    if (res.message.includes('permanently deleted')) {
      this.recState.clear();
      this.deleteConfirmMode.set(false);
    }
    if (res.message.includes('Recommendation') && res.message.includes('created')) {
      this.recState.refreshAfterUpdate();
    }
    if (!res.requiresConfirmation && !res.delta) {
      this.recState.refreshAfterUpdate();
    }
  }

  confirmOrCancel(answer: 'yes' | 'no'): void {
    this.awaitingConfirmation.set(false);
    this.pendingDelta.set(null);
    this.activeDisplayData.set(null);
    this.state.addUserMessage(answer === 'yes' ? 'Yes, confirm' : 'No, cancel');
    this.state.setLoading(true);

    this.orchestrator.sendMessage(answer, this.router.url).subscribe({
      next: (res) => {
        this.handleOrchestratorResponse(res);
        this.recState.refreshAfterUpdate();
      },
      error: () => {
        this.state.setLoading(false);
        this.state.addAssistantMessage(APP_MESSAGES.GENERIC_ERROR);
      },
    });
  }
}
