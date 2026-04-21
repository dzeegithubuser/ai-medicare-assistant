import {
  Component, ChangeDetectionStrategy, inject, OnInit,
  ChangeDetectorRef, signal,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RecommendationService } from '../../services/recommendation.service';
import { RecommendationResponse } from '../../models/recommendation.model';
import { RecDetailLtcComponent } from './ltc/rec-detail-ltc.component';
import { RecDetailMedicareComponent } from './medicare/rec-detail-medicare.component';

@Component({
  selector: 'app-recommendation-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, DatePipe,
    MatIconModule, MatButtonModule,
    MatTooltipModule, MatProgressSpinnerModule,
    RecDetailLtcComponent, RecDetailMedicareComponent,
  ],
  templateUrl: './recommendation-detail.component.html',
  styleUrls: ['./recommendation-detail.component.scss'],
})
export class RecommendationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private recommendationService = inject(RecommendationService);
  private cdr = inject(ChangeDetectorRef);

  readonly recommendationId = this.route.snapshot.paramMap.get('id') ?? '';
  readonly recommendation = signal<RecommendationResponse | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

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

  goBack() {
    this.router.navigate(['/saved']);
  }
}