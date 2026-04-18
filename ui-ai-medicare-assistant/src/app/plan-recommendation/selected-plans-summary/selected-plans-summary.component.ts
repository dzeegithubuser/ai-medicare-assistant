import { Component, ChangeDetectionStrategy, inject, input, output } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DrugStateService } from '../../services/drug-state.service';
import { RecommendationListItem, PharmacyWiseRecommendation } from '../../models/part-d-plan.model';
import { MedigapPlan } from '../../models/medigap-plan.model';

@Component({
  selector: 'app-selected-plans-summary',
  templateUrl: './selected-plans-summary.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, MatProgressSpinnerModule, CurrencyPipe],
})
export class SelectedPlansSummaryComponent {
  protected state = inject(DrugStateService);

  costLoading = input(false);
  maIncludesPartD = input(false);
  canCalculate = input(true);

  calculateCost = output<void>();

  getFirstRec(plan: RecommendationListItem): PharmacyWiseRecommendation | null {
    return plan.pharmacyWiseRecommendations?.[0] ?? null;
  }
}
