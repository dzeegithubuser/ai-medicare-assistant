import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { RecommendationResponse } from '../../../../models/recommendation.model';
import {
  deltaIcon, deltaLabel, buildProfileRows,
  getTrajectoryIcon, getTrajectoryColor,
} from '../../compare-helpers';

@Component({
  selector: 'app-compare-tab-overview',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, MatIconModule, MatCardModule],
  templateUrl: './tab-overview.component.html',
})
export class TabOverviewComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  readonly deltaIcon = deltaIcon;
  readonly deltaLabel = deltaLabel;
  readonly getTrajectoryIcon = getTrajectoryIcon;
  readonly getTrajectoryColor = getTrajectoryColor;

  readonly costDelta = computed(() =>
    (this.left().lastCostSnapshot?.lifetimeTotal ?? 0) - (this.right().lastCostSnapshot?.lifetimeTotal ?? 0));
  readonly premiumDelta = computed(() =>
    (this.left().lastCostSnapshot?.lifetimePremiums ?? 0) - (this.right().lastCostSnapshot?.lifetimePremiums ?? 0));
  readonly oopDelta = computed(() =>
    (this.left().lastCostSnapshot?.lifetimeOop ?? 0) - (this.right().lastCostSnapshot?.lifetimeOop ?? 0));
  readonly irmaaDelta = computed(() =>
    (this.left().lastCostSnapshot?.lifetimeIrmaa ?? 0) - (this.right().lastCostSnapshot?.lifetimeIrmaa ?? 0));
  readonly pvDelta = computed(() =>
    (this.left().lastCostSnapshot?.presentValue ?? 0) - (this.right().lastCostSnapshot?.presentValue ?? 0));
  readonly currentYearDelta = computed(() =>
    (this.left().lastCostSnapshot?.currentYearTotal ?? 0) - (this.right().lastCostSnapshot?.currentYearTotal ?? 0));

  readonly winner = computed<'left' | 'right' | 'tie'>(() => {
    const d = this.costDelta();
    if (d === 0) return 'tie';
    return d < 0 ? 'left' : 'right';
  });
  readonly winnerName = computed(() => {
    const w = this.winner();
    if (w === 'tie') return 'Tied';
    return w === 'left' ? this.left().name : this.right().name;
  });
  readonly winnerSavings = computed(() => Math.abs(this.costDelta()));

  readonly profileDiffs = computed(() =>
    buildProfileRows(this.left().profile, this.right().profile).filter(r => r.left !== r.right));

  readonly commonDrugCount = computed(() => {
    const ld = this.left().drugList;
    const rd = this.right().drugList;
    const key = (d: { rxcui: string | null; drugName: string }) => d.rxcui?.trim() || d.drugName.toLowerCase().trim();
    const rightKeys = new Set(rd.map(key));
    return ld.filter(d => rightKeys.has(key(d))).length;
  });
  readonly uniqueLeftCount = computed(() => this.left().drugList.length - this.commonDrugCount());
  readonly uniqueRightCount = computed(() => this.right().drugList.length - this.commonDrugCount());
  readonly uniqueLeftDrugNames = computed(() => {
    const key = (d: { rxcui: string | null; drugName: string }) => d.rxcui?.trim() || d.drugName.toLowerCase().trim();
    const rightKeys = new Set(this.right().drugList.map(key));
    return this.left().drugList.filter(d => !rightKeys.has(key(d))).map(d => d.drugName);
  });
  readonly uniqueRightDrugNames = computed(() => {
    const key = (d: { rxcui: string | null; drugName: string }) => d.rxcui?.trim() || d.drugName.toLowerCase().trim();
    const leftKeys = new Set(this.left().drugList.map(key));
    return this.right().drugList.filter(d => !leftKeys.has(key(d))).map(d => d.drugName);
  });

  readonly leftPlanSummary = computed(() =>
    this.left().planSelections.map(p => ({ type: p.planType, name: p.planName, carrier: p.carrier, premium: p.monthlyPremium })));
  readonly rightPlanSummary = computed(() =>
    this.right().planSelections.map(p => ({ type: p.planType, name: p.planName, carrier: p.carrier, premium: p.monthlyPremium })));
}
