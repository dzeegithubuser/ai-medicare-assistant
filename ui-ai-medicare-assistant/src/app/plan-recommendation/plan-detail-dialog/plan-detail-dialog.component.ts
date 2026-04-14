import { Component, ChangeDetectionStrategy, Inject } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { RecommendationListItem, PharmacyWiseRecommendation } from '../../models/part-d-plan.model';
import { MedigapPlan } from '../../models/medigap-plan.model';

export type PlanDetailData =
  | { type: 'partd'; plan: RecommendationListItem }
  | { type: 'medigap'; plan: MedigapPlan }
  | { type: 'ma'; plan: RecommendationListItem };

@Component({
  selector: 'app-plan-detail-dialog',
  templateUrl: './plan-detail-dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatIconModule, MatButtonModule, MatDividerModule, CurrencyPipe],
})
export class PlanDetailDialogComponent {
  constructor(
    @Inject(MAT_DIALOG_DATA) public data: PlanDetailData,
    private dialogRef: MatDialogRef<PlanDetailDialogComponent>
  ) {}

  close() { this.dialogRef.close(); }

  // Helpers
  get isPartD(): boolean { return this.data.type === 'partd' || this.data.type === 'ma'; }
  get isMedigap(): boolean { return this.data.type === 'medigap'; }

  get partDPlan(): RecommendationListItem { return this.data.plan as RecommendationListItem; }
  get medigapPlan(): MedigapPlan { return this.data.plan as MedigapPlan; }

  get firstPharmacy(): PharmacyWiseRecommendation | null {
    if (!this.isPartD) return null;
    return this.partDPlan.pharmacyWiseRecommendations?.[0] ?? null;
  }
}
