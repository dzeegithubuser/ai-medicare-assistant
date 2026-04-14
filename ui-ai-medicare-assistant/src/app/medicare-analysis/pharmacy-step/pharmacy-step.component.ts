import { Component, ChangeDetectionStrategy, inject, OnInit, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { DrugStateService, ChatPharmacySelectionCommand } from '../../services/drug-state.service';
import { DrugService } from '../../services/drug.service';
import { PharmacyLookupEntry } from '../../models/drug.model';

@Component({
  selector: 'app-pharmacy-step',
  templateUrl: './pharmacy-step.component.html',
  styleUrls: ['./pharmacy-step.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatIconModule, MatTooltipModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
  ],
})
export class PharmacyStepComponent implements OnInit {
  protected state = inject(DrugStateService);
  private drugService = inject(DrugService);

  // Filter/pagination state
  readonly nameFilter = signal('');
  readonly radiusFilter = signal('25');
  readonly pageSize = signal(20);
  readonly currentPage = signal(1);
  readonly radiusOptions = ['10', '25', '50', '100'];
  readonly pageSizeOptions = [10, 20, 50];

  constructor() {
    // Watch chat-driven pharmacy selection commands
    effect(() => {
      const cmd = this.state.pendingPharmacySelection();
      if (!cmd) return;
      this.state.pendingPharmacySelection.set(null);
      this.applyChatPharmacySelection(cmd);
    });
  }

  ngOnInit() {
    this.state.currentStep.set(3);
    if (this.state.pharmacyLookup() === null && !this.state.isPharmacyLookupLoading()) {
      this.loadPharmacies();
    }
  }

  loadPharmacies() {
    this.state.setPharmacyLookupLoading(true);
    this.drugService.lookupPharmacies({
      page: this.currentPage(),
      size: this.pageSize(),
      radius: this.radiusFilter(),
      name: this.nameFilter(),
    }).subscribe({
      next: (result) => {
        this.state.setPharmacyLookup(result);
        this.state.setPharmacyLookupLoading(false);
      },
      error: () => {
        this.state.setPharmacyLookupLoading(false);
      }
    });
  }

  applyFilters() {
    this.currentPage.set(1);
    this.loadPharmacies();
  }

  clearFilters() {
    this.nameFilter.set('');
    this.radiusFilter.set('25');
    this.currentPage.set(1);
    this.loadPharmacies();
  }

  goToPage(page: number) {
    const totalPages = this.state.pharmacyLookup()?.totalPages ?? 1;
    if (page < 1 || page > totalPages) return;
    this.currentPage.set(page);
    this.loadPharmacies();
  }

  onPageSizeChange(size: number) {
    this.pageSize.set(size);
    this.currentPage.set(1);
    this.loadPharmacies();
  }

  getSpotOnMapUrl(pharmacy: PharmacyLookupEntry): string {
    const query = `${pharmacy.pharmacyName},${pharmacy.address},${pharmacy.zipcode}`;
    return `https://www.google.com/maps?q=${encodeURIComponent(query)}`;
  }

  getDirectionsUrl(pharmacy: PharmacyLookupEntry): string {
    const query = `${pharmacy.pharmacyName},${pharmacy.address},${pharmacy.zipcode}`;
    return `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(query)}`;
  }

  formatDistance(distance: string): string {
    const num = parseFloat(distance);
    if (isNaN(num)) return distance;
    return num.toFixed(1) + ' mi';
  }

  /** Generate visible page numbers for pagination */
  getPageNumbers(): number[] {
    const totalPages = this.state.pharmacyLookup()?.totalPages ?? 1;
    const current = this.currentPage();
    const pages: number[] = [];
    const start = Math.max(1, current - 2);
    const end = Math.min(totalPages, current + 2);
    for (let i = start; i <= end; i++) pages.push(i);
    return pages;
  }

  // ── Chat-driven pharmacy selection ────────────────────────────────────────

  private applyChatPharmacySelection(cmd: ChatPharmacySelectionCommand): void {
    if (cmd.action === 'clearFilter') {
      this.clearFilters();
      return;
    }

    if (cmd.action === 'search' && cmd.searchTerm) {
      this.nameFilter.set(cmd.searchTerm);
      this.applyFilters();
      return;
    }

    const names = cmd.pharmacyNames?.length
      ? cmd.pharmacyNames
      : cmd.pharmacyName
        ? [cmd.pharmacyName]
        : [];

    if (cmd.action === 'select' && names.length) {
      for (const name of names) {
        const pharmacy = this.findPharmacyByName(name);
        if (pharmacy && !this.state.isLookupPharmacySelected(pharmacy.pharmacyNumber)) {
          const ok = this.state.toggleLookupPharmacy(pharmacy);
          if (!ok) break; // max 5
        }
      }
      return;
    }

    if (cmd.action === 'remove' && names.length) {
      for (const name of names) {
        const selected = this.state.selectedLookupPharmacies().find(
          p => p.pharmacyName.toLowerCase().includes(name.toLowerCase()),
        );
        if (selected) {
          this.state.toggleLookupPharmacy(selected);
        }
      }
      return;
    }
  }

  private findPharmacyByName(name: string): PharmacyLookupEntry | undefined {
    const pharmacies = this.state.pharmacyLookup()?.pharmacies ?? [];
    const lower = name.toLowerCase();
    // Exact match first
    const exact = pharmacies.find(p => p.pharmacyName.toLowerCase() === lower);
    if (exact) return exact;
    // Partial match — prefer closest (first in list, already sorted by distance)
    return pharmacies.find(p => p.pharmacyName.toLowerCase().includes(lower));
  }
}
