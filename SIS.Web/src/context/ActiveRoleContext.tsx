import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from './AuthContext';
import { effectiveRolesApi } from '../api/effectiveRoles.api';
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
  /** Vekâletten gelen rollerin set'i — UI'da "(vekâlet)" suffix'i için. */
  delegatedRoles: AppRole[];
  isMultiRole: boolean;
  isDelegated: (role: AppRole) => boolean;
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
  const { user, isAuthenticated } = useAuth();
  const ownRolesFromJwt = user?.roles ?? [];

  // Backend'den effective-roles çek: own ∪ aktif vekâletten devralınan
  const { data: effective } = useQuery({
    queryKey: ['me', 'effective-roles', user?.userId],
    queryFn: () => effectiveRolesApi.get(),
    enabled: isAuthenticated,
    staleTime: 60_000, // 1 dk — vekâlet başla/bit anlarında biraz gecikme tolere edilir
  });

  // delegated rolleri ayrı tut (UI suffix için), availableRoles'u union olarak inşa et
  const delegatedRoles = useMemo<AppRole[]>(
    () => sortRolesByPriority(effective?.delegatedRoles ?? []),
    [effective?.delegatedRoles?.join('|')],
  );

  const availableRoles = useMemo<AppRole[]>(() => {
    const all = new Set<string>(effective?.ownRoles ?? ownRolesFromJwt);
    for (const r of effective?.delegatedRoles ?? []) all.add(r);
    return sortRolesByPriority(Array.from(all));
  }, [
    effective?.ownRoles?.join('|'),
    effective?.delegatedRoles?.join('|'),
    ownRolesFromJwt.join('|'),
  ]);

  const [activeRole, setActiveRoleState] = useState<AppRole | null>(null);
  const [invalidationNotice, setInvalidationNotice] = useState<InvalidationNotice | null>(null);

  // Resolve activeRole on mount and whenever the available roles change.
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

  const isDelegated = useCallback(
    (role: AppRole) => delegatedRoles.includes(role),
    [delegatedRoles],
  );

  const value = useMemo<ActiveRoleState>(
    () => ({
      activeRole,
      availableRoles,
      delegatedRoles,
      isMultiRole: availableRoles.length > 1,
      isDelegated,
      setActiveRole,
      invalidationNotice,
      dismissNotice,
    }),
    [activeRole, availableRoles, delegatedRoles, isDelegated, setActiveRole, invalidationNotice, dismissNotice],
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
