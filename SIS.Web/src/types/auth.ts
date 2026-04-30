export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  success: boolean;
  token: string;
  expiresAt: string;
  roles: string[];
  requiresPasswordChange: boolean;
  passwordExpiresAt?: string | null;
  defaultLanguage: string;
  error?: string;
}

export interface User {
  userId: number;
  name: string;
  email: string;
  roles: string[];
}

export interface PublicRuntimeSettings {
  defaultLanguage: string;
}

export interface SessionRuntimeSettings {
  defaultLanguage: string;
  requiresPasswordChange: boolean;
  passwordExpiresAt?: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}
