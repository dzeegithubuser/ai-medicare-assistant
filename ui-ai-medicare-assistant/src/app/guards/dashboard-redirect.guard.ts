import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AppRoutes } from '../app-routes.const';

/**
 * Dashboard root redirect: route to the appropriate landing for the user's role.
 * - admin                      → /admin
 * - financial_planner_group    → /fpg
 * - financial_planner          → /fp
 * - user (and unknown)         → /saved
 */
export const dashboardRedirectGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const role = auth.currentRole();
  switch (role) {
    case 'admin':
      router.navigate([AppRoutes.abs.ADMIN_HOME]);
      return false;
    case 'financial_planner_group':
      router.navigate([AppRoutes.abs.FPG_HOME]);
      return false;
    case 'financial_planner':
      router.navigate([AppRoutes.abs.FP_HOME]);
      return false;
    default:
      router.navigate([AppRoutes.abs.SAVED]);
      return false;
  }
};
