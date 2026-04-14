import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ProfileService } from '../services/profile.service';
import { catchError, map, of } from 'rxjs';

/**
 * Redirects to /profile when the user's profile is incomplete.
 * Protects the /analysis route so users must complete onboarding first.
 */
export const profileCompleteGuard: CanActivateFn = () => {
  const profile = inject(ProfileService);
  const router = inject(Router);

  // If profile is already loaded in memory, decide immediately.
  if (profile.profile() !== null) {
    return profile.isProfileComplete() ? true : router.parseUrl('/profile');
  }

  // On hard refresh/deep-link, load profile first to avoid false redirects.
  return profile.loadProfile().pipe(
    map(() => (profile.isProfileComplete() ? true : router.parseUrl('/profile'))),
    catchError(() => of(router.parseUrl('/profile')))
  );
};
