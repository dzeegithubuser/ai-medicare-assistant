import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ErrorNotificationService } from '../services/error-notification.service';

/** URLs whose errors should NOT trigger the global popup (handled locally). */
const SILENT_URL_FRAGMENTS = [
  '/api/auth/',
];

export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const errorNotification = inject(ErrorNotificationService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Skip if the calling code handles errors locally (auth flow, etc.)
      const isSilent = SILENT_URL_FRAGMENTS.some(f => req.url.includes(f));
      if (isSilent) {
        return throwError(() => error);
      }

      const message = friendlyMessage(error);
      const detail = `${req.method} ${req.url}\nStatus: ${error.status} ${error.statusText}`;

      errorNotification.show({ message, detail });

      return throwError(() => error);
    })
  );
};

function friendlyMessage(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'Unable to reach the server. Please check your internet connection and try again.';
  }
  if (error.status === 401) {
    return 'Your session has expired. Please sign in again.';
  }
  if (error.status === 403) {
    return 'You do not have permission to perform this action.';
  }
  if (error.status === 404) {
    return 'The requested resource was not found.';
  }
  if (error.status === 408 || error.status === 504) {
    return 'The request timed out. Please try again.';
  }
  if (error.status === 429) {
    return 'Too many requests. Please wait a moment and try again.';
  }
  if (error.status >= 500) {
    return 'An unexpected server error occurred. Please try again later.';
  }
  return 'Something went wrong while processing your request. Please try again.';
}
