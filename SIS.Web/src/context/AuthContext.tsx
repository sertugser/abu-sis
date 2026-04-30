import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { getSessionRuntimeSettings, login as loginApi } from '../api/auth.api';
import { decodeJwt, extractUser } from '../lib/jwt';
import { queryClient } from '../lib/queryClient';
import type { User } from '../types/auth';

interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  requiresPasswordChange: boolean;
  passwordExpiresAt: string | null;
  login: (email: string, password: string) => Promise<string | null>;
  logout: () => void;
  clearPasswordRotationRequirement: () => void;
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
  const [isLoading] = useState(() => {
    const storedToken = localStorage.getItem('sis_token');
    const expiresAt = localStorage.getItem('sis_token_expires');
    if (!storedToken || !expiresAt || new Date(expiresAt) <= new Date()) {
      localStorage.removeItem('sis_token');
      localStorage.removeItem('sis_token_expires');
      localStorage.removeItem('sis_roles');
      localStorage.removeItem('sis_password_rotation_required');
      localStorage.removeItem('sis_password_rotation_expires_at');
    }
    return false;
  });
  const [requiresPasswordChange, setRequiresPasswordChange] = useState(
    () => localStorage.getItem('sis_password_rotation_required') === 'true',
  );
  const [passwordExpiresAt, setPasswordExpiresAt] = useState<string | null>(
    () => localStorage.getItem('sis_password_rotation_expires_at'),
  );

  useEffect(() => {
    const defaultLanguage = localStorage.getItem('sis_default_language');
    if (defaultLanguage) {
      document.documentElement.lang = defaultLanguage;
    }
  }, []);

  useEffect(() => {
    if (!token) {
      return;
    }

    let isDisposed = false;
    getSessionRuntimeSettings()
      .then((session) => {
        if (isDisposed) {
          return;
        }

        localStorage.setItem('sis_default_language', session.defaultLanguage);
        localStorage.setItem('sis_password_rotation_required', session.requiresPasswordChange ? 'true' : 'false');
        if (session.passwordExpiresAt) {
          localStorage.setItem('sis_password_rotation_expires_at', session.passwordExpiresAt);
        } else {
          localStorage.removeItem('sis_password_rotation_expires_at');
        }

        document.documentElement.lang = session.defaultLanguage;
        setRequiresPasswordChange(session.requiresPasswordChange);
        setPasswordExpiresAt(session.passwordExpiresAt ?? null);
      })
      .catch(() => {});

    return () => {
      isDisposed = true;
    };
  }, [token]);

  const login = async (email: string, password: string): Promise<string | null> => {
    // Onceki oturumun cache'ini temizle — farkli kullanicilarin verileri karismasin
    queryClient.clear();

    const response = await loginApi({ email, password });
    if (response.success) {
      localStorage.setItem('sis_token', response.token);
      localStorage.setItem('sis_token_expires', response.expiresAt);
      localStorage.setItem('sis_roles', JSON.stringify(response.roles));
      localStorage.setItem('sis_default_language', response.defaultLanguage);
      localStorage.setItem('sis_password_rotation_required', response.requiresPasswordChange ? 'true' : 'false');
      if (response.passwordExpiresAt) {
        localStorage.setItem('sis_password_rotation_expires_at', response.passwordExpiresAt);
      } else {
        localStorage.removeItem('sis_password_rotation_expires_at');
      }
      const payload = decodeJwt(response.token);
      const userData = extractUser(payload);
      document.documentElement.lang = response.defaultLanguage;
      setUser(userData);
      setToken(response.token);
      setRequiresPasswordChange(response.requiresPasswordChange);
      setPasswordExpiresAt(response.passwordExpiresAt ?? null);
      return null;
    }
    return response.error || 'Giriş başarısız';
  };

  const clearPasswordRotationRequirement = () => {
    localStorage.setItem('sis_password_rotation_required', 'false');
    localStorage.removeItem('sis_password_rotation_expires_at');
    setRequiresPasswordChange(false);
    setPasswordExpiresAt(null);
  };

  const logout = () => {
    localStorage.removeItem('sis_token');
    localStorage.removeItem('sis_token_expires');
    localStorage.removeItem('sis_roles');
    localStorage.removeItem('sis_password_rotation_required');
    localStorage.removeItem('sis_password_rotation_expires_at');
    setUser(null);
    setToken(null);
    setRequiresPasswordChange(false);
    setPasswordExpiresAt(null);
    // Tum cache'i temizle — eski kullanicinin verileri kalmasin
    queryClient.clear();
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!token,
        isLoading,
        requiresPasswordChange,
        passwordExpiresAt,
        login,
        logout,
        clearPasswordRotationRequirement,
      }}
    >
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
