import { Component, ChangeDetectionStrategy, input, inject, computed } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { RecommendationResponse } from '../../../models/recommendation.model';
import { LABEL_A, LABEL_B } from '../compare-helpers';

@Component({
  selector: 'app-compare-ltc-metrics',
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
export class CompareLtcMetricsComponent {
  readonly left = input.required<RecommendationResponse>();
  readonly right = input.required<RecommendationResponse>();

  readonly labelA = LABEL_A;
  readonly labelB = LABEL_B;

  private currencyPipe = inject(CurrencyPipe);

  private fmt(value: number, format = '1.0-0'): string {
    return this.currencyPipe.transform(value, 'USD', 'symbol', format) ?? '$0';
  }

  readonly allMetrics = computed(() => [...this.costMetrics(), ...this.profileMetrics()]);

  readonly costMetrics = computed(() => {
    const l = this.left();
    const r = this.right();
    const leftTotal = l.ltcSnapshot?.totalCost ?? 0;
    const rightTotal = r.ltcSnapshot?.totalCost ?? 0;
    const leftPV = l.ltcSnapshot?.totalPresentValue ?? 0;
    const rightPV = r.ltcSnapshot?.totalPresentValue ?? 0;
    const leftAvg = l.ltcSnapshot?.evaluation?.averageAnnualCost ?? 0;
    const rightAvg = r.ltcSnapshot?.evaluation?.averageAnnualCost ?? 0;

    return [
      { label: 'Lifetime Cost', left: this.fmt(leftTotal), right: this.fmt(rightTotal) },
      { label: 'Present Value', left: this.fmt(leftPV), right: this.fmt(rightPV) },
    ];
  });

  readonly profileMetrics = computed(() => {
    const l = this.left();
    const r = this.right();
    const leftYears = l.ltcSnapshot?.evaluation?.projectionYears;
    const rightYears = r.ltcSnapshot?.evaluation?.projectionYears;

    return [
      { label: 'Projection Years', left: leftYears ? `${leftYears}` : 'N/A', right: rightYears ? `${rightYears}` : 'N/A' },
      { label: 'ZIP Code', left: l.profile.zipCode, right: r.profile.zipCode },
    ];
  });
}
