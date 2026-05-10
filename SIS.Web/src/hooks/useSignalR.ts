import { useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';

export type SignalRStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'failed';

interface UseSignalROptions {
  /** Hub URL — ör. "/hubs/schedule-progress" (vite proxy üzerinden backend'e yönlendirilir). */
  hubUrl: string;
  /** Connection kurulduğunda event handler'ları register etmek için callback. */
  onConnect?: (connection: HubConnection) => void;
  /** Hook çalışmasın istiyorsan false yap (ör. login olmadan). */
  enabled?: boolean;
}

interface UseSignalRResult {
  connection: HubConnection | null;
  status: SignalRStatus;
}

/**
 * SignalR connection lifecycle yöneten hook.
 *
 * - JWT token'ı localStorage'dan alır ve query param olarak geçirir (backend
 *   JwtBearerEvents.OnMessageReceived bunu Authorization header'a çevirir).
 * - Auto-reconnect aktif (Microsoft client default policy: 0s, 2s, 10s, 30s).
 * - Component unmount olduğunda connection.stop() ile temiz kapanır.
 * - enabled=false ise connection açılmaz.
 */
export function useSignalR(options: UseSignalROptions): UseSignalRResult {
  const { hubUrl, onConnect, enabled = true } = options;
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [status, setStatus] = useState<SignalRStatus>('idle');
  // onConnect referans değişikliği yeniden bağlanmaya neden olmasın diye ref'te tut.
  const onConnectRef = useRef(onConnect);
  useEffect(() => {
    onConnectRef.current = onConnect;
  });

  useEffect(() => {
    if (!enabled) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setStatus('idle');
      return;
    }

    let cancelled = false;
    setStatus('connecting');

    const conn = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => localStorage.getItem('sis_token') ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    conn.onreconnecting(() => setStatus('reconnecting'));
    conn.onreconnected(() => setStatus('connected'));
    conn.onclose(() => {
      if (!cancelled) setStatus('disconnected');
    });

    conn
      .start()
      .then(() => {
        if (cancelled) {
          conn.stop().catch(() => undefined);
          return;
        }
        setStatus('connected');
        setConnection(conn);
        onConnectRef.current?.(conn);
      })
      .catch(() => {
        if (!cancelled) setStatus('failed');
      });

    return () => {
      cancelled = true;
      if (conn.state !== HubConnectionState.Disconnected) {
        conn.stop().catch(() => undefined);
      }
      setConnection(null);
    };
  }, [hubUrl, enabled]);

  return { connection, status };
}
