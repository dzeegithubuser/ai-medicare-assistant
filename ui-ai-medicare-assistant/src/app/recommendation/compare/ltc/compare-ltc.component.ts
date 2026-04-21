import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RecommendationResponse } from '../../../models/recommendation.model';
import {
  deltaIcon, deltaLabel,
  getTrajectoryIcon, getTrajectoryColor, getPriorityColor,
  buildProfileRows, ProfileRow,
} from '../compare-helpers';

@Component({
  selector: 'app-compare-ltc',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, CurrencyPipe, MatIconModule, MatCardModule, MatTabsModule, MatTooltipModule],
  templateUrl: './compare-ltc.component.html',
})
export class CompareLtcComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  // ── Helpers (template-callable) ──────────────────────────────────────────
  readonly deltaIcon = deltaIcon;
  readonly deltaLabel = deltaLabel;
  readonly getTrajectoryIcon = getTrajectoryIcon;
  readonly getTrajectoryColor = getTrajectoryColor;
  readonly getPriorityColor = getPriorityColor;

  // ── Deltas ───────────────────────────────────────────────────────────────
  readonly costDelta = computed(() =>
    (this.left().ltcSnapshot?.totalCost ?? 0) - (this.right().ltcSnapshot?.totalCost ?? 0));

  readonly pvDelta = computed(() =>
    (this.left().ltcSnapshot?.totalPresentValue ?? 0) - (this.right().ltcSnapshot?.totalPresentValue ?? 0));

  readonly avgAnnualDelta = computed(() =>
    (this.left().ltcSnapshot?.evaluation?.averageAnnualCost ?? 0) - (this.right().ltcSnapshot?.evaluation?.averageAnnualCost ?? 0));

  // ── Winner ───────────────────────────────────────────────────────────────
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

  // ── Profile rows ─────────────────────────────────────────────────────────
  readonly profileRows = computed(() =>
    buildProfileRows(this.left().profile, this.right().profile));
  readonly profileDiffs = computed(() =>
    this.profileRows().filter(r => r.left !== r.right));

  // ── Care config rows ───────────────────────────────────────────────────────────
  readonly careConfigRows = computed<{ label: string; icon: string; left: string; right: string }[]>(() => {
    const l = this.left().ltcSnapshot;
    const r = this.right().ltcSnapshot;
    return [
      { label: 'Health Profile', icon: 'favorite', left: String(l?.healthProfile ?? '—'), right: String(r?.healthProfile ?? '—') },
      { label: 'Adult Day Years', icon: 'wb_sunny', left: String(l?.adultDayYears ?? '—'), right: String(r?.adultDayYears ?? '—') },
      { label: 'Home Care Years', icon: 'home', left: String(l?.homeCareYears ?? '—'), right: String(r?.homeCareYears ?? '—') },
      { label: 'Nursing Care Years', icon: 'local_hospital', left: String(l?.nursingCareYears ?? '—'), right: String(r?.nursingCareYears ?? '—') },
    ];
  });

  readonly careConfigDiffs = computed(() =>
    this.careConfigRows().filter(r => r.left !== r.right));}
