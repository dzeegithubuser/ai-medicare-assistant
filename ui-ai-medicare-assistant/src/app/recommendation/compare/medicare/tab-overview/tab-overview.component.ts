import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { RecommendationResponse } from '../../../../models/recommendation.model';
import {
  deltaIcon, deltaLabel, buildProfileRows,
  getTrajectoryIcon, getTrajectoryColor,
  LABEL_A, LABEL_B,
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

  readonly labelA = LABEL_A;
  readonly labelB = LABEL_B;

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
    return w === 'left' ? LABEL_A : LABEL_B;
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
  readonly uniqueLeftDrugs = computed(() => {
    const key = (d: { rxcui: string | null; drugName: string }) => d.rxcui?.trim() || d.drugName.toLowerCase().trim();
    const rightKeys = new Set(this.right().drugList.map(key));
    return this.left().drugList.filter(d => !rightKeys.has(key(d)));
  });
  readonly uniqueRightDrugs = computed(() => {
    const key = (d: { rxcui: string | null; drugName: string }) => d.rxcui?.trim() || d.drugName.toLowerCase().trim();
    const leftKeys = new Set(this.left().drugList.map(key));
    return this.right().drugList.filter(d => !leftKeys.has(key(d)));
  });

  readonly leftPlanSummary = computed(() =>
    this.left().planSelections.map(p => ({ type: p.planType, name: p.planName, carrier: p.carrier, premium: p.monthlyPremium, planId: p.planId })));
  readonly rightPlanSummary = computed(() =>
    this.right().planSelections.map(p => ({ type: p.planType, name: p.planName, carrier: p.carrier, premium: p.monthlyPremium, planId: p.planId })));

  readonly samePlans = computed(() => {
    const lIds = this.left().planSelections.map(p => p.planId).sort();
    const rIds = this.right().planSelections.map(p => p.planId).sort();
    return lIds.length === rIds.length && lIds.every((id, i) => id === rIds[i]);
  });
  readonly uniqueLeftPlans = computed(() => {
    const rightIds = new Set(this.right().planSelections.map(p => p.planId));
    return this.leftPlanSummary().filter(p => !rightIds.has(p.planId));
  });
  readonly uniqueRightPlans = computed(() => {
    const leftIds = new Set(this.left().planSelections.map(p => p.planId));
    return this.rightPlanSummary().filter(p => !leftIds.has(p.planId));
  });

  readonly samePharmacies = computed(() => {
    const lNpis = this.left().pharmacies.map(p => p.npi).sort();
    const rNpis = this.right().pharmacies.map(p => p.npi).sort();
    return lNpis.length === rNpis.length && lNpis.every((n, i) => n === rNpis[i]);
  });
  readonly uniqueLeftPharmacies = computed(() => {
    const rightNpis = new Set(this.right().pharmacies.map(p => p.npi));
    return this.left().pharmacies.filter(p => !rightNpis.has(p.npi));
  });
  readonly uniqueRightPharmacies = computed(() => {
    const leftNpis = new Set(this.left().pharmacies.map(p => p.npi));
    return this.right().pharmacies.filter(p => !leftNpis.has(p.npi));
  });
}
