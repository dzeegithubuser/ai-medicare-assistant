import {
  Component, ChangeDetectionStrategy, input, computed,
  viewChild, ElementRef, ChangeDetectorRef, inject, OnDestroy, OnInit,
} from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import {
  RecommendationResponse, YearlyDetailDto,
} from '../../../../models/recommendation.model';
import { deltaClass } from '../../compare-helpers';
import {
  Chart, ChartConfiguration,
  LineController, BarController, LineElement, BarElement, PointElement,
  CategoryScale, LinearScale, Tooltip, Legend, Filler,
} from 'chart.js';

Chart.register(
  LineController, BarController, LineElement, BarElement, PointElement,
  CategoryScale, LinearScale, Tooltip, Legend, Filler,
);

@Component({
  selector: 'app-compare-tab-cost-analysis',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, MatIconModule, MatCardModule],
  templateUrl: './tab-cost-analysis.component.html',
})
export class TabCostAnalysisComponent implements OnInit, OnDestroy {
  private cdr = inject(ChangeDetectorRef);

  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  readonly costLineChart = viewChild<ElementRef<HTMLCanvasElement>>('costLineChart');
  readonly costBarChart = viewChild<ElementRef<HTMLCanvasElement>>('costBarChart');
  private charts: Chart[] = [];

  readonly deltaClass = deltaClass;

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

  ngOnInit(): void {
    queueMicrotask(() => {
      setTimeout(() => {
        this.buildCharts();
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
