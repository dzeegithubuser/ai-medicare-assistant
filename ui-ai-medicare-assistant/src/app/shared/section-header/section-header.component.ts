import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-section-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="flex items-center gap-3">
      @if (icon()) {
        <div class="flex items-center justify-center w-10 h-10 rounded-xl" [class]="iconBgClass()">
          <mat-icon [class]="iconColorClass()">{{ icon() }}</mat-icon>
        </div>
      }
      <div class="flex-1 min-w-0">
        <h2 [class]="titleClass()">{{ title() }}</h2>
        @if (subtitle()) {
          <p class="text-sm text-gray-500">{{ subtitle() }}</p>
        }
      </div>
      <ng-content />
    </div>
  `,
})
export class SectionHeaderComponent {
  readonly icon = input<string>();
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly iconBgClass = input('bg-cyan-100');
  readonly iconColorClass = input('text-cyan-600');
  readonly titleClass = input('text-xl font-bold text-gray-900');
}
