import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { RecommendationResponse, SelectedDrugDto } from '../../../../models/recommendation.model';

interface DrugMatch {
  drugName: string;
  drugType: string | null;
  dosage: string;
  quantity: number;
  refillFrequency: string;
  rxcui: string | null;
  side: 'common' | 'left' | 'right';
}

@Component({
  selector: 'app-compare-tab-prescriptions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatCardModule],
  templateUrl: './tab-prescriptions.component.html',
})
export class TabPrescriptionsComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  private drugKey(d: SelectedDrugDto): string {
    return d.rxcui?.trim() || d.drugName.toLowerCase().trim();
  }

  readonly drugMatches = computed<DrugMatch[]>(() => {
    const ld = this.left().drugList;
    const rd = this.right().drugList;
    const leftKeys = new Map(ld.map(d => [this.drugKey(d), d]));
    const rightKeys = new Map(rd.map(d => [this.drugKey(d), d]));
    const result: DrugMatch[] = [];
    for (const [key, d] of leftKeys) {
      result.push({ ...d, side: rightKeys.has(key) ? 'common' : 'left' });
    }
    for (const [key, d] of rightKeys) {
      if (!leftKeys.has(key)) result.push({ ...d, side: 'right' });
    }
    return result;
  });

  readonly commonDrugs = computed(() => this.drugMatches().filter(d => d.side === 'common'));
  readonly uniqueLeftDrugs = computed(() => this.drugMatches().filter(d => d.side === 'left'));
  readonly uniqueRightDrugs = computed(() => this.drugMatches().filter(d => d.side === 'right'));
}
