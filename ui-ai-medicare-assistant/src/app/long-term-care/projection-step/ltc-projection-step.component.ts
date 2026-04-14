import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  viewChild,
  afterNextRender,
  OnDestroy,
} from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import {
  Chart, ChartConfiguration,
  LineController, BarController, DoughnutController, ArcElement,
  LineElement, BarElement, PointElement,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler,
} from 'chart.js';
import { LtcStateService } from '../ltc-state.service';
import { LtcService } from '../ltc.service';
import { LtcExpenseEntry, LtcCostEvaluation, LtcCostCategory, LtcProjectionResponse } from '../../models/ltc.model';
import { catchError, of } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AppRoutes } from '../../app-routes.const';

Chart.register(
  LineController, BarController, DoughnutController, ArcElement,
  LineElement, BarElement, PointElement,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler,
);

@Component({
  selector: 'app-ltc-projection-step',
  standalone: true,
  imports: [CommonModule, MatCardModule, CurrencyPipe, MatIconModule, MatButtonModule],
  templateUrl: './ltc-projection-step.component.html',
  styleUrls: ['./ltc-projection-step.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LtcProjectionStepComponent implements OnDestroy {
  private state = inject(LtcStateService);
  private ltcService = inject(LtcService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  readonly lineChart = viewChild<ElementRef<HTMLCanvasElement>>('lineChart');
  readonly stackedChart = viewChild<ElementRef<HTMLCanvasElement>>('stackedChart');
  readonly doughnutChart = viewChild<ElementRef<HTMLCanvasElement>>('doughnutChart');

  private charts: Chart[] = [];

  readonly result = computed(() => this.state.ltcResult());
  readonly projection = computed(() => this.result()?.projection ?? null);
  readonly evaluation = computed(() => this.result()?.evaluation ?? null);

  constructor() {
    if (!this.state.ltcResult()) {
      this.ltcService.getCurrent().pipe(
        catchError(() => of(null)),
        takeUntilDestroyed(this.destroyRef),
      ).subscribe(current => {
        if (!current?.ltcResultJson) return;
        try {
          this.state.ltcResult.set(JSON.parse(current.ltcResultJson));
        } catch {
          // Ignore malformed saved JSON and keep UI empty state.
        }
      });
    }

    afterNextRender(() => this.buildCharts());
  }

  ngOnDestroy(): void {
    this.charts.forEach(c => c.destroy());
  }

  startNewAnalysis(): void {
    this.state.resetAll();
    this.router.navigateByUrl(AppRoutes.abs.LTC_PROFILE);
  }

  // ── Trajectory / Flag / Priority helpers (matches Medicare pattern) ──

  getTrajectoryIcon(): string {
    switch (this.evaluation()?.costTrajectory) {
      case 'Rising': return 'trending_up';
      case 'Declining': return 'trending_down';
      case 'Stable': return 'trending_flat';
      default: return 'swap_vert';
    }
  }

  getTrajectoryColor(): string {
    switch (this.evaluation()?.costTrajectory) {
      case 'Rising': return 'text-red-600';
      case 'Declining': return 'text-green-600';
      case 'Stable': return 'text-blue-600';
      default: return 'text-amber-600';
    }
  }

  getFlagIcon(flag: string): string {
    switch (flag) {
      case 'Highest': return 'arrow_upward';
      case 'Lowest': return 'arrow_downward';
      case 'Spike': return 'warning';
      default: return 'check_circle';
    }
  }

  getFlagColor(flag: string): string {
    switch (flag) {
      case 'Highest': return 'text-red-600 bg-red-50';
      case 'Lowest': return 'text-green-600 bg-green-50';
      case 'Spike': return 'text-amber-600 bg-amber-50';
      default: return 'text-gray-600 bg-gray-50';
    }
  }

  getPriorityColor(priority: string): string {
    switch (priority) {
      case 'High': return 'bg-red-100 text-red-700';
      case 'Medium': return 'bg-amber-100 text-amber-700';
      case 'Low': return 'bg-green-100 text-green-700';
      default: return 'bg-gray-100 text-gray-700';
    }
  }

  getTrendIcon(trend: string): string {
    switch (trend) {
      case 'Rising': return 'trending_up';
      case 'Declining': return 'trending_down';
      default: return 'trending_flat';
    }
  }

  getTrendColor(trend: string): string {
    switch (trend) {
      case 'Rising': return 'text-red-600';
      case 'Declining': return 'text-green-600';
      default: return 'text-blue-600';
    }
  }

  // ── Chart building ──

  private buildCharts(): void {
    const p = this.projection();
    if (!p) return;

    const { labels, sortedYears } = this.getYearLabels(p);
    this.buildLineChart(labels, sortedYears, p);
    this.buildStackedBarChart(labels, sortedYears, p);
    this.buildDoughnutChart();
  }

  private getYearLabels(p: LtcProjectionResponse) {
    const allYears = new Set<number>();
    p.futureAdultDayHealthCareExpenseList.forEach(x => allYears.add(x.year));
    p.futureHomeCareExpenseList.forEach(x => allYears.add(x.year));
    p.futureAssistedCareExpensesList.forEach(x => allYears.add(x.year));
    p.futureNursingCareExpensesList.forEach(x => allYears.add(x.year));
    const sortedYears = Array.from(allYears).sort((a, b) => a - b);
    return { labels: sortedYears.map(String), sortedYears };
  }

  private mapByYear(list: LtcExpenseEntry[], sortedYears: number[]): number[] {
    const map = new Map<number, number>();
    list.forEach(x => map.set(x.year, x.expense));
    return sortedYears.map(year => map.get(year) ?? 0);
  }

  private buildLineChart(labels: string[], sortedYears: number[], p: LtcProjectionResponse): void {
    const el = this.lineChart()?.nativeElement;
    if (!el) return;

    const adultDay = this.mapByYear(p.futureAdultDayHealthCareExpenseList, sortedYears);
    const home = this.mapByYear(p.futureHomeCareExpenseList, sortedYears);
    const assisted = this.mapByYear(p.futureAssistedCareExpensesList, sortedYears);
    const nursing = this.mapByYear(p.futureNursingCareExpensesList, sortedYears);
    const total = adultDay.map((v, i) => v + home[i] + assisted[i] + nursing[i]);

    const config: ChartConfiguration<'line'> = {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: 'Total Cost',
            data: total,
            borderColor: '#4f46e5',
            backgroundColor: 'rgba(79, 70, 229, 0.1)',
            fill: true,
            tension: 0.3,
            pointRadius: 3,
          },
          {
            label: 'Adult Day Care',
            data: adultDay,
            borderColor: '#0ea5e9',
            fill: false,
            tension: 0.3,
            pointRadius: 2,
          },
          {
            label: 'Home Care',
            data: home,
            borderColor: '#10b981',
            fill: false,
            tension: 0.3,
            pointRadius: 2,
          },
          {
            label: 'Assisted Care',
            data: assisted,
            borderColor: '#f59e0b',
            fill: false,
            tension: 0.3,
            pointRadius: 2,
          },
          {
            label: 'Nursing Care',
            data: nursing,
            borderColor: '#ef4444',
            fill: false,
            tension: 0.3,
            pointRadius: 2,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'top' } },
        scales: {
          y: {
            beginAtZero: true,
            ticks: { callback: v => `$${Number(v).toLocaleString()}` },
          },
        },
      },
    };
    const chart = new Chart(el, config);
    this.charts.push(chart);
  }

  private buildStackedBarChart(labels: string[], sortedYears: number[], p: LtcProjectionResponse): void {
    const el = this.stackedChart()?.nativeElement;
    if (!el) return;

    const config: ChartConfiguration<'bar'> = {
      type: 'bar',
      data: {
        labels,
        datasets: [
          {
            label: 'Adult Day Care',
            data: this.mapByYear(p.futureAdultDayHealthCareExpenseList, sortedYears),
            backgroundColor: '#0ea5e9',
          },
          {
            label: 'Home Care',
            data: this.mapByYear(p.futureHomeCareExpenseList, sortedYears),
            backgroundColor: '#10b981',
          },
          {
            label: 'Assisted Care',
            data: this.mapByYear(p.futureAssistedCareExpensesList, sortedYears),
            backgroundColor: '#f59e0b',
          },
          {
            label: 'Nursing Care',
            data: this.mapByYear(p.futureNursingCareExpensesList, sortedYears),
            backgroundColor: '#ef4444',
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'top' } },
        scales: {
          x: { stacked: true },
          y: {
            stacked: true,
            beginAtZero: true,
            ticks: { callback: v => `$${Number(v).toLocaleString()}` },
          },
        },
      },
    };
    const chart = new Chart(el, config);
    this.charts.push(chart);
  }

  private buildDoughnutChart(): void {
    const el = this.doughnutChart()?.nativeElement;
    const categories = this.evaluation()?.categories;
    if (!el || !categories?.length) return;

    const config: ChartConfiguration<'doughnut'> = {
      type: 'doughnut',
      data: {
        labels: categories.map(c => c.name),
        datasets: [
          {
            data: categories.map(c => c.lifetimeTotal),
            backgroundColor: ['#0ea5e9', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'],
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'bottom' },
          tooltip: {
            callbacks: {
              label: ctx => {
                const val = ctx.parsed;
                return `${ctx.label}: $${val.toLocaleString()}`;
              },
            },
          },
        },
      },
    };
    const chart = new Chart(el, config);
    this.charts.push(chart);
  }
}
