import { Component, ChangeDetectionStrategy, inject, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MedicareStateService } from '../../services/drug-state.service';
import { PlanCardEnrichmentService } from '../../services/plan-card-enrichment.service';
import { RecommendationListItem, EnrichedPartDCard } from '../../models/part-d-plan.model';
import { RecommendationCardComponent } from '../recommendation-card/recommendation-card.component';

@Component({
  selector: 'app-partd-gap-section',
  templateUrl: './partd-gap-section.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule, RecommendationCardComponent],
})
export class PartDGapSectionComponent {
  protected state = inject(MedicareStateService);
  private enrichmentService = inject(PlanCardEnrichmentService);

  planSelected = output<RecommendationListItem>();
  detailRequested = output<RecommendationListItem>();

  isSelected(plan: RecommendationListItem): boolean {
    return this.state.selectedMAGapPartDPlan()?.contractId === plan.contractId
        && this.state.selectedMAGapPartDPlan()?.planId === plan.planId;
  }

  getEnriched(plan: RecommendationListItem): EnrichedPartDCard | null {
    const response = this.state.partDPlans();
    if (!response) return null;
    const pharmaNumbers = (this.state.selectedLookupPharmacies() ?? []).map(p => p.pharmacyNumber);
    const totalDrugs = this.state.confirmedDrugNames()?.size ?? 0;
    return this.enrichmentService.enrichPartD(plan, response, pharmaNumbers, totalDrugs);
  }
}
