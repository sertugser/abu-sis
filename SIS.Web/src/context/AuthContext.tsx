import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { login as loginApi } from '../api/auth.api';
import {
  clearStoredAuth,
  readStoredAuth,
  saveStoredAuth,
  subscribeToAuthStorageChanges,
} from '../lib/authStorage';
import { decodeJwt, extractUser } from '../lib/jwt';
import { queryClient } from '../lib/queryClient';
import type { User } from '../types/auth';

interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<string | null>;
  logout: () => void;
}

interface LocalAuthState {
  user: User | null;
  token: string | null;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

function readInitialAuthState(): LocalAuthState {
  const auth = readStoredAuth({ clearInvalid: true, notify: false });
  if (auth.status !== 'valid') {
    return { user: null, token: null };
  }

  const payload = decodeJwt(auth.session.token);
  const user = extractUser(payload);
  if (!user.userId) {
    clearStoredAuth({ reason: 'invalid', notify: false });
    return { user: null, token: null };
  }

  return { user, token: auth.session.token };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [authState, setAuthState] = useState<LocalAuthState>(() => readInitialAuthState());
  const [isLoading] = useState(false);

  useEffect(() => subscribeToAuthStorageChanges((reason) => {
    if (reason !== 'login') {
      queryClient.clear();
    }
    setAuthState(readInitialAuthState());
  }), []);

  const login = async (email: string, password: string): Promise<string | null> => {
    queryClient.clear();

    const response = await loginApi({ email, password });
    if (response.success) {
      saveStoredAuth({
        token: response.token,
        expiresAt: response.expiresAt,
        roles: response.roles,
      });
      const payload = decodeJwt(response.token);
      const userData = extractUser(payload);
      setAuthState({ user: userData, token: response.token });
      return null;
    }

    return response.error || 'Giriş başarısız';
  };

  const logout = () => {
    clearStoredAuth({ reason: 'logout' });
    setAuthState({ user: null, token: null });
    queryClient.clear();
  };

  return (
    <AuthContext.Provider
      value={{
        user: authState.user,
        token: authState.token,
        isAuthenticated: !!authState.token,
        isLoading,
        login,
        logout,
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
