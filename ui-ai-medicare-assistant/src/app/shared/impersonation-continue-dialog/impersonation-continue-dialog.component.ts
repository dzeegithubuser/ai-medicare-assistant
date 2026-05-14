import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface ImpersonationContinueDialogData {
  targetEmail: string;
  expiresAt: Date;
}

export type ImpersonationContinueResult = 'continue' | 'exit';

@Component({
  selector: 'app-impersonation-continue-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="!flex !items-center !gap-2">
      <mat-icon class="!text-amber-700">schedule</mat-icon>
      Continue impersonating?
    </h2>
    <mat-dialog-content>
      <p class="text-sm text-gray-700">
        You are acting as <strong>{{ data.targetEmail }}</strong>.
      </p>
      <p class="text-sm text-gray-700 mt-2">
        Your impersonation session expires in
        <strong class="font-mono text-base">{{ formatted() }}</strong>.
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="dialogRef.close('exit')">Exit impersonation</button>
      <button mat-raised-button color="primary" type="button" (click)="dialogRef.close('continue')">
        Continue
      </button>
    </mat-dialog-actions>
  `,
})
export class ImpersonationContinueDialogComponent {
  protected data = inject<ImpersonationContinueDialogData>(MAT_DIALOG_DATA);
  protected dialogRef = inject(MatDialogRef<ImpersonationContinueDialogComponent, ImpersonationContinueResult>);

  private now = signal(Date.now());

  protected formatted = computed(() => {
    const remainingMs = this.data.expiresAt.getTime() - this.now();
    const totalSec = Math.max(0, Math.floor(remainingMs / 1000));
    const m = Math.floor(totalSec / 60);
    const s = totalSec % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  });

  constructor() {
    const handle = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(handle));
  }
}
