import {
  Component, ChangeDetectionStrategy, inject, OnInit, OnDestroy,
  viewChild, ElementRef, ChangeDetectorRef, signal,
} from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { RecommendationService } from '../services/recommendation.service';
import { RecommendationResponse } from '../models/recommendation.model';
import { IndividualMedicareDetail, CostCategory, ExpenseTableRow } from '../models/cost-projection.model';
import {
  Chart, ChartConfiguration,
  LineController, BarController, DoughnutController, ArcElement,
  LineElement, BarElement, PointElement,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler,
} from 'chart.js';

Chart.register(
  LineController, BarController, DoughnutController, ArcElement,
  LineElement, BarElement, PointElement,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler
);

/** Cost tab is the 5th tab (0-based index 4). */
const COST_TAB_INDEX = 4;

@Component({
  selector: 'app-recommendation-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, CurrencyPipe, DatePipe, DecimalPipe,
    MatIconModule, MatButtonModule, MatCardModule,
    MatTooltipModule, MatProgressSpinnerModule,
    MatTabsModule,
  ],
  templateUrl: './recommendation-detail.component.html',
  styleUrls: ['./recommendation-detail.component.scss'],
})
export class RecommendationDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private recommendationService = inject(RecommendationService);
  private cdr = inject(ChangeDetectorRef);

  readonly recommendationId = this.route.snapshot.paramMap.get('id') ?? '';
  readonly recommendation = signal<RecommendationResponse | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly lineChart = viewChild<ElementRef<HTMLCanvasElement>>('lineChart');
  readonly stackedChart = viewChild<ElementRef<HTMLCanvasElement>>('stackedChart');
  readonly doughnutChart = viewChild<ElementRef<HTMLCanvasElement>>('doughnutChart');
  readonly barChart = viewChild<ElementRef<HTMLCanvasElement>>('barChart');
  readonly medicareProjectionChart = viewChild<ElementRef<HTMLCanvasElement>>('medicareProjectionChart');

  private charts: Chart[] = [];
  private chartsBuiltForId: string | null = null;

  ngOnInit() {
    if (!this.recommendationId) {
      this.error.set('Missing recommendation id');
      this.loading.set(false);
      return;
    }
    this.recommendationService.getById(this.recommendationId).subscribe({
      next: (rec) => {
        this.recommendation.set(rec);
        this.loading.set(false);
        this.cdr.markForCheck();
      },
      error: () => {
        this.error.set('Could not load this saved analysis. It may have been removed.');
        this.loading.set(false);
        this.cdr.markForCheck();
      },
    });
  }

  ngOnDestroy() {
    this.destroyCharts();
  }

  goBack() {
    this.router.navigate(['/saved']);
  }

  onCostTabMaybeActivate(index: number) {
    if (index !== COST_TAB_INDEX) return;
    const rec = this.recommendation();
    if (!rec) return;
    const years = rec.lastCostSnapshot?.yearlyDetails;
    if (!years?.length) return;
    if (this.chartsBuiltForId === rec.id) return;
    queueMicrotask(() => {
      this.destroyCharts();
      setTimeout(() => {
        this.buildCharts();
        this.chartsBuiltForId = rec.id;
        this.cdr.markForCheck();
      }, 0);
    });
  }

  getTrajectoryIcon(trajectory: string): string {
    switch (trajectory) {
      case 'Rising': return 'trending_up';
      case 'Declining': return 'trending_down';
      case 'Stable': return 'trending_flat';
      default: return 'swap_vert';
    }
  }

  getTrajectoryColor(trajectory: string): string {
    switch (trajectory) {
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

  // ─── Inline Formatters ──────────────────────────────────────

  fmtGender(v: string): string {
    return v === 'M' ? 'Male' : v === 'F' ? 'Female' : v === 'Male' || v === 'Female' ? v : v;
  }

  fmtHealth(v: number): string {
    const labels: Record<number, string> = { 1: '1 — Best Health', 2: '2 — Good Health', 3: '3 — Average Health', 4: '4 — Below Average', 5: '5 — Poor Health' };
    return labels[v] ?? String(v);
  }

  fmtTaxFiling(v: string): string {
    const map: Record<string, string> = {
      'MARRIED_FILING_JOINTLY': 'Married Filing Jointly',
      'MARRIED_FILING_SEPARATELY': 'Married Filing Separately',
      'INDIVIDUAL': 'Individually',
    };
    return map[v] ?? v;
  }

  starArray(rating: number): ('full' | 'half' | 'empty')[] {
    const arr: ('full' | 'half' | 'empty')[] = [];
    for (let i = 1; i <= 5; i++) {
      if (rating >= i) arr.push('full');
      else if (rating >= i - 0.5) arr.push('half');
      else arr.push('empty');
    }
    return arr;
  }

  getPriorityColor(priority: string): string {
    switch (priority) {
      case 'High': return 'bg-red-100 text-red-700';
      case 'Medium': return 'bg-amber-100 text-amber-700';
      case 'Low': return 'bg-green-100 text-green-700';
      default: return 'bg-gray-100 text-gray-700';
    }
  }

  get recBundleLabel(): string {
    const rec = this.recommendation();
    if (!rec?.lastCostSnapshot) return '';
    const code = rec.lastCostSnapshot.evaluation?.planBundleCode ?? '';
    const suppType = rec.lastCostSnapshot.supplementPlanType ?? '';
    let label: string;
    if (code === 'MA_ONLY') {
      label = 'AB + MA';
    } else if (code === 'MA_PDP') {
      label = 'ABD + MA';
    } else {
      label = suppType ? `ABD + ${suppType}` : 'ABD';
    }
    const years = this.getYears();
    if (years.length > 0 && years[0].conciergePremium > 0) {
      label += ' + Concierge';
    }
    return label;
  }

  get recExpenseTableRow(): ExpenseTableRow | null {
    const rec = this.recommendation();
    if (!rec?.lastCostSnapshot) return null;
    const years = this.getYears();
    if (years.length === 0) return null;
    const y = years[0];
    const snap = rec.lastCostSnapshot;
    const code = snap.evaluation?.planBundleCode ?? '';
    const suppType = (snap.supplementPlanType ?? '').toUpperCase();

    let currentPremium: number;
    let currentOOP: number;

    if (code === 'MA_ONLY') {
      currentPremium = y.partAPremium + y.partBPremium + y.medicareAdvantagePremium;
      currentOOP = y.partAOOP + y.partBOOP;
    } else if (code === 'MA_PDP') {
      currentPremium = y.partAPremium + y.partBPremium + y.medicareAdvantagePremium + y.partDPremium;
      currentOOP = y.partAOOP + y.partBOOP + y.partDOOP;
    } else {
      const suppPremium = suppType === 'G' ? y.planGPremium
        : suppType === 'F' ? y.planFPremium
        : suppType === 'N' ? y.planNPremium
        : 0;
      currentPremium = y.partAPremium + y.partBPremium + y.partDPremium + suppPremium;
      currentOOP = y.partAOOP + y.partBOOP + y.partDOOP;
    }

    let currentExpense = currentPremium + currentOOP;
    if (y.conciergePremium > 0) currentExpense += y.conciergePremium;

    // Lifetime from snapshot summary
    const lifetimeExpense = snap.lifetimeTotal ?? 0;
    const lifetimePremium = snap.lifetimePremiums ?? 0;
    const lifetimeOOP = snap.lifetimeOop ?? 0;

    return {
      currentTotalExpense: currentExpense,
      currentTotalPremium: currentPremium,
      currentTotalOOP: currentOOP,
      lifetimeTotalExpense: lifetimeExpense,
      lifetimeTotalPremium: lifetimePremium,
      lifetimeTotalOOP: lifetimeOOP,
    };
  }

  get recPresentValue(): number {
    return this.recommendation()?.lastCostSnapshot?.presentValue ?? 0;
  }

  get recCoverageYear(): number {
    const years = this.getYears();
    return years[0]?.year ?? 0;
  }

  get recPlanSpecificLifetimeExpense(): number {
    return this.recExpenseTableRow?.lifetimeTotalExpense ?? 0;
  }

  get recTotalIrmaaSurcharge(): number {
    return this.recommendation()?.lastCostSnapshot?.lifetimeIrmaa ?? 0;
  }

  private destroyCharts() {
    this.charts.forEach(c => c.destroy());
    this.charts = [];
  }

  private getYears(): IndividualMedicareDetail[] {
    const raw = this.recommendation()?.lastCostSnapshot?.yearlyDetails;
    if (!raw?.length) return [];
    return raw as unknown as IndividualMedicareDetail[];
  }

  private getEvaluationCategories(): CostCategory[] {
    const ev = this.recommendation()?.lastCostSnapshot?.evaluation;
    if (!ev?.categories?.length) return [];
    return ev.categories as unknown as CostCategory[];
  }

  private buildCharts() {
    const years = this.getYears();
    if (years.length === 0) return;

    const labels = years.map(y => y.year.toString());

    this.buildLineChart(labels, years);
    this.buildStackedBarChart(labels, years);
    this.buildDoughnutChart();
    this.buildBarChart(labels, years);
    this.buildMedicareProjectionChart(labels, years);
  }

  private buildLineChart(labels: string[], years: IndividualMedicareDetail[]) {
    const el = this.lineChart()?.nativeElement;
    if (!el) return;

    const totalPremiums = years.map(y =>
      y.partAPremium + y.partBPremium + y.partDPremium +
      y.medicareAdvantagePremium + y.conciergePremium + y.dentalPremium
    );
    const totalOop = years.map(y =>
      y.partAOOP + y.partBOOP + y.partDOOP + y.dentalOOP
    );
    const totalCombined = totalPremiums.map((p, i) => p + totalOop[i]);

    const config: ChartConfiguration<'line'> = {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: 'Total Cost',
            data: totalCombined,
            borderColor: '#4f46e5',
            backgroundColor: 'rgba(79, 70, 229, 0.1)',
            fill: true,
            tension: 0.3,
            pointRadius: 3,
          },
          {
            label: 'Premiums',
            data: totalPremiums,
            borderColor: '#0891b2',
            backgroundColor: 'rgba(8, 145, 178, 0.1)',
            fill: false,
            tension: 0.3,
            pointRadius: 2,
          },
          {
            label: 'Out-of-Pocket',
            data: totalOop,
            borderColor: '#d97706',
            backgroundColor: 'rgba(217, 119, 6, 0.1)',
            fill: false,
            tension: 0.3,
            pointRadius: 2,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'top', labels: { usePointStyle: true, padding: 16 } },
          tooltip: {
            callbacks: {
              label: ctx => `${ctx.dataset.label}: $${(ctx.parsed.y ?? 0).toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`,
            },
          },
        },
        scales: {
          y: {
            ticks: { callback: v => '$' + Number(v).toLocaleString() },
            grid: { color: 'rgba(0,0,0,0.05)' },
          },
          x: { grid: { display: false } },
        },
      },
    };

    this.charts.push(new Chart(el, config));
  }

  private buildStackedBarChart(labels: string[], years: IndividualMedicareDetail[]) {
    const el = this.stackedChart()?.nativeElement;
    if (!el) return;

    const config: ChartConfiguration<'bar'> = {
      type: 'bar',
      data: {
        labels,
        datasets: [
          { label: 'Part A Premium', data: years.map(y => y.partAPremium), backgroundColor: '#6366f1', stack: 'premiums' },
          { label: 'Part B Premium', data: years.map(y => y.partBPremium), backgroundColor: '#8b5cf6', stack: 'premiums' },
          { label: 'Part D Premium', data: years.map(y => y.partDPremium), backgroundColor: '#a78bfa', stack: 'premiums' },
          { label: 'MA Premium', data: years.map(y => y.medicareAdvantagePremium), backgroundColor: '#c4b5fd', stack: 'premiums' },
          { label: 'Part A OOP', data: years.map(y => y.partAOOP), backgroundColor: '#f97316', stack: 'oop' },
          { label: 'Part B OOP', data: years.map(y => y.partBOOP), backgroundColor: '#fb923c', stack: 'oop' },
          { label: 'Part D OOP', data: years.map(y => y.partDOOP), backgroundColor: '#fdba74', stack: 'oop' },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'top', labels: { usePointStyle: true, boxWidth: 8, padding: 12, font: { size: 10 } } },
          tooltip: {
            callbacks: {
              label: ctx => `${ctx.dataset.label}: $${(ctx.parsed.y ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}`,
            },
          },
        },
        scales: {
          x: { stacked: true, grid: { display: false } },
          y: {
            stacked: true,
            ticks: { callback: v => '$' + Number(v).toLocaleString() },
            grid: { color: 'rgba(0,0,0,0.05)' },
          },
        },
      },
    };

    this.charts.push(new Chart(el, config));
  }

  private buildDoughnutChart() {
    const el = this.doughnutChart()?.nativeElement;
    if (!el) return;

    const categories = this.getEvaluationCategories();
    if (categories.length === 0) return;

    const colors = ['#6366f1', '#0891b2', '#d97706', '#059669', '#dc2626', '#7c3aed', '#db2777', '#64748b'];

    const config: ChartConfiguration<'doughnut'> = {
      type: 'doughnut',
      data: {
        labels: categories.map(c => c.name),
        datasets: [{
          data: categories.map(c => c.lifetimeTotal),
          backgroundColor: colors.slice(0, categories.length),
          borderWidth: 2,
          borderColor: '#fff',
        }],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'right', labels: { usePointStyle: true, padding: 12, font: { size: 11 } } },
          tooltip: {
            callbacks: {
              label: ctx => {
                const val = ctx.parsed as number;
                const pct = categories[ctx.dataIndex]?.percentOfTotal ?? 0;
                return `${ctx.label}: $${val.toLocaleString(undefined, { maximumFractionDigits: 0 })} (${pct.toFixed(1)}%)`;
              },
            },
          },
        },
      },
    };

    this.charts.push(new Chart(el, config));
  }

  private buildBarChart(labels: string[], years: IndividualMedicareDetail[]) {
    const el = this.barChart()?.nativeElement;
    if (!el) return;

    const surcharges = years.map(y => y.partBPremiumSurcharge + y.partDPremiumSurcharge);

    const config: ChartConfiguration<'bar'> = {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          label: 'IRMAA Surcharges',
          data: surcharges,
          backgroundColor: surcharges.map(s => (s > 0 ? '#ef4444' : '#d1d5db')),
          borderRadius: 4,
        }],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: ctx => `Surcharges: $${(ctx.parsed.y ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}`,
            },
          },
        },
        scales: {
          y: {
            ticks: { callback: v => '$' + Number(v).toLocaleString() },
            grid: { color: 'rgba(0,0,0,0.05)' },
          },
          x: { grid: { display: false } },
        },
      },
    };

    this.charts.push(new Chart(el, config));
  }

  private buildMedicareProjectionChart(labels: string[], years: IndividualMedicareDetail[]) {
    const el = this.medicareProjectionChart()?.nativeElement;
    if (!el) return;

    const rec = this.recommendation();
    const code = rec?.lastCostSnapshot?.evaluation?.planBundleCode ?? '';
    const suppType = (rec?.lastCostSnapshot?.supplementPlanType ?? '').toUpperCase();

    const premiums = years.map(y => {
      const basePremiumB = y.partBPremium - y.partBPremiumSurcharge;
      const basePremiumD = y.partDPremium - y.partDPremiumSurcharge;
      let total = y.partAPremium + basePremiumB;
      if (code === 'MA_ONLY') {
        total += y.medicareAdvantagePremium;
      } else if (code === 'MA_PDP') {
        total += y.medicareAdvantagePremium + basePremiumD;
      } else {
        total += basePremiumD;
        if (suppType === 'G' || suppType === 'HDG') total += y.planGPremium;
        else if (suppType === 'F' || suppType === 'HDF') total += y.planFPremium;
        else if (suppType === 'N' || suppType === 'HDN') total += y.planNPremium;
      }
      if (y.conciergePremium > 0) total += y.conciergePremium;
      return total;
    });

    const surcharges = years.map(y => y.partBPremiumSurcharge + y.partDPremiumSurcharge);
    const oops = years.map(y => y.partAOOP + y.partBOOP + y.partDOOP);
    const totals = premiums.map((p, i) => p + surcharges[i] + oops[i]);

    const config: ChartConfiguration<'bar'> = {
      type: 'bar',
      data: {
        labels,
        datasets: [
          { label: 'Premium', data: premiums, backgroundColor: 'rgb(132, 201, 54)', stack: 'stack0' },
          { label: 'Surcharge', data: surcharges, backgroundColor: 'rgb(106, 162, 42)', stack: 'stack0' },
          { label: 'Out-of-Pocket', data: oops, backgroundColor: 'rgb(204, 0, 0)', stack: 'stack0' },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'top', labels: { usePointStyle: true, boxWidth: 10, padding: 16, font: { size: 11 } } },
          tooltip: {
            callbacks: {
              label: ctx => `${ctx.dataset.label}: $${(ctx.parsed.y ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}`,
              afterBody: (items) => {
                const idx = items[0]?.dataIndex;
                if (idx != null) return `Total: $${totals[idx].toLocaleString(undefined, { maximumFractionDigits: 0 })}`;
                return '';
              },
            },
          },
        },
        scales: {
          x: { stacked: true, grid: { display: false } },
          y: {
            stacked: true,
            ticks: { callback: v => '$' + Number(v).toLocaleString() },
            grid: { color: 'rgba(0,0,0,0.05)' },
          },
        },
      },
    };

    this.charts.push(new Chart(el, config));
  }
}
