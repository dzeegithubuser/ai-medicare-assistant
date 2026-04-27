import { Component, ChangeDetectionStrategy, input } from '@angular/core';

@Component({
  selector: 'app-kpi-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rounded-xl border border-gray-200 bg-white p-4 text-center shadow-sm">
      <p class="text-xs uppercase tracking-wide mb-1" [class]="labelClass()">{{ label() }}</p>
      <p class="font-bold" [class]="valueClass()">{{ value() }}</p>
      @if (subtitle()) {
        <p class="text-xs text-gray-500 mt-1">{{ subtitle() }}</p>
      }
    </div>
  `,
})
export class KpiCardComponent {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly subtitle = input<string>();
  readonly labelClass = input('text-gray-500');
  readonly valueClass = input('text-lg text-gray-900');
}
