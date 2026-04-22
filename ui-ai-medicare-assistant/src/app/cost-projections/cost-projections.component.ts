import {
  Component, ChangeDetectionStrategy, inject, OnInit, OnDestroy,
  viewChild, ElementRef, afterNextRender
} from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { NavigationStart, Router } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MedicareStateService } from '../services/drug-state.service';
import { AnalysisSnapshotService } from '../services/analysis-snapshot.service';
import { EvaluateCostsResponse, IndividualMedicareDetail, CostEvaluation, CostCategory, ExpenseTableRow } from '../models/cost-projection.model';
import {
  Chart, ChartConfiguration,
  LineController, BarController, DoughnutController, ArcElement,
  LineElement, BarElement, PointElement,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler
} from 'chart.js';
import { COST_PROJECTION_IMMUTABILITY_WARNING } from '../medicare-analysis/cost-projection-messages';
import { AppRoutes } from '../app-routes.const';

Chart.register(
  LineController, BarController, DoughnutController, ArcElement,
  LineElement, BarElement, PointElement,
  CategoryScale, LinearScale,
  Tooltip, Legend, Filler
);

@Component({
  selector: 'app-cost-projections',
  templateUrl: './cost-projections.component.html',
  styleUrls: ['./cost-projections.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [
    CommonModule, CurrencyPipe, MatIconModule, MatButtonModule,
    MatCardModule, MatTooltipModule, MatProgressSpinnerModule
  ]
})
export class CostProjectionsComponent implements OnInit, OnDestroy {
  protected state = inject(MedicareStateService);
  private router = inject(Router);
  private analysisSnapshot = inject(AnalysisSnapshotService);

  readonly lineChart = viewChild<ElementRef<HTMLCanvasElement>>('lineChart');
  readonly barChart = viewChild<ElementRef<HTMLCanvasElement>>('barChart');
  readonly doughnutChart = viewChild<ElementRef<HTMLCanvasElement>>('doughnutChart');
  readonly stackedChart = viewChild<ElementRef<HTMLCanvasElement>>('stackedChart');
  readonly medicareProjectionChart = viewChild<ElementRef<HTMLCanvasElement>>('medicareProjectionChart');

  private charts: Chart[] = [];
  private popStateNavSub?: Subscription;

  /** Inline banner uses the same copy as chat/UI warnings. */
  readonly immutabilityNotice = COST_PROJECTION_IMMUTABILITY_WARNING;

  constructor() {
    afterNextRender(() => this.buildCharts());
  }

  ngOnInit() {
    // No cost data = either a hard refresh (F5 clears in-memory signals) or direct URL access.
    // Reset analysis and redirect to profile.
    if (!this.state.hasCostProjection()) {
      this.state.resetAll();
      this.state.addAssistantMessage(
        'No cost projection data available. Starting a new analysis. ' +
          'To change your profile, use Edit Profile from the menu before the next analysis.'
      );
      this.router.navigateByUrl(AppRoutes.abs.PROFILE, { replaceUrl: true });
      return;
    }
    this.tryAutoSaveRecommendation();
    // Browser Back from cost projections starts a fresh analysis (no return to Plans to edit inputs).
    this.popStateNavSub = this.router.events
      .pipe(filter((e): e is NavigationStart => e instanceof NavigationStart))
      .subscribe(e => {
        if (e.navigationTrigger !== 'popstate') return;
        if (!e.url || e.url.includes('cost-projections')) return;
        if (!this.router.url.includes(AppRoutes.abs.COST_PROJECTIONS)) return;
        setTimeout(() => {
          if (!this.router.url.includes(AppRoutes.abs.COST_PROJECTIONS)) return;
          this.state.resetAll();
          this.state.addAssistantMessage(
            'Browser back started a new analysis. Your drugs, pharmacy, and plan selections for this run were cleared. ' +
              'To change your profile, use Edit Profile from the menu before the next analysis.'
          );
          this.router.navigateByUrl(AppRoutes.abs.PROFILE, { replaceUrl: true });
        }, 0);
      });
  }

  ngOnDestroy() {
    this.popStateNavSub?.unsubscribe();
    this.charts.forEach(c => c.destroy());
  }

  /**
   * After cost evaluation, persist the full recommendation document (profile, drugs, pharmacies, plans, costs)
   * when the user provided a name before running the evaluation.
   */
  private tryAutoSaveRecommendation(): void {
    const name = this.state.pendingCostRunRecommendationName();
    if (!name?.trim()) return;
    if (!this.analysisSnapshot.canSave()) {
      this.state.setPendingCostRunRecommendationName(null);
      return;
    }
    const trimmed = name.trim();
    this.analysisSnapshot.save(trimmed).subscribe({
      next: () => {
        this.state.setPendingCostRunRecommendationName(null);
        this.state.addAssistantMessage(`Plan recommendation "${trimmed}" was saved to your account.`);
      },
      error: (err: { status?: number }) => {
        if (err?.status === 409) {
          this.analysisSnapshot.save(trimmed, true).subscribe({
            next: () => {
              this.state.setPendingCostRunRecommendationName(null);
              this.state.addAssistantMessage(`Plan recommendation "${trimmed}" was saved (updated existing).`);
            },
            error: () => {
              this.state.setPendingCostRunRecommendationName(null);
              this.state.addAssistantMessage('Could not save your plan recommendation. Please try again from the chat.');
            },
          });
        } else {
          this.state.setPendingCostRunRecommendationName(null);
          this.state.addAssistantMessage(
            'Could not save your plan recommendation automatically. You can ask the assistant to save it for you.'
          );
        }
      },
    });
  }

  get data(): EvaluateCostsResponse | null {
    return this.state.costProjection();
  }

  get evaluation(): CostEvaluation | null {
    return this.data?.evaluation ?? null;
  }

  get years(): IndividualMedicareDetail[] {
    return this.data?.yearlyDetails ?? [];
  }

  get bundleLabel(): string {
    if (!this.data) return '';
    const code = this.evaluation?.planBundleCode ?? '';
    const suppType = this.data.lifetimeTotals.supplementPlanType ?? '';
    let label: string;
    if (code === 'MA_ONLY') {
      label = 'AB + MA';
    } else if (code === 'MA_PDP') {
      label = 'ABD + MA';
    } else {
      label = suppType ? `ABD + ${suppType}` : 'ABD';
    }
    const firstYear = this.years[0];
    if (firstYear && firstYear.conciergePremium > 0) {
      label += ' + Concierge';
    }
    return label;
  }

  get expenseTableRow(): ExpenseTableRow | null {
    if (!this.data || this.years.length === 0) return null;
    const y = this.years[0];
    const lt = this.data.lifetimeTotals;
    const code = this.evaluation?.planBundleCode ?? '';
    const suppType = lt.supplementPlanType?.toUpperCase() ?? '';

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
    if (y.conciergePremium > 0) {
      currentExpense += y.conciergePremium;
    }

    // Lifetime: use plan-specific lifetime fields from API
    let lifetimePremium: number;
    let lifetimeOOP: number;
    let lifetimeExpense: number;

    if (code === 'MA_ONLY' || code === 'MA_PDP') {
      lifetimeExpense = lt.lifeTimeABMedicareAdvantageExpenses;
      lifetimePremium = lt.lifeTimeABMedicareAdvantagePremium;
      lifetimeOOP = lt.lifeTimeABMedicareAdvantageOop;
    } else {
      const upper = suppType.toUpperCase();
      if (upper === 'G' || upper === 'HDG') {
        lifetimeExpense = lt.lifeTimeABGDExpenses;
        lifetimePremium = lt.lifeTimeABGDPremium;
        lifetimeOOP = lt.lifeTimeABGDOop;
      } else if (upper === 'F' || upper === 'HDF') {
        lifetimeExpense = lt.lifeTimeABFDExpenses;
        lifetimePremium = lt.lifeTimeABFDPremium;
        lifetimeOOP = lt.lifeTimeABFDOop;
      } else if (upper === 'N' || upper === 'HDN') {
        lifetimeExpense = lt.lifeTimeABNDExpenses;
        lifetimePremium = lt.lifeTimeABNDPremium;
        lifetimeOOP = lt.lifeTimeABNDOop;
      } else if (upper === 'C') {
        lifetimeExpense = lt.lifeTimeABCDExpenses;
        lifetimePremium = lt.lifeTimeABCDPremium;
        lifetimeOOP = lt.lifeTimeABCDOop;
      } else {
        lifetimeExpense = lt.lifeTimeABMedicareAdvantageExpenses;
        lifetimePremium = lt.lifeTimeABMedicareAdvantagePremium;
        lifetimeOOP = lt.lifeTimeABMedicareAdvantageOop;
      }
    }

    if (lt.lifeTimeConciergePremium > 0) {
      lifetimeExpense += lt.lifeTimeConciergePremium;
    }

    return {
      currentTotalExpense: currentExpense,
      currentTotalPremium: currentPremium,
      currentTotalOOP: currentOOP,
      lifetimeTotalExpense: lifetimeExpense,
      lifetimeTotalPremium: lifetimePremium,
      lifetimeTotalOOP: lifetimeOOP,
    };
  }

  getTrajectoryIcon(): string {
    switch (this.evaluation?.costTrajectory) {
      case 'Rising': return 'trending_up';
      case 'Declining': return 'trending_down';
      case 'Stable': return 'trending_flat';
      default: return 'swap_vert';
    }
  }

  getTrajectoryColor(): string {
    switch (this.evaluation?.costTrajectory) {
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

  get presentValueAmount(): number {
    return this.data?.presentValue ?? 0;
  }

  get coverageYear(): number {
    return this.years[0]?.year ?? 0;
  }

  get planSpecificLifetimeExpense(): number {
    return this.expenseTableRow?.lifetimeTotalExpense ?? 0;
  }

  get totalIrmaaSurcharge(): number {
    return this.data?.lifetimeTotals?.totalIrmaa ?? 0;
  }

  private buildCharts() {
    const years = this.years;
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
            pointRadius: 3
          },
          {
            label: 'Premiums',
            data: totalPremiums,
            borderColor: '#0891b2',
            backgroundColor: 'rgba(8, 145, 178, 0.1)',
            fill: false,
            tension: 0.3,
            pointRadius: 2
          },
          {
            label: 'Out-of-Pocket',
            data: totalOop,
            borderColor: '#d97706',
            backgroundColor: 'rgba(217, 119, 6, 0.1)',
            fill: false,
            tension: 0.3,
            pointRadius: 2
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'top', labels: { usePointStyle: true, padding: 16 } },
          tooltip: {
            callbacks: {
              label: ctx => `${ctx.dataset.label}: $${(ctx.parsed.y ?? 0).toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
            }
          }
        },
        scales: {
          y: {
            ticks: { callback: v => '$' + Number(v).toLocaleString() },
            grid: { color: 'rgba(0,0,0,0.05)' }
          },
          x: { grid: { display: false } }
        }
      }
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
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'top', labels: { usePointStyle: true, boxWidth: 8, padding: 12, font: { size: 10 } } },
          tooltip: {
            callbacks: {
              label: ctx => `${ctx.dataset.label}: $${(ctx.parsed.y ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}`
            }
          }
        },
        scales: {
          x: { stacked: true, grid: { display: false } },
          y: {
            stacked: true,
            ticks: { callback: v => '$' + Number(v).toLocaleString() },
            grid: { color: 'rgba(0,0,0,0.05)' }
          }
        }
      }
    };

    this.charts.push(new Chart(el, config));
  }

  private buildDoughnutChart() {
    const el = this.doughnutChart()?.nativeElement;
    if (!el || !this.evaluation) return;

    const categories = this.evaluation.categories;
    if (categories.length === 0) return;

    const colors = ['#6366f1', '#0891b2', '#d97706', '#059669', '#dc2626', '#7c3aed', '#db2777', '#64748b'];

    const config: ChartConfiguration<'doughnut'> = {
      type: 'doughnut',
      data: {
        labels: categories.map((c: CostCategory) => c.name),
        datasets: [{
          data: categories.map((c: CostCategory) => c.lifetimeTotal),
          backgroundColor: colors.slice(0, categories.length),
          borderWidth: 2,
          borderColor: '#fff'
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { position: 'right', labels: { usePointStyle: true, padding: 12, font: { size: 11 } } },
          tooltip: {
            callbacks: {
              label: ctx => {
                const val = ctx.parsed;
                const pct = categories[ctx.dataIndex]?.percentOfTotal ?? 0;
                return `${ctx.label}: $${val.toLocaleString(undefined, { maximumFractionDigits: 0 })} (${pct.toFixed(1)}%)`;
              }
            }
          }
        }
      }
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
          backgroundColor: surcharges.map(s => s > 0 ? '#ef4444' : '#d1d5db'),
          borderRadius: 4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: ctx => `Surcharges: $${(ctx.parsed.y ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 })}`
            }
          }
        },
        scales: {
          y: {
            ticks: { callback: v => '$' + Number(v).toLocaleString() },
            grid: { color: 'rgba(0,0,0,0.05)' }
          },
          x: { grid: { display: false } }
        }
      }
    };

    this.charts.push(new Chart(el, config));
  }

  private buildMedicareProjectionChart(labels: string[], years: IndividualMedicareDetail[]) {
    const el = this.medicareProjectionChart()?.nativeElement;
    if (!el) return;

    const code = this.evaluation?.planBundleCode ?? '';
    const suppType = (this.data?.lifetimeTotals?.supplementPlanType ?? '').toUpperCase();

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
          {
            label: 'Premium',
            data: premiums,
            backgroundColor: 'rgb(132, 201, 54)',
            stack: 'stack0'
          },
          {
            label: 'Surcharge',
            data: surcharges,
            backgroundColor: 'rgb(106, 162, 42)',
            stack: 'stack0'
          },
          {
            label: 'Out-of-Pocket',
            data: oops,
            backgroundColor: 'rgb(204, 0, 0)',
            stack: 'stack0'
          }
        ]
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
              }
            }
          }
        },
        scales: {
          x: { stacked: true, grid: { display: false } },
          y: {
            stacked: true,
            ticks: { callback: v => '$' + Number(v).toLocaleString() },
            grid: { color: 'rgba(0,0,0,0.05)' }
          }
        }
      }
    };

    this.charts.push(new Chart(el, config));
  }
}
