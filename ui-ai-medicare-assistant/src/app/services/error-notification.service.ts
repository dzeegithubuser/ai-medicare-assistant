import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ErrorDialogComponent, ErrorDialogData } from '../shared/error-dialog/error-dialog.component';

@Injectable({ providedIn: 'root' })
export class ErrorNotificationService {
  private dialog = inject(MatDialog);
  private isOpen = false;

  /** Show a popup dialog for an API error. Only one dialog at a time. */
  show(data: ErrorDialogData): void {
    if (this.isOpen) return;

    this.isOpen = true;
    const ref = this.dialog.open(ErrorDialogComponent, {
      data,
      width: '440px',
      disableClose: false,
      panelClass: 'error-dialog-panel',
    });

    ref.afterClosed().subscribe(() => (this.isOpen = false));
  }
}
