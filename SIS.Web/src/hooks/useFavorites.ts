import { useCallback, useEffect, useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getUserPreferences,
  saveUserPreferences,
  type UserPreferenceDto,
} from '../api/userPreferences.api';
import { useAuth } from '../context/AuthContext';

const STORAGE_KEY = 'sis.favorites.v1';
const MIGRATION_FLAG_KEY = 'sis.favorites.migrated.v1'; // gitleaks:allow
export const MAX_FAVORITES = 11;

const QUERY_KEY = ['user-preferences'] as const;

function loadLegacyFavorites(): string[] | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw === null) return null;
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter(x => typeof x === 'string') : null;
  } catch {
    return null;
  }
}

function writeLocalCache(favorites: string[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(favorites));
  } catch {
    // quota/storage disabled — tolerate
  }
}

function clearLegacyStorage(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // tolerate
  }
}

function markMigrated(): void {
  try {
    localStorage.setItem(MIGRATION_FLAG_KEY, '1');
  } catch {
    // tolerate
  }
}

function hasMigrated(): boolean {
  try {
    return localStorage.getItem(MIGRATION_FLAG_KEY) === '1';
  } catch {
    return false;
  }
}

/**
 * User-pinned favorite routes for the sidebar "Sik Kullanilanlar" group.
 *
 * - Server-backed via TanStack Query (`/api/user/preferences`)
 * - Falls back to localStorage cache if the server is unreachable
 * - One-time migrates legacy localStorage pins to the server on first load
 *
 * Returns `favorites === null` when the user has never customized (sidebar
 * then shows role-default pins).
 */
export function useFavorites() {
  const { user, isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const migrationAttemptedRef = useRef(false);

  const query = useQuery<UserPreferenceDto>({
    queryKey: QUERY_KEY,
    queryFn: ({ signal }) => getUserPreferences(signal),
    enabled: isAuthenticated && !!user,
    staleTime: 5 * 60 * 1000,
    retry: 1,
  });

  const mutation = useMutation({
    mutationFn: saveUserPreferences,
    onMutate: async (newFavorites) => {
      await queryClient.cancelQueries({ queryKey: QUERY_KEY });
      const previous = queryClient.getQueryData<UserPreferenceDto>(QUERY_KEY);
      const optimistic: UserPreferenceDto = { favorites: newFavorites, hasCustomized: true };
      queryClient.setQueryData<UserPreferenceDto>(QUERY_KEY, optimistic);
      writeLocalCache(newFavorites);
      return { previous };
    },
    onError: (_error, _newFavorites, context) => {
      // Rollback cache if server rejected — but keep localStorage copy so the
      // user's selection survives the session (fallback mode).
      if (context?.previous) {
        queryClient.setQueryData<UserPreferenceDto>(QUERY_KEY, context.previous);
      }
    },
  });

  // One-time migration: if server says "not customized" AND we have legacy
  // localStorage pins, PUT them once and clear the legacy store.
  useEffect(() => {
    if (migrationAttemptedRef.current) return;
    if (!isAuthenticated || !user) return;
    if (!query.data) return;                          // wait for initial query
    if (query.data.hasCustomized) return;             // server already has data
    if (hasMigrated()) return;                        // one-shot

    const legacy = loadLegacyFavorites();
    if (!legacy || legacy.length === 0) {
      markMigrated();
      clearLegacyStorage();
      migrationAttemptedRef.current = true;
      return;
    }

    migrationAttemptedRef.current = true;
    mutation.mutate(legacy, {
      onSuccess: () => {
        markMigrated();
        clearLegacyStorage();
      },
      onError: () => {
        // Leave legacy data in place; we'll try again on next successful load.
        migrationAttemptedRef.current = false;
      },
    });
  }, [isAuthenticated, user, query.data, mutation]);

  // Fallback source when query has errored: use localStorage cache so the
  // sidebar doesn't lose the user's selections just because the API is down.
  const fallbackFavorites = query.isError ? loadLegacyFavorites() : null;

  const serverFavorites = query.data?.hasCustomized ? query.data.favorites : null;
  const effectiveFavorites = serverFavorites ?? fallbackFavorites;
  const hasCustomized = effectiveFavorites !== null;

  const save = useCallback((routes: string[]) => {
    const trimmed = routes.slice(0, MAX_FAVORITES);
    mutation.mutate(trimmed);
  }, [mutation]);

  const isFavorite = useCallback(
    (route: string) => (effectiveFavorites ?? []).includes(route),
    [effectiveFavorites],
  );

  const toggle = useCallback((route: string): boolean => {
    const current = effectiveFavorites ?? [];
    if (current.includes(route)) {
      save(current.filter(r => r !== route));
      return true;
    }
    if (current.length >= MAX_FAVORITES) return false;
    save([...current, route]);
    return true;
  }, [effectiveFavorites, save]);

  return {
    favorites: effectiveFavorites,
    hasCustomized,
    isFavorite,
    toggle,
    save,
    MAX: MAX_FAVORITES,
  };
}
