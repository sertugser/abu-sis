import { useEffect, useState } from 'react';
import {
  NOTICE_AUTO_DISMISS_MS,
  type ConfirmOptions,
  type NoticeTone,
  type PendingConfirm,
} from '@/lib/calendarGenerator/types';

/**
 * Takvim Oluşturucu bildirim + onay altyapısı. Bağımsız (başka takvim state'ine
 * bağımlı değil); diğer takvim hook'ları bunu parametre olarak alır.
 */
export function useCalendarNotices() {
  const [notice, setNoticeMessage] = useState<string | null>(null);
  const [noticeTone, setNoticeTone] = useState<NoticeTone>('info');
  const [errorNotice, setErrorNotice] = useState<string | null>(null);
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirm | null>(null);

  useEffect(() => {
    if (!notice)
      return;

    const timer = window.setTimeout(() => {
      setNoticeMessage(null);
    }, NOTICE_AUTO_DISMISS_MS);

    return () => window.clearTimeout(timer);
  }, [notice]);

  function askConfirm(options: ConfirmOptions) {
    return new Promise<boolean>(resolve => {
      setPendingConfirm({ ...options, resolve });
    });
  }

  function closeConfirm(value: boolean) {
    const resolver = pendingConfirm?.resolve;
    setPendingConfirm(null);
    resolver?.(value);
  }

  function setNotice(message: string | null) {
    setNoticeTone('info');
    setNoticeMessage(message);
  }

  function showNotice(message: string, tone: NoticeTone = 'info') {
    setNoticeTone(tone);
    setNoticeMessage(message);
    if (tone !== 'error')
      setErrorNotice(null);
  }

  function showSuccessNotice(message: string) {
    showNotice(message, 'success');
  }

  function showErrorNotice(message: string, showDialog = true) {
    showNotice(message, 'error');
    if (showDialog)
      setErrorNotice(message);
  }

  return {
    notice,
    noticeTone,
    errorNotice,
    setErrorNotice,
    pendingConfirm,
    askConfirm,
    closeConfirm,
    setNotice,
    showSuccessNotice,
    showErrorNotice,
  };
}

export type CalendarNotices = ReturnType<typeof useCalendarNotices>;
