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
import { AdminService } from '../services/admin.service';
import { UserSummary } from '../models/role-management.model';
import { LoadingSpinnerComponent } from '../shared/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '../shared/empty-state/empty-state.component';
import { CreateAdminUserDialogComponent } from './create-admin-user-dialog.component';
import {
  ConfirmDeleteDialogComponent,
  ConfirmDeleteDialogData,
} from '../shared/confirm-delete-dialog/confirm-delete-dialog.component';

type SortOption = 'newest' | 'oldest' | 'name-asc' | 'name-desc';

@Component({
  selector: 'app-admin-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    MatButtonModule, MatButtonToggleModule, MatCardModule, MatDialogModule,
    MatFormFieldModule, MatIconModule, MatInputModule, MatProgressSpinnerModule,
    MatSelectModule, MatTooltipModule,
    LoadingSpinnerComponent, EmptyStateComponent,
  ],
  templateUrl: './admin-home.component.html',
})
export class AdminHomeComponent {
  private adminService = inject(AdminService);
  private dialog = inject(MatDialog);

  protected users = signal<UserSummary[]>([]);
  protected loading = signal(false);
  protected error = signal('');

  protected searchQuery = signal('');
  protected sortBy = signal<SortOption>('newest');
  protected currentPage = signal(1);
  protected pageSize = signal(6);

  protected filtered = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const sort = this.sortBy();
    const list = this.users().filter(u => {
      if (!q) return true;
      return (`${u.firstName} ${u.lastName}`).toLowerCase().includes(q)
        || u.email.toLowerCase().includes(q);
    });
    return [...list].sort((a, b) => {
      switch (sort) {
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
    this.loadUsers();
  }

  protected loadUsers() {
    this.loading.set(true);
    this.error.set('');
    this.adminService.listFpgAdminUsers().subscribe({
      next: users => { this.users.set(users); this.loading.set(false); },
      error: err => { this.error.set(err.error?.message ?? 'Failed to load FPG admins.'); this.loading.set(false); },
    });
  }

  protected openCreateUser() {
    this.dialog.open(CreateAdminUserDialogComponent, { autoFocus: 'first-tabbable', restoreFocus: true })
      .afterClosed().subscribe(created => {
        if (created) this.users.update(list => [created, ...list]);
      });
  }

  protected openDeleteUser(u: UserSummary) {
    const data: ConfirmDeleteDialogData = {
      title: 'Remove FPG admin',
      subject: `${u.firstName} ${u.lastName} (${u.email})`,
      warning:
        'This removes the FPG-admin account and their auto-created group. ' +
        'If their group still has financial planners attached, the request will be rejected — ' +
        'sign in as the FPG admin first and clear out the planners.',
      confirmationToken: u.email,
      confirmLabel: 'Remove admin',
    };
    this.dialog.open(ConfirmDeleteDialogComponent, { data, autoFocus: 'first-tabbable', restoreFocus: true })
      .afterClosed().subscribe(confirmed => {
        if (!confirmed) return;
        this.error.set('');
        this.adminService.deleteFpgAdminUser(u.userId).subscribe({
          next: () => this.users.update(list => list.filter(x => x.userId !== u.userId)),
          error: err => this.error.set(err.error?.message ?? 'Failed to remove FPG admin.'),
        });
      });
  }

  protected setSearch(value: string) { this.searchQuery.set(value); this.currentPage.set(1); }
  protected setSortBy(value: SortOption) { this.sortBy.set(value); this.currentPage.set(1); }
  protected setPageSize(value: number) { this.pageSize.set(value); this.currentPage.set(1); }
  protected prevPage() { this.currentPage.update(p => Math.max(1, p - 1)); }
  protected nextPage() { this.currentPage.update(p => Math.min(this.totalPages(), p + 1)); }
  protected clearFilters() { this.searchQuery.set(''); this.sortBy.set('newest'); this.currentPage.set(1); }
}
