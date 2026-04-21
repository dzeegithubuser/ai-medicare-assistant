import {
  Component, ChangeDetectionStrategy, input, computed,
  viewChild, ElementRef, ChangeDetectorRef, inject, OnDestroy,
} from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  RecommendationResponse, YearlyDetailDto, SelectedDrugDto,
} from '../../../models/recommendation.model';
import {
  deltaClass, deltaIcon, deltaLabel,
  getTrajectoryIcon, getTrajectoryColor,
  starArray, buildProfileRows, ProfileRow,
} from '../compare-helpers';
import {
  Chart, ChartConfiguration,
  LineController, BarController, LineElement, BarElement, PointElement,
  CategoryScale, LinearScale, Tooltip, Legend, Filler,
} from 'chart.js';

Chart.register(
  LineController, BarController, LineElement, BarElement, PointElement,
  CategoryScale, LinearScale, Tooltip, Legend, Filler,
);

interface DrugMatch {
  drugName: string;
  dosage: string;
  quantity: number;
  refillFrequency: string;
  side: 'common' | 'left' | 'right';
}

@Component({
  selector: 'app-compare-medicare',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, CurrencyPipe, MatIconModule, MatCardModule, MatTabsModule, MatTooltipModule],
  templateUrl: './compare-medicare.component.html',
})
export class CompareMedicareComponent implements OnDestroy {
  private cdr = inject(ChangeDetectorRef);

  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  readonly costLineChart = viewChild<ElementRef<HTMLCanvasElement>>('costLineChart');
  readonly costBarChart = viewChild<ElementRef<HTMLCanvasElement>>('costBarChart');
  private charts: Chart[] = [];
  private chartsBuilt = false;

  // ── Helpers (template-callable) ──────────────────────────────────────────
  readonly deltaClass = deltaClass;
  readonly deltaIcon = deltaIcon;
  readonly deltaLabel = deltaLabel;
  readonly getTrajectoryIcon = getTrajectoryIcon;
  readonly getTrajectoryColor = getTrajectoryColor;
  readonly starArray = starArray;

  // ── Deltas ───────────────────────────────────────────────────────────────
  readonly costDelta = computed(() =>
    (this.left().lastCostSnapshot?.lifetimeTotal ?? 0) - (this.right().lastCostSnapshot?.lifetimeTotal ?? 0));

  readonly premiumDelta = computed(() =>
    (this.left().lastCostSnapshot?.lifetimePremiums ?? 0) - (this.right().lastCostSnapshot?.lifetimePremiums ?? 0));

  readonly oopDelta = computed(() =>
    (this.left().lastCostSnapshot?.lifetimeOop ?? 0) - (this.right().lastCostSnapshot?.lifetimeOop ?? 0));

  readonly irmaaDelta = computed(() =>
    (this.left().lastCostSnapshot?.lifetimeIrmaa ?? 0) - (this.right().lastCostSnapshot?.lifetimeIrmaa ?? 0));

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

  readonly personalRows = computed(() => this.profileRows().filter(r => r.group === 'personal'));
  readonly locationRows = computed(() => this.profileRows().filter(r => r.group === 'location'));
  readonly healthRows = computed(() => this.profileRows().filter(r => r.group === 'health'));
  readonly financialRows = computed(() => this.profileRows().filter(r => r.group === 'financial'));

  // ── Drug matching ────────────────────────────────────────────────────────
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

  // ── Overview: profile diff highlights ────────────────────────────────────
  readonly profileDiffs = computed(() =>
    this.profileRows().filter(r => r.left !== r.right));

  // ── Overview: plan summaries ─────────────────────────────────────────────
  readonly leftPlanSummary = computed(() => {
    const plans = this.left().planSelections;
    return plans.map(p => ({ type: p.planType, name: p.planName, carrier: p.carrier, premium: p.monthlyPremium, stars: p.starRating }));
  });

  readonly rightPlanSummary = computed(() => {
    const plans = this.right().planSelections;
    return plans.map(p => ({ type: p.planType, name: p.planName, carrier: p.carrier, premium: p.monthlyPremium, stars: p.starRating }));
  });

  // ── Overview: present value delta ────────────────────────────────────────
  readonly pvDelta = computed(() =>
    (this.left().lastCostSnapshot?.presentValue ?? 0) - (this.right().lastCostSnapshot?.presentValue ?? 0));

  // ── Overview: current year delta ─────────────────────────────────────────
  readonly currentYearDelta = computed(() =>
    (this.left().lastCostSnapshot?.currentYearTotal ?? 0) - (this.right().lastCostSnapshot?.currentYearTotal ?? 0));

  // ── Yearly rows ──────────────────────────────────────────────────────────
  yearlyRows(): Array<{ year: number; left: YearlyDetailDto | null; right: YearlyDetailDto | null }> {
    const left = this.left().lastCostSnapshot?.yearlyDetails ?? [];
    const right = this.right().lastCostSnapshot?.yearlyDetails ?? [];
    const years = new Set<number>([...left.map(y => y.year), ...right.map(y => y.year)]);
    return Array.from(years).sort((a, b) => a - b).map(year => ({
      year,
      left: left.find(y => y.year === year) ?? null,
      right: right.find(y => y.year === year) ?? null,
    }));
  }

  yearlyTotal(row: YearlyDetailDto | null): number {
    if (!row) return 0;
    return row.partAPremium + row.partBPremium + row.partBPremiumSurcharge +
      row.medicareAdvantagePremium + row.partDPremium + row.partDPremiumSurcharge +
      row.conciergePremium + row.partAOOP + row.partBOOP + row.partDOOP +
      row.dentalPremium + row.dentalOOP;
  }

  // ── Charts ───────────────────────────────────────────────────────────────
  private static readonly COST_TAB_INDEX = 4;

  onTabChange(index: number): void {
    if (index !== CompareMedicareComponent.COST_TAB_INDEX || this.chartsBuilt) return;
    queueMicrotask(() => {
      this.destroyCharts();
      setTimeout(() => {
        this.buildCharts();
        this.chartsBuilt = true;
        this.cdr.markForCheck();
      }, 0);
    });
  }

  ngOnDestroy(): void {
    this.destroyCharts();
  }

  private destroyCharts(): void {
    this.charts.forEach(c => c.destroy());
    this.charts = [];
  }

  private buildCharts(): void {
    this.buildLineChart();
    this.buildBarChart();
  }

  private buildLineChart(): void {
    const el = this.costLineChart()?.nativeElement;
    if (!el) return;
    const lYears = this.left().lastCostSnapshot?.yearlyDetails ?? [];
    const rYears = this.right().lastCostSnapshot?.yearlyDetails ?? [];
    if (!lYears.length && !rYears.length) return;

    const allYears = [...new Set([...lYears.map(y => y.year), ...rYears.map(y => y.year)])].sort();
    const labels = allYears.map(y => y.toString());
    const toMap = (arr: YearlyDetailDto[]) => new Map(arr.map(y => [y.year, this.yearlyTotal(y)]));
    const lMap = toMap(lYears);
    const rMap = toMap(rYears);

    const config: ChartConfiguration<'line'> = {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: this.left().name, data: allYears.map(y => lMap.get(y) ?? 0),
            borderColor: '#4f46e5', backgroundColor: 'rgba(79, 70, 229, 0.08)',
            fill: true, tension: 0.3, pointRadius: 3,
          },
          {
            label: this.right().name, data: allYears.map(y => rMap.get(y) ?? 0),
            borderColor: '#0891b2', backgroundColor: 'rgba(8, 145, 178, 0.08)',
            fill: true, tension: 0.3, pointRadius: 3,
          },
        ],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: {
          legend: { position: 'top', labels: { usePointStyle: true, padding: 16 } },
          tooltip: { callbacks: { label: ctx => `${ctx.dataset.label}: $${(ctx.parsed.y ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}` } },
        },
        scales: {
          y: { ticks: { callback: v => '$' + Number(v).toLocaleString() }, grid: { color: 'rgba(0,0,0,0.05)' } },
          x: { grid: { display: false } },
        },
      },
    };
    this.charts.push(new Chart(el, config));
  }

  private buildBarChart(): void {
    const el = this.costBarChart()?.nativeElement;
    if (!el) return;
    const lSnap = this.left().lastCostSnapshot;
    const rSnap = this.right().lastCostSnapshot;
    if (!lSnap && !rSnap) return;

    const config: ChartConfiguration<'bar'> = {
      type: 'bar',
      data: {
        labels: ['Premiums', 'Out-of-Pocket', 'IRMAA', 'Total'],
        datasets: [
          {
            label: this.left().name,
            data: [lSnap?.lifetimePremiums ?? 0, lSnap?.lifetimeOop ?? 0, lSnap?.lifetimeIrmaa ?? 0, lSnap?.lifetimeTotal ?? 0],
            backgroundColor: 'rgba(79, 70, 229, 0.7)', borderRadius: 4,
          },
          {
            label: this.right().name,
            data: [rSnap?.lifetimePremiums ?? 0, rSnap?.lifetimeOop ?? 0, rSnap?.lifetimeIrmaa ?? 0, rSnap?.lifetimeTotal ?? 0],
            backgroundColor: 'rgba(8, 145, 178, 0.7)', borderRadius: 4,
          },
        ],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: {
          legend: { position: 'top', labels: { usePointStyle: true, padding: 16 } },
          tooltip: { callbacks: { label: ctx => `${ctx.dataset.label}: $${(ctx.parsed.y ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}` } },
        },
        scales: {
          y: { ticks: { callback: v => '$' + Number(v).toLocaleString() }, grid: { color: 'rgba(0,0,0,0.05)' } },
          x: { grid: { display: false } },
        },
      },
    };
    this.charts.push(new Chart(el, config));
  }
}
