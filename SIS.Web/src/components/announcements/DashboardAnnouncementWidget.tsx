import { useEffect, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { Check, Pin, Megaphone, ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  useActiveAnnouncements,
  useAnnouncementDetail,
  useMarkAnnouncementAsRead,
} from '../../hooks/useAnnouncements';
import { getAudienceBadgeLabel } from '../../types/announcements';
import RichTextViewer from './RichTextViewer';
import { ROUTES } from '../../lib/constants';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { formatDate } from '../../lib/utils';

interface DashboardAnnouncementWidgetProps {
  max?: number;
}

export default function DashboardAnnouncementWidget({ max = 5 }: DashboardAnnouncementWidgetProps) {
  const { data, isLoading } = useActiveAnnouncements(max);
  const [selectedId, setSelectedId] = useState<number | null>(null);

  if (isLoading) {
    return <Skeleton className="h-48 rounded-xl" />;
  }

  const items = data ?? [];

  return (
    <Card>
      <CardContent className="p-5">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <Megaphone className="h-4 w-4 text-primary" />
            <h3 className="font-heading text-sm font-bold uppercase tracking-wide">Duyurular</h3>
          </div>
          <Link
            to={ROUTES.ANNOUNCEMENTS}
            className="text-xs text-primary hover:underline flex items-center gap-1"
          >
            Tümü
            <ArrowRight className="h-3 w-3" />
          </Link>
        </div>

        {items.length === 0 ? (
          <p className="text-sm text-muted-foreground py-6 text-center">Duyuru bulunmuyor.</p>
        ) : (
          <ul className="space-y-2">
            {items.map((a) => {
              const unread = !a.isReadByMe;
              return (
                <li
                  key={a.id}
                  onClick={() => setSelectedId(a.id)}
                  className={cn(
                    'group cursor-pointer rounded-md border p-3 transition-colors',
                    unread
                      ? 'border-primary/40 bg-primary/5 hover:bg-primary/10'
                      : 'border-border/60 hover:border-primary/40 hover:bg-muted/30'
                  )}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-center gap-1.5 min-w-0">
                      {a.isPinned && <Pin className="h-3 w-3 text-amber-500 shrink-0" />}
                      {unread && (
                        <span
                          className="h-1.5 w-1.5 rounded-full bg-primary shrink-0"
                          aria-label="Okunmamış"
                        />
                      )}
                      <span className={cn('text-sm truncate', unread ? 'font-semibold' : 'font-medium')}>
                        {a.title}
                      </span>
                    </div>
                    <Badge variant="outline" className="shrink-0 text-[10px] max-w-[120px] truncate">
                      {getAudienceBadgeLabel(a)}
                    </Badge>
                  </div>
                  <p className="text-xs text-muted-foreground line-clamp-1 mt-1">{a.contentPreview}</p>
                  <p className="text-[10px] text-muted-foreground mt-1">
                    {formatDate(a.startsAt)} · {a.createdByName}
                  </p>
                </li>
              );
            })}
          </ul>
        )}

        <AnnouncementViewDialog id={selectedId} onClose={() => setSelectedId(null)} />
      </CardContent>
    </Card>
  );
}

function AnnouncementViewDialog({ id, onClose }: { id: number | null; onClose: () => void }) {
  const { data, isLoading } = useAnnouncementDetail(id, id != null);
  const markReadMutation = useMarkAnnouncementAsRead();

  useEffect(() => {
    if (data && !data.isReadByMe && !markReadMutation.isPending) {
      markReadMutation.mutate(data.id);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data?.id, data?.isReadByMe]);

  return (
    <Dialog open={id != null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{isLoading ? 'Yükleniyor...' : data?.title}</DialogTitle>
        </DialogHeader>

        {data && (
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-xs text-muted-foreground flex-wrap">
              <Badge variant="outline">
                {getAudienceBadgeLabel(data)}
              </Badge>
              <span>{formatDate(data.startsAt)}</span>
              <span>·</span>
              <span>{data.createdByName}</span>
              {data.isReadByMe && (
                <Badge variant="secondary" className="text-[10px] gap-1">
                  <Check className="h-3 w-3" />
                  Okundu
                </Badge>
              )}
            </div>
            <RichTextViewer content={data.content} format={data.contentFormat} className="text-sm leading-relaxed" />
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
