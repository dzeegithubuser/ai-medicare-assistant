import {
  Component, ChangeDetectionStrategy, inject, signal, computed, OnInit,
} from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RecommendationService } from '../services/recommendation.service';
import { RecommendationResponse, RecommendationCategory } from '../models/recommendation.model';
import { CompareMedicareComponent } from './compare-medicare.component';
import { CompareLtcComponent } from './compare-ltc.component';
import { CompareCrossComponent } from './compare-cross.component';

type ComparisonMode = 'medicare' | 'longterm' | 'cross';

@Component({
  selector: 'app-recommendation-compare',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, CurrencyPipe, DatePipe,
    MatIconModule, MatButtonModule, MatCardModule, MatProgressSpinnerModule,
    CompareMedicareComponent, CompareLtcComponent, CompareCrossComponent,
  ],
  templateUrl: './recommendation-compare.component.html',
})
export class RecommendationCompareComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private recommendationService = inject(RecommendationService);

  readonly left = signal<RecommendationResponse | null>(null);
  readonly right = signal<RecommendationResponse | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  // ── Type inference ────────────────────────────────────────────────────────
  private typeOf(rec: RecommendationResponse | null): RecommendationCategory {
    if (rec?.type) return rec.type;
    return 'medicare';
  }

  readonly leftType = computed<RecommendationCategory>(() => this.typeOf(this.left()));
  readonly rightType = computed<RecommendationCategory>(() => this.typeOf(this.right()));

  readonly mode = computed<ComparisonMode>(() => {
    const l = this.leftType();
    const r = this.rightType();
    if (l === 'medicare' && r === 'medicare') return 'medicare';
    if (l === 'longterm' && r === 'longterm') return 'longterm';
    return 'cross';
  });

  readonly modeLabel = computed<string>(() => {
    switch (this.mode()) {
      case 'medicare': return 'Medicare vs Medicare';
      case 'longterm': return 'Long Term Care vs Long Term Care';
      case 'cross': return 'Cross-type Comparison';
    }
  });

  readonly modeBadgeClass = computed<string>(() => {
    switch (this.mode()) {
      case 'medicare': return 'bg-teal-100 text-teal-800';
      case 'longterm': return 'bg-purple-100 text-purple-800';
      case 'cross': return 'bg-orange-100 text-orange-800';
    }
  });

  typeBadgeClass(type: RecommendationCategory): string {
    return type === 'longterm' ? 'bg-purple-100 text-purple-700' : 'bg-teal-100 text-teal-700';
  }

  typeLabel(type: RecommendationCategory): string {
    return type === 'longterm' ? 'Long Term Care' : 'Medicare';
  }

  // ── Cost helpers (hero header) ────────────────────────────────────────────
  lifetimeCost(rec: RecommendationResponse | null, type: RecommendationCategory): number {
    if (!rec) return 0;
    return type === 'longterm'
      ? (rec.ltcSnapshot?.totalCost ?? 0)
      : (rec.lastCostSnapshot?.lifetimeTotal ?? 0);
  }

  readonly leftLifetime = computed(() => this.lifetimeCost(this.left(), this.leftType()));
  readonly rightLifetime = computed(() => this.lifetimeCost(this.right(), this.rightType()));
  readonly costDelta = computed(() => this.leftLifetime() - this.rightLifetime());

  readonly winner = computed<'left' | 'right' | 'tie'>(() => {
    const d = this.costDelta();
    if (d === 0) return 'tie';
    return d < 0 ? 'left' : 'right';
  });

  readonly winnerName = computed<string>(() => {
    const w = this.winner();
    if (w === 'tie') return 'Tied';
    return w === 'left' ? (this.left()?.name ?? 'Left') : (this.right()?.name ?? 'Right');
  });

  readonly winnerSavings = computed(() => Math.abs(this.costDelta()));

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    const idsRaw = this.route.snapshot.queryParamMap.get('ids') ?? '';
    const ids = idsRaw.split(',').map(s => s.trim()).filter(Boolean);
    if (ids.length !== 2) {
      this.error.set('Please select exactly 2 recommendations to compare.');
      this.loading.set(false);
      return;
    }

    forkJoin([
      this.recommendationService.getById(ids[0]),
      this.recommendationService.getById(ids[1]),
    ]).subscribe({
      next: ([left, right]) => {
        this.left.set(left);
        this.right.set(right);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load recommendations for comparison.');
        this.loading.set(false);
      },
    });
  }

  goBack(): void {
    this.router.navigate(['/saved']);
  }
}
