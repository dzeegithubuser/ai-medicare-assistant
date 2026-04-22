import { Component, ChangeDetectionStrategy, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MedicareStateService } from '../../services/drug-state.service';
import { PlanRecommendationComponent } from '../../plan-recommendation/plan-recommendation.component';

@Component({
  selector: 'app-plans-step',
  templateUrl: './plans-step.component.html',
  styleUrls: ['./plans-step.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [CommonModule, MatIconModule, PlanRecommendationComponent],
})
export class PlansStepComponent implements OnInit {
  protected state = inject(MedicareStateService);

  ngOnInit() {
    this.state.currentStep.set(4);
  }
}
