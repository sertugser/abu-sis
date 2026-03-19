export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  success: boolean;
  token: string;
  expiresAt: string;
  roles: string[];
  error?: string;
}

export interface User {
  userId: number;
  name: string;
  email: string;
  roles: string[];
}
