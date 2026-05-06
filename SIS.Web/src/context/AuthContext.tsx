import { createContext, useContext, useState, type ReactNode } from 'react';
import { changePassword as changePasswordApi, login as loginApi } from '../api/auth.api';
import { decodeJwt, extractUser } from '../lib/jwt';
import { queryClient } from '../lib/queryClient';
import type { User } from '../types/auth';

interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  passwordChangeRequired: boolean;
  login: (email: string, password: string) => Promise<string | null>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<string | null>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => {
    const storedToken = localStorage.getItem('sis_token');
    const expiresAt = localStorage.getItem('sis_token_expires');
    if (storedToken && expiresAt && new Date(expiresAt) > new Date()) {
      const payload = decodeJwt(storedToken);
      return extractUser(payload);
    }
    return null;
  });
  const [token, setToken] = useState<string | null>(() => {
    const storedToken = localStorage.getItem('sis_token');
    const expiresAt = localStorage.getItem('sis_token_expires');
    if (storedToken && expiresAt && new Date(expiresAt) > new Date()) {
      return storedToken;
    }
    return null;
  });
  const [passwordChangeRequired, setPasswordChangeRequired] = useState<boolean>(() => {
    const stored = localStorage.getItem('sis_password_change_required');
    return stored === 'true';
  });
  const [isLoading] = useState(() => {
    const storedToken = localStorage.getItem('sis_token');
    const expiresAt = localStorage.getItem('sis_token_expires');
    if (!storedToken || !expiresAt || new Date(expiresAt) <= new Date()) {
      localStorage.removeItem('sis_token');
      localStorage.removeItem('sis_token_expires');
      localStorage.removeItem('sis_roles');
      localStorage.removeItem('sis_password_change_required');
    }
    return false;
  });

  const login = async (email: string, password: string): Promise<string | null> => {
    // Onceki oturumun cache'ini temizle — farkli kullanicilarin verileri karismasin
    queryClient.clear();

    const response = await loginApi({ email, password });
    if (response.success) {
      localStorage.setItem('sis_token', response.token);
      localStorage.setItem('sis_token_expires', response.expiresAt);
      localStorage.setItem('sis_roles', JSON.stringify(response.roles));
      localStorage.setItem('sis_password_change_required', response.passwordChangeRequired ? 'true' : 'false');
      const payload = decodeJwt(response.token);
      const userData = extractUser(payload);
      setUser(userData);
      setToken(response.token);
      setPasswordChangeRequired(!!response.passwordChangeRequired);
      return null;
    }
    return response.error || 'Giriş başarısız';
  };

  const changePassword = async (currentPassword: string, newPassword: string): Promise<string | null> => {
    const response = await changePasswordApi({ currentPassword, newPassword });
    if (!response.success) {
      return response.error || 'Şifre değiştirilemedi';
    }

    queryClient.clear();
    localStorage.setItem('sis_token', response.token);
    localStorage.setItem('sis_token_expires', response.expiresAt);
    localStorage.setItem('sis_roles', JSON.stringify(response.roles));
    localStorage.setItem('sis_password_change_required', 'false');
    const payload = decodeJwt(response.token);
    const userData = extractUser(payload);
    setUser(userData);
    setToken(response.token);
    setPasswordChangeRequired(false);
    return null;
  };

  const logout = () => {
    localStorage.removeItem('sis_token');
    localStorage.removeItem('sis_token_expires');
    localStorage.removeItem('sis_roles');
    localStorage.removeItem('sis_password_change_required');
    setUser(null);
    setToken(null);
    setPasswordChangeRequired(false);
    // Tum cache'i temizle — eski kullanicinin verileri kalmasin
    queryClient.clear();
  };

  return (
    <AuthContext.Provider value={{ user, token, isAuthenticated: !!token, isLoading, passwordChangeRequired, login, changePassword, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within AuthProvider');
  return context;
}
