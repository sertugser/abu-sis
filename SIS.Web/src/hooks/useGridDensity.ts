import { useCallback, useEffect, useState } from 'react';

export type GridDensity = 'compact' | 'normal' | 'wide';

const STORAGE_KEY = 'sis_calendar_grid_density';
const DEFAULT_DENSITY: GridDensity = 'normal';

function readStoredDensity(): GridDensity {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    if (value === 'compact' || value === 'normal' || value === 'wide') {
      return value;
    }
  } catch {
    // localStorage erişimi engellenmiş — sessizce default'a düş
  }
  return DEFAULT_DENSITY;
}

function writeStoredDensity(value: GridDensity): void {
  try {
    localStorage.setItem(STORAGE_KEY, value);
  } catch {
    // localStorage kotası / private mod — sessizce yut
  }
}

/**
 * Salt-okunur takvim görünümünün kart yoğunluğu tercihi.
 * 3 mod: 'compact' (22px tek satır), 'normal' (24px tek satır + derslik/hoca),
 * 'wide' (36px iki satır). Tercih localStorage'da kalır.
 *
 * Düzenleyici sayfasında kullanılmaz — orada CalendarGrid `cardDensity` prop'u
 * geçmediği için 'spacious' default'una düşer ve mevcut davranış korunur.
 */
export function useGridDensity(): {
  density: GridDensity;
  setDensity: (value: GridDensity) => void;
} {
  const [density, setDensityState] = useState<GridDensity>(readStoredDensity);

  // Aynı tarayıcıda iki sekme açıkken senkron kalsın (storage event)
  useEffect(() => {
    const handler = (e: StorageEvent) => {
      if (e.key !== STORAGE_KEY) return;
      setDensityState(readStoredDensity());
    };
    window.addEventListener('storage', handler);
    return () => window.removeEventListener('storage', handler);
  }, []);

  const setDensity = useCallback((value: GridDensity) => {
    setDensityState(value);
    writeStoredDensity(value);
  }, []);

  return { density, setDensity };
}
