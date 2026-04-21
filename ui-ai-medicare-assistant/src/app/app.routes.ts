import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { dashboardRedirectGuard } from './guards/dashboard-redirect.guard';
import { AppRoutes } from './app-routes.const';

export const routes: Routes = [
  { path: AppRoutes.SIGNIN, loadComponent: () => import('./auth/signin/signin.component').then(m => m.SigninComponent) },
  { path: AppRoutes.SIGNUP, loadComponent: () => import('./auth/signup/signup.component').then(m => m.SignupComponent) },
  { path: AppRoutes.FORGOT_PASSWORD, loadComponent: () => import('./auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent) },
  { path: AppRoutes.RESET_PASSWORD, loadComponent: () => import('./auth/reset-password/reset-password.component').then(m => m.ResetPasswordComponent) },
  { path: AppRoutes.VERIFY_EMAIL, loadComponent: () => import('./auth/verify-email/verify-email.component').then(m => m.VerifyEmailComponent) },
  {
    path: '',
    loadComponent: () => import('./dashboard/dashboard.component').then(m => m.DashboardComponent),
    canActivate: [authGuard],
    children: [
      { path: '', canActivate: [dashboardRedirectGuard], children: [] },
      {
        path: AppRoutes.SAVED_COMPARE,
        loadComponent: () =>
          import('./recommendation/compare/recommendation-compare.component').then(m => m.RecommendationCompareComponent),
      },
      {
        path: AppRoutes.SAVED_DETAIL,
        loadComponent: () =>
          import('./recommendation/detail/recommendation-detail.component').then(m => m.RecommendationDetailComponent),
      },
      { path: AppRoutes.SAVED, loadComponent: () => import('./recommendation/recommendation.component').then(m => m.RecommendationComponent) },
      { path: AppRoutes.CHANGE_PASSWORD, loadComponent: () => import('./auth/change-password/change-password.component').then(m => m.ChangePasswordComponent) },
      {
        path: AppRoutes.MEDICARE_ANALYSIS,
        loadComponent: () => import('./medicare-analysis/analysis-shell.component').then(m => m.AnalysisShellComponent),
        children: [
          { path: '', redirectTo: AppRoutes.PROFILE, pathMatch: 'full' },
          { path: AppRoutes.PROFILE, loadComponent: () => import('./user-profile/user-profile.component').then(m => m.UserProfileComponent) },
          { path: AppRoutes.DRUGS, loadComponent: () => import('./medicare-analysis/drug-step/drug-step.component').then(m => m.DrugsStepComponent) },
          { path: AppRoutes.PHARMACIES, loadComponent: () => import('./medicare-analysis/pharmacy-step/pharmacy-step.component').then(m => m.PharmacyStepComponent) },
          { path: AppRoutes.PLANS, loadComponent: () => import('./medicare-analysis/plans-step/plans-step.component').then(m => m.PlansStepComponent) },
          { path: AppRoutes.COST_PROJECTIONS, loadComponent: () => import('./cost-projections/cost-projections.component').then(m => m.CostProjectionsComponent) },
        ]
      },
      {
        path: AppRoutes.LTC,
        loadComponent: () => import('./long-term-care/ltc-shell.component').then(m => m.LtcShellComponent),
        children: [
          { path: '', redirectTo: AppRoutes.PROFILE, pathMatch: 'full' },
          { path: AppRoutes.PROFILE, loadComponent: () => import('./user-profile/user-profile.component').then(m => m.UserProfileComponent) },
          { path: AppRoutes.LTC_CARE_TYPE, loadComponent: () => import('./long-term-care/care-type-step/ltc-care-type-step.component').then(m => m.LtcCareTypeStepComponent) },
          { path: AppRoutes.LTC_PROJECTION, loadComponent: () => import('./long-term-care/projection-step/ltc-projection-step.component').then(m => m.LtcProjectionStepComponent) },
        ]
      },
    ]
  },
  { path: '**', redirectTo: '' }
];
