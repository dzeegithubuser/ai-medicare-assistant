import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-auth-form-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-cyan-50 to-cyan-100">
      <div class="w-full max-w-md mx-4">
        <div class="bg-white rounded-2xl shadow-xl p-8">
          <div class="flex flex-col items-center mb-8">
            <div class="w-14 h-14 rounded-xl flex items-center justify-center mb-3 shadow-lg" [class]="iconBgClass()">
              <mat-icon class="!text-white !text-3xl !w-8 !h-8">{{ icon() }}</mat-icon>
            </div>
            <h1 class="text-2xl font-bold text-gray-800">{{ title() }}</h1>
            <p class="text-sm text-gray-500 mt-1">{{ subtitle() }}</p>
          </div>
          <ng-content />
        </div>
      </div>
    </div>
  `,
})
export class AuthFormShellComponent {
  readonly icon = input.required<string>();
  readonly iconBgClass = input('bg-cyan-600');
  readonly title = input.required<string>();
  readonly subtitle = input.required<string>();
}
