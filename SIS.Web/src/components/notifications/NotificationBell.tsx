import { useNavigate } from 'react-router-dom';
import { Bell, CheckCheck, Pin } from 'lucide-react';
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
import { ROUTES } from '../../lib/constants';

const POLL_INTERVAL_MS = 30_000;
const PREVIEW_COUNT = 10;

/**
 * TopBar'da duran 🔔 zil. Polling ile duyuru okunmamış sayısını günceller;
 * dropdown'da son duyuruları gösterir. Ek bir <c>Notification</c> tablosu kurulmadı —
 * mevcut <c>AnnouncementRead</c> altyapısı zaten "okundu" durumunu yönetiyor.
 *
 * <p>İleride başka tür bildirimler (onay/danışman mesajı vb.) eklenirse generic
 * Notification tablosu açılır ve bu bell her iki kaynağı toplar.</p>
 */
export default function NotificationBell() {
  const navigate = useNavigate();
  const { data: unreadCount = 0 } = useUnreadAnnouncementCount(true, POLL_INTERVAL_MS);
  const { data: items = [], isLoading } = useActiveAnnouncements(PREVIEW_COUNT);
  const markAllReadMutation = useMarkAllAnnouncementsAsRead();

  function openItem(id: number) {
    // Detay dialog'u /duyurular sayfasında deep-link ile açılır
    navigate(`${ROUTES.ANNOUNCEMENTS}?openId=${id}`);
  }

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="relative h-9 w-9"
          aria-label={`Bildirimler${unreadCount > 0 ? ` (${unreadCount} okunmamış)` : ''}`}
        >
          <Bell className="h-5 w-5" />
          {unreadCount > 0 && (
            <span
              className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 rounded-full bg-destructive text-destructive-foreground text-[10px] font-semibold flex items-center justify-center"
            >
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-96 p-0">
        <div className="flex items-center justify-between px-4 py-3 border-b border-border/60">
          <div>
            <h3 className="font-heading font-bold text-sm">Bildirimler</h3>
            <p className="text-[11px] text-muted-foreground">
              {unreadCount > 0 ? `${unreadCount} okunmamış` : 'Tümü okundu'}
            </p>
          </div>
          {unreadCount > 0 && (
            <Button
              variant="ghost"
              size="sm"
              className="h-7 text-xs"
              onClick={() => markAllReadMutation.mutate()}
              disabled={markAllReadMutation.isPending}
            >
              <CheckCheck className="h-3 w-3 mr-1" />
              Tümünü Okundu
            </Button>
          )}
        </div>

        <div className="max-h-[400px] overflow-y-auto">
          {isLoading ? (
            <p className="text-sm text-muted-foreground py-6 text-center">Yükleniyor...</p>
          ) : items.length === 0 ? (
            <p className="text-sm text-muted-foreground py-6 text-center">Bildirim yok.</p>
          ) : (
            <ul>
              {items.map((a) => {
                const unread = !a.isReadByMe;
                return (
                  <li
                    key={a.id}
                    onClick={() => openItem(a.id)}
                    className={cn(
                      'cursor-pointer border-b border-border/40 px-4 py-3 transition-colors',
                      unread ? 'bg-primary/5 hover:bg-primary/10' : 'hover:bg-muted/50',
                    )}
                  >
                    <div className="flex items-start gap-2">
                      {unread && (
                        <span className="h-2 w-2 rounded-full bg-primary shrink-0 mt-1.5" />
                      )}
                      {!unread && <span className="w-2 shrink-0" />}
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-1.5">
                          {a.isPinned && <Pin className="h-3 w-3 text-amber-500 shrink-0" />}
                          <p
                            className={cn(
                              'text-sm truncate',
                              unread ? 'font-semibold' : 'font-medium',
                            )}
                          >
                            {a.title}
                          </p>
                        </div>
                        <p className="text-xs text-muted-foreground line-clamp-1 mt-0.5">
                          {a.contentPreview}
                        </p>
                        <p className="text-[10px] text-muted-foreground mt-1">
                          {formatDate(a.startsAt)} · {a.createdByName}
                        </p>
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </div>

        <div className="px-4 py-2 border-t border-border/60 bg-muted/30">
          <Button
            variant="ghost"
            size="sm"
            className="w-full justify-center text-xs h-7"
            onClick={() => navigate(ROUTES.ANNOUNCEMENTS)}
          >
            Tüm Duyuruları Gör
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
