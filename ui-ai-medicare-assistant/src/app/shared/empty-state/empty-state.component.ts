import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="flex flex-col items-center justify-center text-center text-gray-600" [class]="containerClass()">
      <mat-icon [class]="iconClass()">{{ icon() }}</mat-icon>
      <p [class]="titleClass()">{{ title() }}</p>
      @if (subtitle()) {
        <p class="text-sm mt-1 max-w-md">{{ subtitle() }}</p>
      }
      <ng-content />
    </div>
  `,
})
export class EmptyStateComponent {
  readonly icon = input.required<string>();
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly containerClass = input('py-16 gap-3');
  readonly iconClass = input('!text-5xl !w-14 !h-14 mb-2 opacity-60');
  readonly titleClass = input('text-lg font-medium');
}
