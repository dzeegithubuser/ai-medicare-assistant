import {
  Component, ChangeDetectionStrategy, input, viewChild,
  ElementRef, ChangeDetectorRef, inject, OnDestroy,
} from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { RecommendationResponse } from '../../../models/recommendation.model';
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
  Tooltip, Legend, Filler,
);

@Component({
  selector: 'app-rec-detail-ltc',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, CurrencyPipe, MatIconModule, MatCardModule, MatTabsModule],
  templateUrl: './rec-detail-ltc.component.html',
  styleUrl: './rec-detail-ltc.component.scss',
})
export class RecDetailLtcComponent implements OnDestroy {
  private cdr = inject(ChangeDetectorRef);

  readonly rec = input.required<RecommendationResponse>();

  readonly ltcLineChart = viewChild<ElementRef<HTMLCanvasElement>>('ltcLineChart');
  readonly ltcStackedChart = viewChild<ElementRef<HTMLCanvasElement>>('ltcStackedChart');
  readonly ltcDoughnutChart = viewChild<ElementRef<HTMLCanvasElement>>('ltcDoughnutChart');

  private charts: Chart[] = [];
  private chartsBuiltForId: string | null = null;

  ngOnDestroy() { this.charts.forEach(c => c.destroy()); }

  onCostTabActivate(index: number) {
    if (index !== 1) return;
    const rec = this.rec();
    if (!rec || rec.type !== 'longterm') return;
    if (this.chartsBuiltForId === rec.id) return;
    queueMicrotask(() => {
      this.charts.forEach(c => c.destroy());
      this.charts = [];
      setTimeout(() => {
        this.buildCharts();
        this.chartsBuiltForId = rec.id;
        this.cdr.markForCheck();
      }, 0);
    });
  }

  // ─── Formatters ─────────────────────────────────────────────

  fmtGender(v: string): string {
    return v === 'M' ? 'Male' : v === 'F' ? 'Female' : v;
  }

  fmtHealth(v: number): string {
    const labels: Record<number, string> = { 1: '1 — Best Health', 2: '2 — Good Health', 3: '3 — Average Health', 4: '4 — Below Average', 5: '5 — Poor Health' };
    return labels[v] ?? String(v);
  }

  fmtTaxFiling(v: string): string {
    const map: Record<string, string> = {
      'MARRIED_FILING_JOINTLY': 'Married Filing Jointly',
      'MARRIED_FILING_SEPARATELY': 'Married Filing Separately',
      'INDIVIDUAL': 'Filing Individually',
      'FILING_INDIVIDUALLY': 'Filing Individually',
      'Single': 'Single / Individual',
    };
    return map[v] ?? v;
  }

  fmtMagiTier(v: string): string {
    return /^\d+$/.test(v) ? `Tier ${v}` : v;
  }

  fmtState(code: string): string {
    const states: Record<string, string> = {
      AL:'Alabama',AK:'Alaska',AZ:'Arizona',AR:'Arkansas',CA:'California',
      CO:'Colorado',CT:'Connecticut',DE:'Delaware',FL:'Florida',GA:'Georgia',
      HI:'Hawaii',ID:'Idaho',IL:'Illinois',IN:'Indiana',IA:'Iowa',
      KS:'Kansas',KY:'Kentucky',LA:'Louisiana',ME:'Maine',MD:'Maryland',
      MA:'Massachusetts',MI:'Michigan',MN:'Minnesota',MS:'Mississippi',MO:'Missouri',
      MT:'Montana',NE:'Nebraska',NV:'Nevada',NH:'New Hampshire',NJ:'New Jersey',
      NM:'New Mexico',NY:'New York',NC:'North Carolina',ND:'North Dakota',OH:'Ohio',
      OK:'Oklahoma',OR:'Oregon',PA:'Pennsylvania',RI:'Rhode Island',SC:'South Carolina',
      SD:'South Dakota',TN:'Tennessee',TX:'Texas',UT:'Utah',VT:'Vermont',
      VA:'Virginia',WA:'Washington',WV:'West Virginia',WI:'Wisconsin',WY:'Wyoming',
      DC:'District of Columbia',
    };
    return states[code?.toUpperCase()] ?? code;
  }

  getTrajectoryIcon(t: string): string {
    switch (t) { case 'Rising': return 'trending_up'; case 'Declining': return 'trending_down'; case 'Stable': return 'trending_flat'; default: return 'swap_vert'; }
  }
  getTrajectoryColor(t: string): string {
    switch (t) { case 'Rising': return 'text-red-600'; case 'Declining': return 'text-green-600'; case 'Stable': return 'text-blue-600'; default: return 'text-amber-600'; }
  }
  getFlagIcon(f: string): string {
    switch (f) { case 'Highest': return 'arrow_upward'; case 'Lowest': return 'arrow_downward'; case 'Spike': return 'warning'; default: return 'check_circle'; }
  }
  getFlagColor(f: string): string {
    switch (f) { case 'Highest': return 'text-red-600 bg-red-50'; case 'Lowest': return 'text-green-600 bg-green-50'; case 'Spike': return 'text-amber-600 bg-amber-50'; default: return 'text-gray-600 bg-gray-50'; }
  }
  getTrendIcon(t: string): string {
    switch (t) { case 'Rising': return 'trending_up'; case 'Declining': return 'trending_down'; default: return 'trending_flat'; }
  }
  getTrendColor(t: string): string {
    switch (t) { case 'Rising': return 'text-red-600'; case 'Declining': return 'text-green-600'; default: return 'text-blue-600'; }
  }
  getPriorityColor(p: string): string {
    switch (p) { case 'High': return 'bg-red-100 text-red-700'; case 'Medium': return 'bg-amber-100 text-amber-700'; case 'Low': return 'bg-green-100 text-green-700'; default: return 'bg-gray-100 text-gray-700'; }
  }

  // ─── Charts ─────────────────────────────────────────────────

  private buildCharts() {
    const p = this.rec().ltcSnapshot?.projection;
    if (!p) return;

    const allYears = new Set<number>();
    [...p.adultDayExpenses, ...p.homeCareExpenses, ...p.assistedCareExpenses, ...p.nursingCareExpenses]
      .forEach(e => allYears.add(e.year));
    const sortedYears = Array.from(allYears).sort((a, b) => a - b);
    const labels = sortedYears.map(String);

    const mapByYear = (list: { year: number; expense: number }[]) => {
      const m = new Map(list.map(e => [e.year, e.expense]));
      return sortedYears.map(y => m.get(y) ?? 0);
    };

    const adultDay = mapByYear(p.adultDayExpenses);
    const homeCare = mapByYear(p.homeCareExpenses);
    const assisted = mapByYear(p.assistedCareExpenses);
    const nursing  = mapByYear(p.nursingCareExpenses);
    const total    = adultDay.map((v, i) => v + homeCare[i] + assisted[i] + nursing[i]);

    this.buildLineChart(labels, adultDay, homeCare, assisted, nursing, total);
    this.buildStackedBarChart(labels, adultDay, homeCare, assisted, nursing);
    this.buildDoughnutChart();
  }

  private buildLineChart(labels: string[], adultDay: number[], homeCare: number[], assisted: number[], nursing: number[], total: number[]) {
    const el = this.ltcLineChart()?.nativeElement;
    if (!el) return;
    const config: ChartConfiguration<'line'> = {
      type: 'line',
      data: {
        labels,
        datasets: [
          { label: 'Total Cost',     data: total,    borderColor: '#4f46e5', backgroundColor: 'rgba(79,70,229,0.1)', fill: true, tension: 0.3, pointRadius: 3 },
          { label: 'Adult Day Care', data: adultDay, borderColor: '#0ea5e9', fill: false, tension: 0.3, pointRadius: 2 },
          { label: 'Home Care',      data: homeCare, borderColor: '#10b981', fill: false, tension: 0.3, pointRadius: 2 },
          { label: 'Assisted Care',  data: assisted, borderColor: '#f59e0b', fill: false, tension: 0.3, pointRadius: 2 },
          { label: 'Nursing Care',   data: nursing,  borderColor: '#ef4444', fill: false, tension: 0.3, pointRadius: 2 },
        ],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'top' } },
        scales: { y: { beginAtZero: true, ticks: { callback: v => `$${Number(v).toLocaleString()}` } } },
      },
    };
    this.charts.push(new Chart(el, config));
  }

  private buildStackedBarChart(labels: string[], adultDay: number[], homeCare: number[], assisted: number[], nursing: number[]) {
    const el = this.ltcStackedChart()?.nativeElement;
    if (!el) return;
    const config: ChartConfiguration<'bar'> = {
      type: 'bar',
      data: {
        labels,
        datasets: [
          { label: 'Adult Day Care', data: adultDay, backgroundColor: '#0ea5e9' },
          { label: 'Home Care',      data: homeCare, backgroundColor: '#10b981' },
          { label: 'Assisted Care',  data: assisted, backgroundColor: '#f59e0b' },
          { label: 'Nursing Care',   data: nursing,  backgroundColor: '#ef4444' },
        ],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'top' } },
        scales: {
          x: { stacked: true },
          y: { stacked: true, beginAtZero: true, ticks: { callback: v => `$${Number(v).toLocaleString()}` } },
        },
      },
    };
    this.charts.push(new Chart(el, config));
  }

  private buildDoughnutChart() {
    const el = this.ltcDoughnutChart()?.nativeElement;
    const cats = this.rec().ltcSnapshot?.evaluation?.categories ?? [];
    if (!el || !cats.length) return;
    const config: ChartConfiguration<'doughnut'> = {
      type: 'doughnut',
      data: {
        labels: cats.map(c => c.name),
        datasets: [{
          data: cats.map(c => c.lifetimeTotal),
          backgroundColor: ['#0ea5e9', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'],
        }],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: {
          legend: { position: 'bottom' },
          tooltip: { callbacks: { label: ctx => `${ctx.label}: $${ctx.parsed.toLocaleString()}` } },
        },
      },
    };
    this.charts.push(new Chart(el, config));
  }
}
