export interface SignUpRequest {
  email: string;
  phone: string;
  password: string;
  confirmPassword: string;
}

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

export interface AuthUser {
  id: string;
  email: string;
  phone: string;
}
