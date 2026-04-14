import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MedigapPlan, EnrichedMedigapCard } from '../../models/medigap-plan.model';

@Component({
  selector: 'app-medigap-card',
  templateUrl: './medigap-card.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, MatTooltipModule, CurrencyPipe],
})
export class MedigapCardComponent {
  plan = input.required<MedigapPlan>();
  selected = input(false);
  enriched = input<EnrichedMedigapCard | null>(null);
  seeDetails = output<MedigapPlan>();
}
