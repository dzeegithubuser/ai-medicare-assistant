import { Component, ChangeDetectionStrategy, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DrugStateService } from '../../services/drug-state.service';
import { ReferenceDataService } from '../../services/reference-data.service';
import { PlanCardEnrichmentService } from '../../services/plan-card-enrichment.service';
import { MedigapPlan, EnrichedMedigapCard } from '../../models/medigap-plan.model';
import { MedigapCardComponent } from '../medigap-card/medigap-card.component';

@Component({
  selector: 'app-medigap-gap-section',
  templateUrl: './medigap-gap-section.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatProgressSpinnerModule, MedigapCardComponent],
})
export class MedigapGapSectionComponent {
  protected state = inject(DrugStateService);
  private refData = inject(ReferenceDataService);
  private enrichmentService = inject(PlanCardEnrichmentService);

  selectedDataSource = input.required<'AIVANTE' | 'MEDICARE_GOV' | null>();
  selectedPlanType = input.required<string>();

  dataSourceChanged = output<'AIVANTE' | 'MEDICARE_GOV' | null>();
  planTypeChanged = output<string>();
  planSelected = output<MedigapPlan>();
  detailRequested = output<MedigapPlan>();

  get medigapDataSources() { return this.refData.medigapDataSources(); }
  get medigapPlanTypes() { return this.refData.medigapPlanTypes(); }

  isSelected(plan: MedigapPlan): boolean {
    return this.state.selectedMedigapPlan()?.key === plan.key;
  }

  getEnriched(plan: MedigapPlan): EnrichedMedigapCard | null {
    const response = this.state.medigapQuotes();
    if (!response) return null;
    return this.enrichmentService.enrichMedigap(plan, response);
  }
}
