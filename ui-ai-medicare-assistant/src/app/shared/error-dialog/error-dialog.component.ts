import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface ErrorDialogData {
  title?: string;
  message: string;
  detail?: string;
}

@Component({
  selector: 'app-error-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <div class="p-6 max-w-md">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-4">
        <div class="w-11 h-11 rounded-xl bg-red-100 flex items-center justify-center shrink-0">
          <mat-icon class="!text-red-600 !text-2xl">error_outline</mat-icon>
        </div>
        <h2 class="text-lg font-bold text-gray-900">{{ data.title ?? 'Something Went Wrong' }}</h2>
      </div>

      <!-- Message -->
      <p class="text-sm text-gray-700 leading-relaxed mb-2">{{ data.message }}</p>

      <!-- Technical detail (collapsed by default) -->
      @if (data.detail) {
        <details class="mb-4">
          <summary class="text-xs text-gray-400 cursor-pointer hover:text-gray-600 select-none">
            Technical details
          </summary>
          <pre class="mt-2 p-3 rounded-lg bg-gray-50 border border-gray-200 text-xs text-gray-500 whitespace-pre-wrap break-words max-h-32 overflow-y-auto">{{ data.detail }}</pre>
        </details>
      }

      <!-- Action -->
      <div class="flex justify-end mt-5">
        <button mat-flat-button
                class="!rounded-xl !bg-cyan-600 !text-white"
                (click)="dialogRef.close()">
          OK
        </button>
      </div>
    </div>
  `,
})
export class ErrorDialogComponent {
  dialogRef = inject(MatDialogRef<ErrorDialogComponent>);
  data: ErrorDialogData = inject(MAT_DIALOG_DATA);
}
