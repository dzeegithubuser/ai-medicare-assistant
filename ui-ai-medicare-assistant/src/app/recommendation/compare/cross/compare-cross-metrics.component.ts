import { Component, ChangeDetectionStrategy, input, inject, computed } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { RecommendationResponse, RecommendationCategory } from '../../../models/recommendation.model';
import { LABEL_A, LABEL_B } from '../compare-helpers';

@Component({
  selector: 'app-compare-cross-metrics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  providers: [CurrencyPipe],
  template: `
    <div class="grid grid-cols-2 md:grid-cols-3 gap-3 mb-4">
      @for (m of allMetrics(); track m.label) {
        <div class="rounded-lg border-2 border-gray-400 bg-gray-50/60 p-3">
          <p class="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">{{ m.label }}</p>
          <div class="grid grid-cols-2 gap-2 text-sm">
            <div>
              <p class="text-[10px] text-orange-600 truncate">{{ labelA }}</p>
              <p class="font-bold text-gray-900">{{ m.left }}</p>
            </div>
            <div class="text-right">
              <p class="text-[10px] text-green-600 truncate">{{ labelB }}</p>
              <p class="font-bold text-gray-900">{{ m.right }}</p>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class CompareCrossMetricsComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  readonly labelA = LABEL_A;
  readonly labelB = LABEL_B;

  private currencyPipe = inject(CurrencyPipe);

  readonly leftType = computed<RecommendationCategory>(() => this.left().type ?? 'medicare');
  readonly rightType = computed<RecommendationCategory>(() => this.right().type ?? 'medicare');

  private fmt(value: number, format = '1.0-0'): string {
    return this.currencyPipe.transform(value, 'USD', 'symbol', format) ?? '$0';
  }

  private lifetimeCost(rec: RecommendationResponse, type: RecommendationCategory): number {
    return type === 'longterm'
      ? (rec.ltcSnapshot?.totalCost ?? 0)
      : (rec.lastCostSnapshot?.lifetimeTotal ?? 0);
  }

  private presentValue(rec: RecommendationResponse, type: RecommendationCategory): number {
    return type === 'longterm'
      ? (rec.ltcSnapshot?.totalPresentValue ?? 0)
      : (rec.lastCostSnapshot?.presentValue ?? 0);
  }

  private avgAnnualCost(rec: RecommendationResponse, type: RecommendationCategory): number {
    if (type === 'longterm') return rec.ltcSnapshot?.evaluation?.averageAnnualCost ?? 0;
    const years = rec.lastCostSnapshot?.yearlyDetails?.length;
    if (!years || !rec.lastCostSnapshot?.lifetimeTotal) return 0;
    return rec.lastCostSnapshot.lifetimeTotal / years;
  }

  readonly allMetrics = computed(() => [...this.costMetrics(), ...this.profileMetrics()]);

  readonly costMetrics = computed(() => {
    const l = this.left();
    const r = this.right();
    const lt = this.leftType();
    const rt = this.rightType();

    return [
      { label: 'Lifetime Cost', left: this.fmt(this.lifetimeCost(l, lt)), right: this.fmt(this.lifetimeCost(r, rt)) },
      { label: 'Present Value', left: this.fmt(this.presentValue(l, lt)), right: this.fmt(this.presentValue(r, rt)) },
    ];
  });

  readonly profileMetrics = computed(() => {
    const l = this.left();
    const r = this.right();
    const lt = this.leftType();
    const rt = this.rightType();

    return [
      { label: 'ZIP Code', left: l.profile.zipCode, right: r.profile.zipCode },
    ];
  });
}
