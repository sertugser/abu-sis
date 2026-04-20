import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'sis.favorites.v1';
export const MAX_FAVORITES = 11;

function loadFromStorage(): string[] | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw === null) return null;
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter(x => typeof x === 'string') : null;
  } catch {
    return null;
  }
}

function saveToStorage(routes: string[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(routes));
  } catch {
    // quota exceeded or disabled storage — silently ignore
  }
}

/**
 * User-pinned favorite routes for sidebar Sık Kullanılanlar group.
 * Returns `null` when user has never customized (falls back to role defaults).
 */
export function useFavorites() {
  const [favorites, setFavoritesState] = useState<string[] | null>(loadFromStorage);

  useEffect(() => {
    function onStorage(e: StorageEvent) {
      if (e.key === STORAGE_KEY) setFavoritesState(loadFromStorage());
    }
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  const save = useCallback((routes: string[]) => {
    const trimmed = routes.slice(0, MAX_FAVORITES);
    saveToStorage(trimmed);
    setFavoritesState(trimmed);
  }, []);

  const isFavorite = useCallback(
    (route: string) => (favorites ?? []).includes(route),
    [favorites],
  );

  const toggle = useCallback((route: string): boolean => {
    const current = favorites ?? [];
    if (current.includes(route)) {
      save(current.filter(r => r !== route));
      return true;
    }
    if (current.length >= MAX_FAVORITES) return false;
    save([...current, route]);
    return true;
  }, [favorites, save]);

  return {
    favorites,              // null = uncustomized, [] = user cleared all
    hasCustomized: favorites !== null,
    isFavorite,
    toggle,
    save,
    MAX: MAX_FAVORITES,
  };
}
