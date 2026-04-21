import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { ChatWizardService } from '../services/chat-wizard.service';
import { RecommendationService } from '../services/recommendation.service';
import { RecommendationSummaryResponse, RecommendationCategory } from '../models/recommendation.model';
import { AppRoutes } from '../app-routes.const';

@Component({
  selector: 'app-recommendation',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, CurrencyPipe, DatePipe,
    MatIconModule, MatButtonModule, MatButtonToggleModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatTooltipModule, MatProgressSpinnerModule,
  ],
  templateUrl: './recommendation.component.html',
})
export class RecommendationComponent implements OnInit {
  private recommendationService = inject(RecommendationService);
  private router = inject(Router);
  private chatWizard = inject(ChatWizardService);

  readonly recommendations = signal<RecommendationSummaryResponse[]>([]);
  readonly loadingRecommendations = signal(true);
  readonly topMessage = signal<string | null>(null);

  // ── Filter / Sort / Pagination ─────────────────────────────────────────────
  readonly searchQuery = signal('');
  readonly filterType = signal<'all' | RecommendationCategory>('all');
  readonly sortBy = signal<'newest' | 'oldest' | 'name-asc' | 'name-desc' | 'cost-high' | 'cost-low'>('newest');
  readonly pageSize = signal(6);
  readonly currentPage = signal(1);

  readonly typeOptions: { label: string; value: 'all' | RecommendationCategory }[] = [
    { label: 'All', value: 'all' },
    { label: 'Medicare', value: 'medicare' },
    { label: 'Long Term Care', value: 'longterm' },
  ];

  // ── Compare basket (max 2) ────────────────────────────────────────────────
  readonly selectedForCompare = signal<RecommendationSummaryResponse[]>([]);

  readonly compareCount = computed(() => this.selectedForCompare().length);

  readonly compareReady = computed(() => this.selectedForCompare().length === 2);

  readonly compareTypeLabel = computed<string>(() => {
    const sel = this.selectedForCompare();
    if (sel.length < 1) return '';
    const label = (t?: RecommendationCategory) => t === 'longterm' ? 'Long Term Care' : 'Medicare';
    if (sel.length === 1) return `${label(sel[0].type)} analysis`;
    return `${label(sel[0].type)} vs ${label(sel[1].type)}`;
  });

  readonly compareTypeIcon = computed<string>(() => {
    const sel = this.selectedForCompare();
    if (sel.length < 2) return 'compare_arrows';
    const both = sel.every(r => !r.type || r.type === 'medicare');
    const bothLt = sel.every(r => r.type === 'longterm');
    if (both) return 'analytics';
    if (bothLt) return 'timeline';
    return 'compare_arrows';
  });

  // ── Filtered / sorted / paged computeds ───────────────────────────────────
  readonly filteredAndSorted = computed(() => {
    let list = this.recommendations();
    const q = this.searchQuery().toLowerCase().trim();
    const type = this.filterType();
    if (q) list = list.filter(r => r.name.toLowerCase().includes(q));
    if (type !== 'all') list = list.filter(r => (r.type ?? 'medicare') === type);
    switch (this.sortBy()) {
      case 'oldest':    return [...list].sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
      case 'name-asc':  return [...list].sort((a, b) => a.name.localeCompare(b.name));
      case 'name-desc': return [...list].sort((a, b) => b.name.localeCompare(a.name));
      case 'cost-high': return [...list].sort((a, b) => (b.lifetimeTotal ?? 0) - (a.lifetimeTotal ?? 0));
      case 'cost-low':  return [...list].sort((a, b) => (a.lifetimeTotal ?? 0) - (b.lifetimeTotal ?? 0));
      default:          return [...list].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    }
  });

  readonly totalItems  = computed(() => this.filteredAndSorted().length);
  readonly totalPages  = computed(() => Math.max(1, Math.ceil(this.totalItems() / this.pageSize())));
  readonly pageStart   = computed(() => this.totalItems() === 0 ? 0 : (this.currentPage() - 1) * this.pageSize() + 1);
  readonly pageEnd     = computed(() => Math.min(this.currentPage() * this.pageSize(), this.totalItems()));

  readonly pagedRecommendations = computed(() => {
    const page = Math.min(this.currentPage(), this.totalPages());
    const size = this.pageSize();
    return this.filteredAndSorted().slice((page - 1) * size, page * size);
  });

  isInCompare(id: string): boolean {
    return this.selectedForCompare().some(r => r.id === id);
  }

  canAdd(id: string): boolean {
    return !this.isInCompare(id) && this.compareCount() < 2;
  }

  addToCompare(rec: RecommendationSummaryResponse) {
    if (!this.canAdd(rec.id)) return;
    this.selectedForCompare.update(s => [...s, rec]);
  }

  removeFromCompare(id: string) {
    this.selectedForCompare.update(s => s.filter(r => r.id !== id));
  }

  clearCompare() {
    this.selectedForCompare.set([]);
  }

  launchCompare() {
    const ids = this.selectedForCompare().map(r => r.id);
    if (ids.length !== 2) return;
    this.router.navigate(['/saved/compare'], { queryParams: { ids: ids.join(',') } });
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit() {
    this.refreshRecommendations();
  }

  refreshRecommendations() {
    this.loadingRecommendations.set(true);
    this.recommendationService.getAll().subscribe({
      next: (data) => {
        this.recommendations.set(data);
        this.loadingRecommendations.set(false);
      },
      error: () => this.loadingRecommendations.set(false),
    });
  }

  // ── Navigation helpers ────────────────────────────────────────────────────
  startMedicareAnalysis() {
    this.router.navigate([AppRoutes.abs.PROFILE]);
    this.chatWizard.requestMedicareAnalysisEntry();
  }

  goToAnalysis() {
    this.startMedicareAnalysis();
  }

  startLongTermAnalysis() {
    this.clearTopMessage();
    this.router.navigate([AppRoutes.abs.LTC_PROFILE]);
  }

  clearTopMessage() {
    this.topMessage.set(null);
  }

  viewDetails(id: string) {
    this.router.navigate(['/saved', id]);
  }

  categoryLabel(type?: RecommendationCategory): string {
    return type === 'longterm' ? 'Long Term Care' : 'Medicare';
  }

  categoryColor(type?: RecommendationCategory): string {
    return type === 'longterm' ? 'bg-purple-100 text-purple-700' : 'bg-cyan-100 text-cyan-700';
  }

  planTypeLabel(planType: string): string {
    switch (planType.toLowerCase()) {
      case 'partd':   return 'Part D';
      case 'ma':      return 'MA';
      case 'medigap': return 'Medigap';
      default:        return planType;
    }
  }

  planTypeBadgeClass(planType: string): string {
    switch (planType.toLowerCase()) {
      case 'partd':   return 'bg-blue-100 text-blue-700';
      case 'ma':      return 'bg-cyan-100 text-cyan-700';
      case 'medigap': return 'bg-indigo-100 text-indigo-700';
      default:        return 'bg-gray-100 text-gray-600';
    }
  }

  fmtHealthProfile(v?: number): string {
    const labels: Record<number, string> = {
      1: 'Best Health', 2: 'Good Health', 3: 'Average Health',
      4: 'Below Average', 5: 'Poor Health',
    };
    return v != null ? (labels[v] ?? `Health ${v}`) : '—';
  }

  // ── Filter / Sort / Pagination methods ───────────────────────────────────
  setSearch(q: string)    { this.searchQuery.set(q);  this.currentPage.set(1); }
  setFilterType(t: 'all' | RecommendationCategory) { this.filterType.set(t); this.currentPage.set(1); }
  setSortBy(s: 'newest' | 'oldest' | 'name-asc' | 'name-desc' | 'cost-high' | 'cost-low') { this.sortBy.set(s); this.currentPage.set(1); }
  setPageSize(n: number)  { this.pageSize.set(n);    this.currentPage.set(1); }
  prevPage() { this.currentPage.update(p => Math.max(1, p - 1)); }
  nextPage() { this.currentPage.update(p => Math.min(this.totalPages(), p + 1)); }
  clearFilters() { this.searchQuery.set(''); this.filterType.set('all'); this.currentPage.set(1); }
}
