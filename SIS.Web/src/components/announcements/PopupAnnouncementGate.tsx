import { useEffect, useState } from 'react';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Megaphone } from 'lucide-react';
import AttachmentList from './AttachmentList';
import RichTextViewer from './RichTextViewer';
import {
  useAcknowledgeAnnouncement,
  useMarkAnnouncementAsRead,
  usePendingPopupAnnouncements,
} from '../../hooks/useAnnouncements';
import { getAudienceBadgeLabel } from '../../types/announcements';
import { formatDate } from '../../lib/utils';

/**
 * Kullanıcı login olduktan sonra, görmesi gereken kritik pop-up duyuruları
 * sırayla blocking dialog olarak gösterir.
 *
 * <p><b>RequiresAcknowledgement = true</b>: kullanıcı "Okudum, anladım" butonuna
 * basana kadar pop-up kapatılamaz; arka plan erişilemez.</p>
 *
 * <p><b>RequiresAcknowledgement = false</b>: pop-up kapatılabilir, kapatınca
 * okundu işaretlenir ve bir daha açılmaz.</p>
 */
export default function PopupAnnouncementGate({ enabled }: { enabled: boolean }) {
  const { data: pending = [], refetch } = usePendingPopupAnnouncements(enabled);
  const acknowledgeMutation = useAcknowledgeAnnouncement();
  const markReadMutation    = useMarkAnnouncementAsRead();
  const [currentIndex, setCurrentIndex] = useState(0);

  // pending listesi değiştiğinde index'i resetle
  useEffect(() => {
    setCurrentIndex(0);
  }, [pending.length]);

  if (!enabled || pending.length === 0) return null;
  if (currentIndex >= pending.length) return null;

  const current = pending[currentIndex];
  const open = true;
  const isAckRequired = current.requiresAcknowledgement;

  async function handleAcknowledge() {
    try {
      await acknowledgeMutation.mutateAsync(current.id);
    } finally {
      advance();
    }
  }

  async function handleClose() {
    // RequiresAck olmayan pop-up: kapatınca okundu işaretle
    if (!isAckRequired && !current.isReadByMe) {
      try {
        await markReadMutation.mutateAsync(current.id);
      } catch {
        // ignore — sıradakine geç
      }
    }
    advance();
  }

  function advance() {
    if (currentIndex + 1 < pending.length) {
      setCurrentIndex(currentIndex + 1);
    } else {
      // Kuyruğun sonu — refetch ile yeniyle gelirse devam, yoksa kapanır
      void refetch();
      setCurrentIndex(0);
    }
  }

  return (
    <AlertDialog
      open={open}
      onOpenChange={(o) => {
        // RequiresAck ise kullanıcı dışarı tıklayarak ya da Esc'le kapatamaz
        if (!o && !isAckRequired) handleClose();
      }}
    >
      <AlertDialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
        <AlertDialogHeader>
          <div className="flex items-center gap-2 mb-2">
            <div className="rounded-full bg-primary/10 p-2">
              <Megaphone className="h-5 w-5 text-primary" />
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {current.category && (
                <Badge
                  variant="outline"
                  className="text-[10px] border-0"
                  style={{
                    backgroundColor: `${current.category.colorHex}1a`,
                    color: current.category.colorHex,
                  }}
                >
                  {current.category.name}
                </Badge>
              )}
              <Badge variant="outline" className="text-[10px]">
                {getAudienceBadgeLabel(current)}
              </Badge>
              {isAckRequired && (
                <Badge variant="destructive" className="text-[10px]">
                  Onay gerekli
                </Badge>
              )}
            </div>
          </div>
          <AlertDialogTitle>{current.title}</AlertDialogTitle>
          <AlertDialogDescription className="text-xs text-muted-foreground">
            {formatDate(current.startsAt)} · {current.createdByName}
            {pending.length > 1 && (
              <span className="ml-2">({currentIndex + 1} / {pending.length})</span>
            )}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <div className="space-y-3">
          <RichTextViewer
            content={current.content}
            format={current.contentFormat}
            className="text-sm leading-relaxed"
          />
          {current.attachments.length > 0 && (
            <AttachmentList announcementId={current.id} attachments={current.attachments} />
          )}
        </div>

        <AlertDialogFooter>
          {!isAckRequired && (
            <AlertDialogCancel onClick={handleClose}>Kapat</AlertDialogCancel>
          )}
          {isAckRequired ? (
            <AlertDialogAction
              onClick={handleAcknowledge}
              disabled={acknowledgeMutation.isPending}
            >
              {acknowledgeMutation.isPending ? 'Onaylanıyor...' : 'Okudum, Anladım'}
            </AlertDialogAction>
          ) : (
            <AlertDialogAction onClick={handleClose}>Devam Et</AlertDialogAction>
          )}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
