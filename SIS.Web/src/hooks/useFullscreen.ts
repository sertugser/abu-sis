import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'sis_scheduling_editor_fullscreen_pref';

interface FullscreenDocument extends Document {
  webkitFullscreenElement?: Element | null;
  msFullscreenElement?: Element | null;
  webkitExitFullscreen?: () => Promise<void>;
  msExitFullscreen?: () => Promise<void>;
}

interface FullscreenElement extends HTMLElement {
  webkitRequestFullscreen?: () => Promise<void>;
  msRequestFullscreen?: () => Promise<void>;
}

function getFullscreenElement(): Element | null {
  const doc = document as FullscreenDocument;
  return doc.fullscreenElement ?? doc.webkitFullscreenElement ?? doc.msFullscreenElement ?? null;
}

async function requestFullscreen(el: HTMLElement): Promise<void> {
  const node = el as FullscreenElement;
  if (node.requestFullscreen) return node.requestFullscreen();
  if (node.webkitRequestFullscreen) return node.webkitRequestFullscreen();
  if (node.msRequestFullscreen) return node.msRequestFullscreen();
}

async function exitFullscreen(): Promise<void> {
  const doc = document as FullscreenDocument;
  if (doc.exitFullscreen) return doc.exitFullscreen();
  if (doc.webkitExitFullscreen) return doc.webkitExitFullscreen();
  if (doc.msExitFullscreen) return doc.msExitFullscreen();
}

/**
 * Tarayıcı fullscreen API'sini saran hook. ESC veya butonla çıkış dinlenir,
 * kullanıcının "her açılışta fullscreen" tercihi localStorage'da tutulur.
 */
export function useFullscreen() {
  const [isFullscreen, setIsFullscreen] = useState<boolean>(() => getFullscreenElement() != null);
  const [autoEnter, setAutoEnterState] = useState<boolean>(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === 'true';
    } catch {
      return false;
    }
  });

  useEffect(() => {
    const handler = () => setIsFullscreen(getFullscreenElement() != null);
    document.addEventListener('fullscreenchange', handler);
    document.addEventListener('webkitfullscreenchange', handler);
    document.addEventListener('msfullscreenchange', handler);
    return () => {
      document.removeEventListener('fullscreenchange', handler);
      document.removeEventListener('webkitfullscreenchange', handler);
      document.removeEventListener('msfullscreenchange', handler);
    };
  }, []);

  const enter = useCallback(async (el?: HTMLElement) => {
    const target = el ?? document.documentElement;
    try {
      await requestFullscreen(target);
    } catch {
      // Kullanıcı reddetti veya tarayıcı gesture istiyor — sessizce yut.
    }
  }, []);

  const exit = useCallback(async () => {
    if (getFullscreenElement() == null) return;
    try {
      await exitFullscreen();
    } catch {
      // ignore
    }
  }, []);

  const toggle = useCallback(async (el?: HTMLElement) => {
    if (getFullscreenElement() != null) {
      await exit();
    } else {
      await enter(el);
    }
  }, [enter, exit]);

  const setAutoEnter = useCallback((value: boolean) => {
    setAutoEnterState(value);
    try {
      localStorage.setItem(STORAGE_KEY, value ? 'true' : 'false');
    } catch {
      // localStorage kotası — sessizce yut, in-memory state yine geçerli kalır.
    }
  }, []);

  return { isFullscreen, enter, exit, toggle, autoEnter, setAutoEnter };
}
