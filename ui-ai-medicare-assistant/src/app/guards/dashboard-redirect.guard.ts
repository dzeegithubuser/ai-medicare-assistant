import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

/**
 * Dashboard default route: after login, `/` redirects to saved recommendations.
 */
export const dashboardRedirectGuard: CanActivateFn = () => {
  const router = inject(Router);
  router.navigate(['/saved']);
  return false;
};
