import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { RecommendationResponse, SelectedDrugDto } from '../../../../models/recommendation.model';
import { starArray, LABEL_A, LABEL_B } from '../../compare-helpers';

@Component({
  selector: 'app-compare-tab-rx-pharmacy-plans',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, MatIconModule, MatCardModule],
  templateUrl: './tab-rx-pharmacy-plans.component.html',
})
export class TabRxPharmacyPlansComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  readonly labelA = LABEL_A;
  readonly labelB = LABEL_B;

  readonly starArray = starArray;

  private drugKey(d: SelectedDrugDto): string {
    return d.rxcui?.trim() || d.drugName.toLowerCase().trim();
  }

  readonly commonDrugs = computed(() => {
    const ld = this.left().drugList;
    const rd = this.right().drugList;
    const rightKeys = new Set(rd.map(d => this.drugKey(d)));
    return ld.filter(d => rightKeys.has(this.drugKey(d)));
  });
}
