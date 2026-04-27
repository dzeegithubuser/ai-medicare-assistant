import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RecommendationResponse, RecommendationCategory } from '../../../models/recommendation.model';
import {
  deltaIcon, deltaLabel,
  getTrajectoryIcon, getTrajectoryColor,
  typeBadgeClass, typeLabel, buildProfileRows,
  LABEL_A, LABEL_B,
} from '../compare-helpers';
import { TabProfileComponent } from '../tab-profile/tab-profile.component';
import { CompareCrossMetricsComponent } from './compare-cross-metrics.component';

@Component({
  selector: 'app-compare-cross',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, CurrencyPipe, MatIconModule, MatCardModule, MatTabsModule, MatTooltipModule, TabProfileComponent, CompareCrossMetricsComponent],
  templateUrl: './compare-cross.component.html',
  styleUrl: './compare-cross.component.scss',
})
export class CompareCrossComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  readonly labelA = LABEL_A;
  readonly labelB = LABEL_B;

  // ── Helpers (template-callable) ──────────────────────────────────────────
  readonly deltaIcon = deltaIcon;
  readonly deltaLabel = deltaLabel;
  readonly getTrajectoryIcon = getTrajectoryIcon;
  readonly getTrajectoryColor = getTrajectoryColor;
  readonly typeBadgeClass = typeBadgeClass;
  readonly typeLabel = typeLabel;

  // ── Type inference ───────────────────────────────────────────────────────
  readonly leftType = computed<RecommendationCategory>(() => this.left().type ?? 'medicare');
  readonly rightType = computed<RecommendationCategory>(() => this.right().type ?? 'medicare');

  // ── Cost helpers ─────────────────────────────────────────────────────────
  private lifetimeCost(rec: RecommendationResponse, type: RecommendationCategory): number {
    return type === 'longterm'
      ? (rec.ltcSnapshot?.totalCost ?? 0)
      : (rec.lastCostSnapshot?.lifetimeTotal ?? 0);
  }

  readonly leftLifetime = computed(() => this.lifetimeCost(this.left(), this.leftType()));
  readonly rightLifetime = computed(() => this.lifetimeCost(this.right(), this.rightType()));
  private presentValue(rec: RecommendationResponse, type: RecommendationCategory): number {
    return type === 'longterm'
      ? (rec.ltcSnapshot?.totalPresentValue ?? 0)
      : (rec.lastCostSnapshot?.presentValue ?? 0);
  }

  readonly leftPV = computed(() => this.presentValue(this.left(), this.leftType()));
  readonly rightPV = computed(() => this.presentValue(this.right(), this.rightType()));
  readonly pvDelta = computed(() => this.leftPV() - this.rightPV());
  // ── Profile rows ─────────────────────────────────────────────────────────
  readonly profileRows = computed(() =>
    buildProfileRows(this.left().profile, this.right().profile));
  readonly profileDiffs = computed(() =>
    this.profileRows().filter(r => r.left !== r.right));

  readonly costDelta = computed(() => this.leftLifetime() - this.rightLifetime());}
