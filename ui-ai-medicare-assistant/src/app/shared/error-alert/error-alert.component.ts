import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-error-alert',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="mb-4 px-4 py-3 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm flex items-center gap-2">
      <mat-icon class="!text-red-500 !text-lg !w-5 !h-5">error</mat-icon>
      {{ message() }}
    </div>
  `,
})
export class ErrorAlertComponent {
  readonly message = input.required<string>();
}
