export interface SignInRequest {
  email: string;
  password: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
  confirmPassword: string;
}

export interface ChangePasswordRequest {
  oldPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface VerifyEmailRequest {
  token: string;
}

export interface ResendVerificationRequest {
  email: string;
}

export interface AuthResponse {
  success: boolean;
  message: string;
  token?: string;
  expiresAt?: string;
  user?: AuthUser;
}

export type UserRole = 'admin' | 'financial_planner_group' | 'financial_planner' | 'user';

export interface ImpersonationResponse {
  token: string;
  expiresAt: string;
  actingAsUserId: string;
  targetUserId: string;
  targetEmail: string;
  targetFirstName: string;
  targetLastName: string;
}

export interface AuthUser {
  id: string;
  email: string;
  phone: string;
  role: UserRole;
  fpgId?: string;
  fpId?: string;
  mustChangePassword: boolean;
}
