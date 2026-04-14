import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RecommendationListItem, PharmacyWiseRecommendation, EnrichedPartDCard, EnrichedMACard } from '../../models/part-d-plan.model';

@Component({
  selector: 'app-recommendation-card',
  templateUrl: './recommendation-card.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, CurrencyPipe],
})
export class RecommendationCardComponent {
  plan = input.required<RecommendationListItem>();
  selected = input(false);
  accentColor = input<'blue' | 'purple' | 'orange'>('blue');
  cardType = input<'partd' | 'ma'>('partd');
  enriched = input<EnrichedPartDCard | EnrichedMACard | null>(null);
  seeDetails = output<RecommendationListItem>();

  get rec(): PharmacyWiseRecommendation | null {
    return this.plan().pharmacyWiseRecommendations?.[0] ?? null;
  }

  get enrichedMA(): EnrichedMACard | null {
    return this.cardType() === 'ma' ? (this.enriched() as EnrichedMACard) : null;
  }

  get enrichedPartD(): EnrichedPartDCard | null {
    return this.cardType() === 'partd' ? (this.enriched() as EnrichedPartDCard) : null;
  }
}
