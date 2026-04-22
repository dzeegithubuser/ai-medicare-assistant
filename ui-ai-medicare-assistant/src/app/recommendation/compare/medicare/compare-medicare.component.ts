import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { RecommendationResponse } from '../../../models/recommendation.model';
import { TabOverviewComponent } from './tab-overview/tab-overview.component';
import { TabProfileComponent } from '../tab-profile/tab-profile.component';
import { TabPrescriptionsComponent } from './tab-prescriptions/tab-prescriptions.component';
import { TabPlansPharmacyComponent } from './tab-plans-pharmacy/tab-plans-pharmacy.component';
import { TabCostAnalysisComponent } from './tab-cost-analysis/tab-cost-analysis.component';

@Component({
  selector: 'app-compare-medicare',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatIconModule,
    MatTabsModule,
    TabOverviewComponent,
    TabProfileComponent,
    TabPrescriptionsComponent,
    TabPlansPharmacyComponent,
    TabCostAnalysisComponent,
  ],
  templateUrl: './compare-medicare.component.html',
})
export class CompareMedicareComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();
}
