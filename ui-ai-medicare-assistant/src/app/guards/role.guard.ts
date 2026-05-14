import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models/auth.model';

/**
 * Guard factory: allows the route only if the current user has one of the allowed roles.
 * If not, redirects to the user's natural landing page via `dashboardRedirectGuard` logic.
 */
export const roleGuard = (allowed: UserRole[]): CanActivateFn => () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const role = auth.currentRole();
  if (role && allowed.includes(role)) return true;

  return router.parseUrl('/');
};
