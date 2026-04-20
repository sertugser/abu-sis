import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useAuth } from './AuthContext';
import {
  getPrimaryRole,
  getRoleDisplayLabel,
  sortRolesByPriority,
  type AppRole,
} from '../lib/roles';

const STORAGE_KEY = 'sis.activeRole.v1';

interface InvalidationNotice {
  previousRole: string;
  newRole: AppRole;
}

interface ActiveRoleState {
  activeRole: AppRole | null;
  availableRoles: AppRole[];
  isMultiRole: boolean;
  setActiveRole: (role: AppRole) => void;
  invalidationNotice: InvalidationNotice | null;
  dismissNotice: () => void;
}

const ActiveRoleContext = createContext<ActiveRoleState | undefined>(undefined);

function loadStoredRole(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function saveStoredRole(role: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, role);
  } catch {
    // storage unavailable — tolerate
  }
}

function clearStoredRole(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // tolerate
  }
}

export function ActiveRoleProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const userRoles = user?.roles ?? [];

  // Sort user roles by priority once per roles-list change for stable dropdown order.
  const availableRoles = useMemo<AppRole[]>(
    () => sortRolesByPriority(userRoles),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [userRoles.join('|')],
  );

  const [activeRole, setActiveRoleState] = useState<AppRole | null>(null);
  const [invalidationNotice, setInvalidationNotice] = useState<InvalidationNotice | null>(null);

  // Resolve activeRole on mount and whenever the user's role list changes.
  useEffect(() => {
    if (availableRoles.length === 0) {
      setActiveRoleState(null);
      return;
    }
    const stored = loadStoredRole();
    if (stored && availableRoles.includes(stored as AppRole)) {
      setActiveRoleState(stored as AppRole);
      return;
    }
    // Stored role absent or invalid — fall back to highest-priority role.
    const fallback = getPrimaryRole(availableRoles);
    setActiveRoleState(fallback);
    saveStoredRole(fallback);

    // Notify user only when they previously had a valid (now-stripped) role.
    if (stored && stored !== fallback) {
      setInvalidationNotice({ previousRole: stored, newRole: fallback });
    }
  }, [availableRoles]);

  const setActiveRole = useCallback((role: AppRole) => {
    setActiveRoleState(role);
    saveStoredRole(role);
    setInvalidationNotice(null);
  }, []);

  const dismissNotice = useCallback(() => setInvalidationNotice(null), []);

  // Clear stored role on logout so a new user starts fresh.
  useEffect(() => {
    if (!user) clearStoredRole();
  }, [user]);

  const value = useMemo<ActiveRoleState>(
    () => ({
      activeRole,
      availableRoles,
      isMultiRole: availableRoles.length > 1,
      setActiveRole,
      invalidationNotice,
      dismissNotice,
    }),
    [activeRole, availableRoles, setActiveRole, invalidationNotice, dismissNotice],
  );

  return <ActiveRoleContext.Provider value={value}>{children}</ActiveRoleContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useActiveRole(): ActiveRoleState {
  const ctx = useContext(ActiveRoleContext);
  if (!ctx) throw new Error('useActiveRole must be used within ActiveRoleProvider');
  return ctx;
}

// eslint-disable-next-line react-refresh/only-export-components
export function getInvalidationMessage(notice: InvalidationNotice): string {
  return `Aktif rolünüz ("${getRoleDisplayLabel(notice.previousRole)}") kaldırıldı. ${getRoleDisplayLabel(notice.newRole)} rolüne geçirildiniz.`;
}
