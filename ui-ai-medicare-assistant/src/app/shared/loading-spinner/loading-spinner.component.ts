import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-loading-spinner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="flex items-center justify-center" [class]="containerClass()">
      <mat-spinner [diameter]="diameter()"></mat-spinner>
      @if (message()) {
        <span class="ml-3 text-sm text-gray-500">{{ message() }}</span>
      }
    </div>
  `,
})
export class LoadingSpinnerComponent {
  readonly diameter = input(32);
  readonly message = input<string>();
  readonly containerClass = input('py-8');
}
