import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AppRoutes } from '../app-routes.const';

/**
 * Forces a password change before any other action when the current user was
 * created with a default password. Allows the change-password route through.
 */
export const mustChangePasswordGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const user = auth.currentUser();
  if (!user?.mustChangePassword) return true;

  if (state.url.endsWith(`/${AppRoutes.CHANGE_PASSWORD}`)) return true;

  return router.parseUrl(AppRoutes.abs.CHANGE_PASSWORD);
};
