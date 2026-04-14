import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { DeltaResult } from '../../models/orchestrator.model';

@Component({
  selector: 'app-delta-display',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, MatIconModule],
  template: `
    @if (delta(); as d) {
      <div class="bg-white border rounded-2xl shadow-sm overflow-hidden"
           [class]="lifetimeDiff() > 0 ? 'border-red-200' : lifetimeDiff() < 0 ? 'border-green-200' : 'border-gray-200'">

        <!-- Header -->
        <div class="flex items-center gap-2 px-4 py-2.5"
             [class]="lifetimeDiff() > 0 ? 'bg-red-50' : lifetimeDiff() < 0 ? 'bg-green-50' : 'bg-gray-50'">
          <mat-icon class="!text-base !w-4 !h-4"
                    [class]="lifetimeDiff() > 0 ? 'text-red-600' : lifetimeDiff() < 0 ? 'text-green-600' : 'text-gray-500'">
            {{ lifetimeDiff() > 0 ? 'trending_up' : lifetimeDiff() < 0 ? 'trending_down' : 'trending_flat' }}
          </mat-icon>
          <span class="text-xs font-semibold"
                [class]="lifetimeDiff() > 0 ? 'text-red-700' : lifetimeDiff() < 0 ? 'text-green-700' : 'text-gray-600'">
            Cost Impact: {{ d.fieldChanged }}
          </span>
        </div>

        <!-- Change row -->
        <div class="px-4 py-2 text-xs text-gray-500 border-b border-gray-100">
          <span class="line-through">{{ d.previousValue }}</span>
          <mat-icon class="!text-xs !w-3 !h-3 mx-1 align-middle text-gray-400">arrow_forward</mat-icon>
          <span class="font-medium text-gray-800">{{ d.newValue }}</span>
        </div>

        <!-- Cost grid -->
        <div class="grid grid-cols-3 gap-px bg-gray-100 text-center text-xs">
          <div class="bg-white py-2 px-1">
            <div class="text-gray-400 mb-0.5">Lifetime</div>
            <div class="font-semibold" [class]="lifetimeDiff() > 0 ? 'text-red-600' : lifetimeDiff() < 0 ? 'text-green-600' : 'text-gray-700'">
              {{ lifetimeDiff() > 0 ? '+' : '' }}{{ formatCurrency(lifetimeDiff()) }}
            </div>
            <div class="text-[10px] text-gray-400">{{ formatCurrency(d.updatedLifetimeTotal) }}</div>
          </div>
          <div class="bg-white py-2 px-1">
            <div class="text-gray-400 mb-0.5">This Year</div>
            <div class="font-semibold" [class]="currentYearDiff() > 0 ? 'text-red-600' : currentYearDiff() < 0 ? 'text-green-600' : 'text-gray-700'">
              {{ currentYearDiff() > 0 ? '+' : '' }}{{ formatCurrency(currentYearDiff()) }}
            </div>
            <div class="text-[10px] text-gray-400">{{ formatCurrency(d.updatedCurrentYearTotal) }}</div>
          </div>
          <div class="bg-white py-2 px-1">
            <div class="text-gray-400 mb-0.5">Present Value</div>
            <div class="font-semibold" [class]="presentValueDiff() > 0 ? 'text-red-600' : presentValueDiff() < 0 ? 'text-green-600' : 'text-gray-700'">
              {{ presentValueDiff() > 0 ? '+' : '' }}{{ formatCurrency(presentValueDiff()) }}
            </div>
            <div class="text-[10px] text-gray-400">{{ formatCurrency(d.updatedPresentValue) }}</div>
          </div>
        </div>

        <!-- Narrative -->
        @if (d.narrativeSummary) {
          <div class="px-4 py-2.5 text-xs text-gray-600 leading-relaxed border-t border-gray-100">
            {{ d.narrativeSummary }}
          </div>
        }
      </div>
    }
  `,
})
export class DeltaDisplayComponent {
  delta = input.required<DeltaResult>();

  lifetimeDiff(): number {
    const d = this.delta();
    return d.updatedLifetimeTotal - d.previousLifetimeTotal;
  }

  currentYearDiff(): number {
    const d = this.delta();
    return d.updatedCurrentYearTotal - d.previousCurrentYearTotal;
  }

  presentValueDiff(): number {
    const d = this.delta();
    return d.updatedPresentValue - d.previousPresentValue;
  }

  formatCurrency(value: number): string {
    const abs = Math.abs(value);
    if (abs >= 1_000_000) return '$' + (abs / 1_000_000).toFixed(1) + 'M';
    if (abs >= 1_000) return '$' + Math.round(abs).toLocaleString();
    return '$' + abs.toFixed(0);
  }
}
