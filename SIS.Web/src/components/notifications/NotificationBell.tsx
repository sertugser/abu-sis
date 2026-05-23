import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, CheckCheck, Megaphone, Pin } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { formatDate } from '../../lib/utils';
import {
  useActiveAnnouncements,
  useMarkAllAnnouncementsAsRead,
  useUnreadAnnouncementCount,
} from '../../hooks/useAnnouncements';
import {
  useDeleteNotification,
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotificationRealtime,
  useNotificationUnreadCount,
  useRecentNotifications,
  type NotificationDto,
} from '../../hooks/useNotifications';
import { ROUTES } from '../../lib/constants';

const POLL_INTERVAL_MS = 30_000;
const PREVIEW_COUNT = 10;

// Bildirim önem tonu → sol nokta rengi.
const SEVERITY_DOT: Record<string, string> = {
  Success: 'bg-emerald-500',
  Warning: 'bg-amber-500',
  Critical: 'bg-red-500',
  Info: 'bg-blue-500',
};

interface FeedItem {
  key: string;
  source: 'announcement' | 'notification';
  rawId: number;
  title: string;
  preview: string;
  meta: string;
  date: string;
  unread: boolean;
  isPinned: boolean;
  severity?: string;
  onClick: () => void;
}

/**
 * TopBar'daki 🔔 zil. Artık iki kaynağı toplar: yayın **duyuruları** + kişisel
 * **bildirimler** (onay/kayıt vb.). Tek, zaman sıralı akış; rozet = ikisinin
 * okunmamış toplamı. Bildirimler SignalR ile gerçek-zamanlı güncellenir.
 */
export default function NotificationBell() {
  const navigate = useNavigate();

  // ── Duyurular ──
  const { data: annUnread = 0, refetch: refetchAnnUnread } = useUnreadAnnouncementCount(true, POLL_INTERVAL_MS);
  const { data: announcements = [], isLoading: annLoading, refetch: refetchAnnouncements } = useActiveAnnouncements(PREVIEW_COUNT);
  const markAllAnnouncementsRead = useMarkAllAnnouncementsAsRead();

  // ── Kişisel bildirimler ──
  const { data: notifUnread = 0, refetch: refetchNotifUnread } = useNotificationUnreadCount(true, POLL_INTERVAL_MS);
  const { data: notifications = [], isLoading: notifLoading, refetch: refetchNotifications } = useRecentNotifications(PREVIEW_COUNT);
  const markAllNotificationsRead = useMarkAllNotificationsRead();
  const markNotificationRead = useMarkNotificationRead();
  const deleteNotification = useDeleteNotification();
  useNotificationRealtime(); // gerçek-zamanlı push aboneliği

  const totalUnread = annUnread + notifUnread;
  const isLoading = annLoading || notifLoading;

  function openNotification(n: NotificationDto) {
    if (!n.isRead)
      markNotificationRead.mutate(n.id);
    if (n.link)
      navigate(n.link);
  }

  const feed = useMemo<FeedItem[]>(() => {
    const annItems: FeedItem[] = announcements.map((a) => ({
      key: `a-${a.id}`,
      source: 'announcement',
      rawId: a.id,
      title: a.title,
      preview: a.contentPreview,
      meta: `${formatDate(a.startsAt)} · ${a.createdByName}`,
      date: a.startsAt,
      unread: !a.isReadByMe,
      isPinned: a.isPinned,
      onClick: () => navigate(`${ROUTES.ANNOUNCEMENTS}?openId=${a.id}`),
    }));

    const notifItems: FeedItem[] = notifications.map((n) => ({
      key: `n-${n.id}`,
      source: 'notification',
      rawId: n.id,
      title: n.title,
      preview: n.body,
      meta: formatDate(n.createdAt),
      date: n.createdAt,
      unread: !n.isRead,
      isPinned: false,
      severity: n.severity,
      onClick: () => openNotification(n),
    }));

    return [...notifItems, ...annItems].sort((left, right) => right.date.localeCompare(left.date));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [announcements, notifications]);

  function handleOpenChange(open: boolean) {
    if (!open) return;
    void refetchAnnUnread();
    void refetchAnnouncements();
    void refetchNotifUnread();
    void refetchNotifications();
  }

  function handleMarkAllRead() {
    if (annUnread > 0) markAllAnnouncementsRead.mutate();
    if (notifUnread > 0) markAllNotificationsRead.mutate();
  }

  return (
    <Popover onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="relative h-9 w-9"
          data-notification-bell
          aria-label={`Bildirimler${totalUnread > 0 ? ` (${totalUnread} okunmamış)` : ''}`}
        >
          <Bell className="h-5 w-5" />
          {totalUnread > 0 && (
            <span className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 rounded-full bg-destructive text-destructive-foreground text-[10px] font-semibold flex items-center justify-center">
              {totalUnread > 99 ? '99+' : totalUnread}
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-96 p-0">
        <div className="flex items-center justify-between px-4 py-3 border-b border-border/60">
          <div>
            <h3 className="font-heading font-bold text-sm">Bildirimler</h3>
            <p className="text-[11px] text-muted-foreground">
              {totalUnread > 0 ? `${totalUnread} okunmamış` : 'Tümü okundu'}
            </p>
          </div>
          {totalUnread > 0 && (
            <Button
              variant="ghost"
              size="sm"
              className="h-7 text-xs"
              onClick={handleMarkAllRead}
              disabled={markAllAnnouncementsRead.isPending || markAllNotificationsRead.isPending}
            >
              <CheckCheck className="h-3 w-3 mr-1" />
              Tümünü Okundu
            </Button>
          )}
        </div>

        <div className="max-h-[400px] overflow-y-auto">
          {isLoading ? (
            <p className="text-sm text-muted-foreground py-6 text-center">Yükleniyor...</p>
          ) : feed.length === 0 ? (
            <p className="text-sm text-muted-foreground py-6 text-center">Bildirim yok.</p>
          ) : (
            <ul>
              {feed.map((item) => (
                <li
                  key={item.key}
                  onClick={item.onClick}
                  className={cn(
                    'group cursor-pointer border-b border-border/40 px-4 py-3 transition-colors',
                    item.unread ? 'bg-primary/5 hover:bg-primary/10' : 'hover:bg-muted/50',
                  )}
                >
                  <div className="flex items-start gap-2">
                    {item.unread ? (
                      <span
                        className={cn(
                          'h-2 w-2 rounded-full shrink-0 mt-1.5',
                          item.source === 'notification' ? (SEVERITY_DOT[item.severity ?? 'Info'] ?? 'bg-primary') : 'bg-primary',
                        )}
                      />
                    ) : (
                      <span className="w-2 shrink-0" />
                    )}
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-1.5">
                        {item.source === 'announcement'
                          ? <Megaphone className="h-3 w-3 text-muted-foreground shrink-0" />
                          : null}
                        {item.isPinned && <Pin className="h-3 w-3 text-amber-500 shrink-0" />}
                        <p className={cn('text-sm truncate', item.unread ? 'font-semibold' : 'font-medium')}>
                          {item.title}
                        </p>
                      </div>
                      <p className="text-xs text-muted-foreground line-clamp-1 mt-0.5">
                        {item.preview}
                      </p>
                      <p className="text-[10px] text-muted-foreground mt-1">
                        {item.source === 'announcement' ? 'Duyuru' : 'Bildirim'} · {item.meta}
                      </p>
                    </div>
                    {item.source === 'notification' && (
                      <button
                        type="button"
                        onClick={(e) => { e.stopPropagation(); deleteNotification.mutate(item.rawId); }}
                        className="shrink-0 rounded p-1 text-muted-foreground/0 transition-colors group-hover:text-muted-foreground hover:bg-muted"
                        aria-label="Bildirimi sil"
                        title="Sil"
                      >
                        ×
                      </button>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="grid grid-cols-2 gap-px border-t border-border/60 bg-border/60">
          <Button
            variant="ghost"
            size="sm"
            className="rounded-none bg-background text-xs h-8"
            onClick={() => navigate(ROUTES.NOTIFICATIONS)}
          >
            Tüm Bildirimler
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="rounded-none bg-background text-xs h-8"
            onClick={() => navigate(ROUTES.ANNOUNCEMENTS)}
          >
            Duyurular
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
