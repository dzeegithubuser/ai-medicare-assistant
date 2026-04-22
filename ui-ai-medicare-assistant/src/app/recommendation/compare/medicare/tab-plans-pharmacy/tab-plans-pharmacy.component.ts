import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { RecommendationResponse } from '../../../../models/recommendation.model';
import { starArray } from '../../compare-helpers';

@Component({
  selector: 'app-compare-tab-plans-pharmacy',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, MatIconModule, MatCardModule],
  templateUrl: './tab-plans-pharmacy.component.html',
})
export class TabPlansPharmacyComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  readonly starArray = starArray;
}
