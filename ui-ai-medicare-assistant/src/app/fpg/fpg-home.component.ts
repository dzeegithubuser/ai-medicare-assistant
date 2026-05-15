import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FinancialPlannerGroupService } from '../services/financial-planner-group.service';
import {
  EndUserSummary,
  FpSummary,
  FpgSummary,
  RecommendationByUser,
} from '../models/role-management.model';
import { LoadingSpinnerComponent } from '../shared/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '../shared/empty-state/empty-state.component';
import { CreateFpDialogComponent } from './create-fp-dialog.component';
import {
  ConfirmDeleteDialogComponent,
  ConfirmDeleteDialogData,
} from '../shared/confirm-delete-dialog/confirm-delete-dialog.component';

type View = 'planners' | 'end-users' | 'recommendations';
type SortOption = 'newest' | 'oldest' | 'name-asc' | 'name-desc';

@Component({
  selector: 'app-fpg-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    MatButtonModule, MatButtonToggleModule, MatCardModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule,
    MatSelectModule, MatTooltipModule,
    LoadingSpinnerComponent, EmptyStateComponent,
  ],
  templateUrl: './fpg-home.component.html',
})
export class FpgHomeComponent {
  private fpgService = inject(FinancialPlannerGroupService);
  private dialog = inject(MatDialog);

  protected group = signal<FpgSummary | null>(null);
  protected fps = signal<FpSummary[]>([]);
  protected groupEndUsers = signal<EndUserSummary[] | null>(null);
  protected groupRecs = signal<RecommendationByUser[] | null>(null);
  protected loading = signal(false);
  protected error = signal('');

  protected view = signal<View>('planners');
  protected searchQuery = signal('');
  protected sortBy = signal<SortOption>('newest');
  protected currentPage = signal(1);
  protected pageSize = signal(6);

  protected readonly viewOptions: { value: View; label: string; icon: string }[] = [
    { value: 'planners',        label: 'Financial Planners', icon: 'badge' },
    { value: 'end-users',       label: 'End-Users',          icon: 'group' },
    { value: 'recommendations', label: 'Recommendations',    icon: 'bookmarks' },
  ];

  protected filteredFps = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const list = this.fps().filter(fp => {
      if (!q) return true;
      return (`${fp.firstName} ${fp.lastName}`).toLowerCase().includes(q)
        || fp.email.toLowerCase().includes(q)
        || (fp.phone ?? '').toLowerCase().includes(q);
    });
    return this.applySort(list, fp => `${fp.firstName} ${fp.lastName}`, fp => fp.createdAt);
  });

  protected filteredEndUsers = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const list = (this.groupEndUsers() ?? []).filter(u => {
      if (!q) return true;
      return (`${u.firstName} ${u.lastName}`).toLowerCase().includes(q)
        || u.email.toLowerCase().includes(q);
    });
    return this.applySort(list, u => `${u.firstName} ${u.lastName}`, u => u.createdAt);
  });

  protected filteredRecs = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const list = (this.groupRecs() ?? []).filter(g => {
      if (!q) return true;
      return (`${g.firstName} ${g.lastName}`).toLowerCase().includes(q)
        || g.email.toLowerCase().includes(q)
        || g.recommendations.some(r => (r.name ?? '').toLowerCase().includes(q));
    });
    return [...list].sort((a, b) => {
      const aDate = a.recommendations[0]?.createdAt ?? '';
      const bDate = b.recommendations[0]?.createdAt ?? '';
      switch (this.sortBy()) {
        case 'newest': return +new Date(bDate) - +new Date(aDate);
        case 'oldest': return +new Date(aDate) - +new Date(bDate);
        case 'name-asc': return `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`);
        case 'name-desc': return `${b.firstName} ${b.lastName}`.localeCompare(`${a.firstName} ${a.lastName}`);
      }
    });
  });

  protected totalItems = computed(() => {
    switch (this.view()) {
      case 'planners':        return this.filteredFps().length;
      case 'end-users':       return this.filteredEndUsers().length;
      case 'recommendations': return this.filteredRecs().length;
    }
  });
  protected totalPages = computed(() => Math.max(1, Math.ceil(this.totalItems() / this.pageSize())));
  protected pageStart = computed(() => this.totalItems() === 0 ? 0 : (this.currentPage() - 1) * this.pageSize() + 1);
  protected pageEnd = computed(() => Math.min(this.currentPage() * this.pageSize(), this.totalItems()));

  protected pagedFps = computed(() => this.page(this.filteredFps()));
  protected pagedEndUsers = computed(() => this.page(this.filteredEndUsers()));
  protected pagedRecs = computed(() => this.page(this.filteredRecs()));

  protected sourceLoading = computed(() => {
    switch (this.view()) {
      case 'planners':        return this.loading();
      case 'end-users':       return this.groupEndUsers() === null;
      case 'recommendations': return this.groupRecs() === null;
    }
  });

  protected sourceEmpty = computed(() => {
    switch (this.view()) {
      case 'planners':        return this.fps().length === 0;
      case 'end-users':       return (this.groupEndUsers() ?? []).length === 0;
      case 'recommendations': return (this.groupRecs() ?? []).length === 0;
    }
  });

  constructor() {
    this.loadAll();
  }

  protected loadAll() {
    this.loading.set(true);
    this.error.set('');
    this.fpgService.getMyGroup().subscribe({ next: g => this.group.set(g), error: () => {} });
    this.fpgService.listFinancialPlanners().subscribe({
      next: fps => { this.fps.set(fps); this.loading.set(false); },
      error: err => { this.error.set(err.error?.message ?? 'Failed to load.'); this.loading.set(false); },
    });
  }

  protected setView(value: View) {
    this.view.set(value);
    this.currentPage.set(1);
    if (value === 'end-users' && this.groupEndUsers() === null) {
      this.fpgService.listGroupEndUsers().subscribe({
        next: users => this.groupEndUsers.set(users),
        error: err => this.error.set(err.error?.message ?? 'Failed to load end-users.'),
      });
    }
    if (value === 'recommendations' && this.groupRecs() === null) {
      this.fpgService.listGroupRecommendations().subscribe({
        next: recs => this.groupRecs.set(recs),
        error: err => this.error.set(err.error?.message ?? 'Failed to load recommendations.'),
      });
    }
  }

  protected openCreateFp() {
    this.dialog.open(CreateFpDialogComponent, { autoFocus: 'first-tabbable', restoreFocus: true })
      .afterClosed().subscribe(created => {
        if (created) this.fps.update(list => [created, ...list]);
      });
  }

  protected deleteFp(fp: FpSummary) {
    const data: ConfirmDeleteDialogData = {
      title: 'Remove financial planner',
      subject: `${fp.firstName} ${fp.lastName} (${fp.email})`,
      warning:
        'This removes the planner from your group. ' +
        'The request will be rejected if the planner still has end-users assigned — ' +
        'sign in as the planner first (or impersonate them) and clear out their end-users.',
      confirmationToken: fp.email,
      confirmLabel: 'Remove planner',
    };
    this.dialog.open(ConfirmDeleteDialogComponent, { data, autoFocus: 'first-tabbable', restoreFocus: true })
      .afterClosed().subscribe(confirmed => {
        if (!confirmed) return;
        this.error.set('');
        this.fpgService.deleteFinancialPlanner(fp.userId).subscribe({
          next: () => this.fps.update(list => list.filter(f => f.userId !== fp.userId)),
          error: err => this.error.set(err.error?.message ?? 'Failed to remove planner.'),
        });
      });
  }

  protected setSearch(value: string) { this.searchQuery.set(value); this.currentPage.set(1); }
  protected setSortBy(value: SortOption) { this.sortBy.set(value); this.currentPage.set(1); }
  protected setPageSize(value: number) { this.pageSize.set(value); this.currentPage.set(1); }
  protected prevPage() { this.currentPage.update(p => Math.max(1, p - 1)); }
  protected nextPage() { this.currentPage.update(p => Math.min(this.totalPages(), p + 1)); }
  protected clearFilters() { this.searchQuery.set(''); this.sortBy.set('newest'); this.currentPage.set(1); }

  private page<T>(list: T[]): T[] {
    const start = (this.currentPage() - 1) * this.pageSize();
    return list.slice(start, start + this.pageSize());
  }

  private applySort<T>(list: T[], nameOf: (item: T) => string, dateOf: (item: T) => string): T[] {
    return [...list].sort((a, b) => {
      switch (this.sortBy()) {
        case 'newest': return +new Date(dateOf(b)) - +new Date(dateOf(a));
        case 'oldest': return +new Date(dateOf(a)) - +new Date(dateOf(b));
        case 'name-asc': return nameOf(a).localeCompare(nameOf(b));
        case 'name-desc': return nameOf(b).localeCompare(nameOf(a));
      }
    });
  }
}
