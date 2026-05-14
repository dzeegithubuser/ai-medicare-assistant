import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../services/auth.service';
import {
  ImpersonationContinueDialogComponent,
  ImpersonationContinueResult,
} from '../impersonation-continue-dialog/impersonation-continue-dialog.component';

/** Show the "Continue?" prompt this many ms before the impersonation token expires. */
const WARN_BEFORE_EXPIRY_MS = 5 * 60 * 1000;

@Component({
  selector: 'app-impersonation-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule],
  template: `
    @if (active()) {
      <div class="flex items-center gap-3 px-6 py-2 bg-amber-100 border-b border-amber-300 text-amber-900 text-sm">
        <mat-icon class="!text-amber-700">supervisor_account</mat-icon>
        <span>
          You are acting as <strong>{{ targetEmail() }}</strong>. Changes are saved to this user's account.
        </span>
        <span class="flex-1"></span>
        <button mat-stroked-button color="warn" type="button" (click)="exit()">
          <mat-icon class="!text-base mr-1">logout</mat-icon>
          Exit impersonation
        </button>
      </div>
    }
  `,
})
export class ImpersonationBannerComponent {
  private auth = inject(AuthService);
  private dialog = inject(MatDialog);

  protected active = computed(() => this.auth.isImpersonating());
  protected targetEmail = computed(() => this.auth.currentUser()?.email ?? '');

  constructor() {
    // Drive the warn / auto-exit timers from the impersonation expiry signal.
    // The effect re-runs (and cleans up old timers) whenever the expiry changes —
    // including when the user clicks "Continue" and a fresh expiry is published.
    effect(onCleanup => {
      const expiresAt = this.auth.impersonationExpiresAt();
      if (!expiresAt) return;

      const remaining = expiresAt.getTime() - Date.now();
      const warnIn = remaining - WARN_BEFORE_EXPIRY_MS;

      let warnHandle: ReturnType<typeof setTimeout> | undefined;
      let expireHandle: ReturnType<typeof setTimeout> | undefined;

      if (warnIn <= 0 && remaining > 0) {
        // We are already inside the warn window — show the dialog now.
        this.openWarning(expiresAt);
      } else if (warnIn > 0) {
        warnHandle = setTimeout(() => this.openWarning(expiresAt), warnIn);
      }

      if (remaining <= 0) {
        this.handleExpiry();
      } else {
        expireHandle = setTimeout(() => this.handleExpiry(), remaining);
      }

      onCleanup(() => {
        if (warnHandle) clearTimeout(warnHandle);
        if (expireHandle) clearTimeout(expireHandle);
      });
    });
  }

  protected exit() {
    this.auth.exitImpersonation();
    window.location.assign('/');
  }

  private openWarning(expiresAt: Date) {
    const ref = this.dialog.open<
      ImpersonationContinueDialogComponent,
      { targetEmail: string; expiresAt: Date },
      ImpersonationContinueResult
    >(ImpersonationContinueDialogComponent, {
      data: { targetEmail: this.targetEmail(), expiresAt },
      disableClose: false,
      width: '420px',
    });

    ref.afterClosed().subscribe(result => {
      if (result === 'continue') {
        this.auth.refreshImpersonation().subscribe({
          // Effect picks up the new expiry signal and reschedules timers.
          error: () => this.exit(),
        });
      } else if (result === 'exit') {
        this.exit();
      }
      // If undefined (e.g. expiry timer auto-closed), handleExpiry has already exited.
    });
  }

  private handleExpiry() {
    if (!this.auth.isImpersonating()) return;
    this.dialog.closeAll();
    this.auth.exitImpersonation();
    window.location.assign('/');
  }
}
