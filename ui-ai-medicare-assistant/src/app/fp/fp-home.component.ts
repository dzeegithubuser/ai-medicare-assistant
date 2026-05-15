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
import { FinancialPlannerService } from '../services/financial-planner.service';
import { AuthService } from '../services/auth.service';
import { EndUserSummary, RecommendationByUser, RecommendationSummary } from '../models/role-management.model';
import { LoadingSpinnerComponent } from '../shared/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '../shared/empty-state/empty-state.component';
import { CreateEndUserDialogComponent } from './create-end-user-dialog.component';
import {
  ConfirmDeleteDialogComponent,
  ConfirmDeleteDialogData,
} from '../shared/confirm-delete-dialog/confirm-delete-dialog.component';

type SortOption = 'newest' | 'oldest' | 'name-asc' | 'name-desc';
type FilterOption = 'all' | 'with-recs' | 'no-recs';

@Component({
  selector: 'app-fp-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    MatButtonModule, MatButtonToggleModule, MatCardModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule,
    MatSelectModule, MatTooltipModule,
    LoadingSpinnerComponent, EmptyStateComponent,
  ],
  templateUrl: './fp-home.component.html',
})
export class FpHomeComponent {
  private fpService = inject(FinancialPlannerService);
  private auth = inject(AuthService);
  private dialog = inject(MatDialog);

  protected users = signal<EndUserSummary[]>([]);
  protected recsByUser = signal<RecommendationByUser[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected actingOnUserId = signal<string | null>(null);
  protected expandedUserId = signal<string | null>(null);

  protected searchQuery = signal('');
  protected sortBy = signal<SortOption>('newest');
  protected filterBy = signal<FilterOption>('all');
  protected currentPage = signal(1);
  protected pageSize = signal(6);

  protected readonly filterOptions: { value: FilterOption; label: string }[] = [
    { value: 'all',       label: 'All' },
    { value: 'with-recs', label: 'Has analyses' },
    { value: 'no-recs',   label: 'No analyses' },
  ];

  protected recsLookup = computed(() => {
    const map = new Map<string, RecommendationByUser>();
    for (const entry of this.recsByUser()) map.set(entry.userId, entry);
    return map;
  });

  protected filtered = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const filter = this.filterBy();
    const lookup = this.recsLookup();

    const list = this.users().filter(u => {
      if (q) {
        const matches = (`${u.firstName} ${u.lastName}`).toLowerCase().includes(q)
          || u.email.toLowerCase().includes(q);
        if (!matches) return false;
      }
      const hasRecs = (lookup.get(u.userId)?.recommendations.length ?? 0) > 0;
      if (filter === 'with-recs' && !hasRecs) return false;
      if (filter === 'no-recs' && hasRecs) return false;
      return true;
    });

    return [...list].sort((a, b) => {
      switch (this.sortBy()) {
        case 'newest': return +new Date(b.createdAt) - +new Date(a.createdAt);
        case 'oldest': return +new Date(a.createdAt) - +new Date(b.createdAt);
        case 'name-asc': return `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`);
        case 'name-desc': return `${b.firstName} ${b.lastName}`.localeCompare(`${a.firstName} ${a.lastName}`);
      }
    });
  });

  protected totalItems = computed(() => this.filtered().length);
  protected totalPages = computed(() => Math.max(1, Math.ceil(this.totalItems() / this.pageSize())));
  protected pageStart = computed(() => this.totalItems() === 0 ? 0 : (this.currentPage() - 1) * this.pageSize() + 1);
  protected pageEnd = computed(() => Math.min(this.currentPage() * this.pageSize(), this.totalItems()));
  protected paged = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  constructor() {
    this.loadAll();
  }

  protected loadAll() {
    this.loading.set(true);
    this.error.set('');
    this.fpService.listEndUsers().subscribe({
      next: users => this.users.set(users),
      error: err => this.error.set(err.error?.message ?? 'Failed to load users.'),
    });
    this.fpService.listRecommendations().subscribe({
      next: recs => { this.recsByUser.set(recs); this.loading.set(false); },
      error: err => { this.error.set(err.error?.message ?? 'Failed to load recommendations.'); this.loading.set(false); },
    });
  }

  protected openCreateUser() {
    this.dialog.open(CreateEndUserDialogComponent, { autoFocus: 'first-tabbable', restoreFocus: true })
      .afterClosed().subscribe(user => {
        if (!user) return;
        this.users.update(list => [user, ...list]);
        this.actingOnUserId.set(user.userId);
        this.auth.impersonate(user.userId).subscribe({
          next: () => window.location.assign('/saved'),
          error: err => {
            this.actingOnUserId.set(null);
            this.error.set(err.error?.message ?? 'User created but impersonation failed.');
          },
        });
      });
  }

  protected openDeleteEndUser(u: EndUserSummary) {
    const data: ConfirmDeleteDialogData = {
      title: 'Remove end-user',
      subject: `${u.firstName} ${u.lastName} (${u.email})`,
      warning:
        'This permanently deletes the user along with their profile, chat history, ' +
        'saved analyses, recommendations, prescription selections, and LTC selections.',
      confirmationToken: u.email,
      confirmLabel: 'Remove user',
    };
    this.dialog.open(ConfirmDeleteDialogComponent, { data, autoFocus: 'first-tabbable', restoreFocus: true })
      .afterClosed().subscribe(confirmed => {
        if (!confirmed) return;
        this.error.set('');
        this.fpService.deleteEndUser(u.userId).subscribe({
          next: () => {
            this.users.update(list => list.filter(x => x.userId !== u.userId));
            this.recsByUser.update(list => list.filter(g => g.userId !== u.userId));
            if (this.expandedUserId() === u.userId) this.expandedUserId.set(null);
          },
          error: err => this.error.set(err.error?.message ?? 'Failed to remove end-user.'),
        });
      });
  }

  protected impersonateUser(userId: string) {
    this.error.set('');
    this.actingOnUserId.set(userId);
    this.auth.impersonate(userId).subscribe({
      next: () => window.location.assign('/saved'),
      error: err => {
        this.actingOnUserId.set(null);
        this.error.set(err.error?.message ?? 'Impersonation failed.');
      },
    });
  }

  protected deleteRecommendation(rec: RecommendationSummary) {
    const data: ConfirmDeleteDialogData = {
      title: 'Delete recommendation',
      subject: rec.name || '(unnamed)',
      warning: 'This permanently removes the saved analysis. Other recommendations for this user are untouched.',
      confirmationToken: 'delete',
      inputLabel: 'Type "delete" to confirm',
      confirmLabel: 'Delete',
    };
    this.dialog.open(ConfirmDeleteDialogComponent, { data, autoFocus: 'first-tabbable', restoreFocus: true })
      .afterClosed().subscribe(confirmed => {
        if (!confirmed) return;
        this.error.set('');
        this.fpService.deleteRecommendation(rec.id).subscribe({
          next: () => this.recsByUser.update(list =>
            list
              .map(g => ({ ...g, recommendations: g.recommendations.filter(r => r.id !== rec.id) }))
              .filter(g => g.recommendations.length > 0)
          ),
          error: err => this.error.set(err.error?.message ?? 'Failed to delete recommendation.'),
        });
      });
  }

  protected toggleExpand(userId: string) {
    this.expandedUserId.update(current => current === userId ? null : userId);
  }

  protected setSearch(value: string) { this.searchQuery.set(value); this.currentPage.set(1); }
  protected setSortBy(value: SortOption) { this.sortBy.set(value); this.currentPage.set(1); }
  protected setFilterBy(value: FilterOption) { this.filterBy.set(value); this.currentPage.set(1); }
  protected setPageSize(value: number) { this.pageSize.set(value); this.currentPage.set(1); }
  protected prevPage() { this.currentPage.update(p => Math.max(1, p - 1)); }
  protected nextPage() { this.currentPage.update(p => Math.min(this.totalPages(), p + 1)); }
  protected clearFilters() {
    this.searchQuery.set('');
    this.sortBy.set('newest');
    this.filterBy.set('all');
    this.currentPage.set(1);
  }
}
