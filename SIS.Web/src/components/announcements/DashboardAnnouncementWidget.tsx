import { useEffect, useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { cn } from '@/lib/utils';
import { Check, Pin, Megaphone, ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  useActiveAnnouncements,
  useAnnouncementDetail,
  useMarkAnnouncementAsRead,
} from '../../hooks/useAnnouncements';
import { useAnnouncementCategories } from '../../hooks/useAnnouncementCategories';
import { getAudienceBadgeLabel } from '../../types/announcements';
import RichTextViewer from './RichTextViewer';
import AttachmentList from './AttachmentList';
import { ROUTES } from '../../lib/constants';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { formatDate } from '../../lib/utils';

type CategoryFilter = 'all' | 'none' | number;

interface DashboardAnnouncementWidgetProps {
  max?: number;
}

export default function DashboardAnnouncementWidget({ max = 5 }: DashboardAnnouncementWidgetProps) {
  // Sekme filtresi için tüm aktif duyuruları çekiyoruz; max hala student top-count'a etki ediyor
  const { data, isLoading } = useActiveAnnouncements(max);
  const { data: categories = [] } = useAnnouncementCategories();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [activeTab, setActiveTab] = useState<CategoryFilter>('all');

  const all = useMemo(() => data ?? [], [data]);

  // Yalnızca veride bulunan kategorileri sekme olarak göster (boş sekme kalabalık yapmasın)
  const visibleCategoryIds = useMemo(() => {
    const ids = new Set<number>();
    all.forEach((a) => a.category && ids.add(a.category.id));
    return ids;
  }, [all]);

  const filtered = useMemo(() => {
    if (activeTab === 'all') return all;
    if (activeTab === 'none') return all.filter((a) => a.category === null);
    return all.filter((a) => a.category?.id === activeTab);
  }, [all, activeTab]);

  // Kategori sayaçları (sekme rozeti için)
  const counts = useMemo(() => {
    const map = new Map<CategoryFilter, number>();
    map.set('all', all.length);
    map.set('none', all.filter((a) => a.category === null).length);
    for (const cat of categories) {
      const c = all.filter((a) => a.category?.id === cat.id).length;
      if (c > 0) map.set(cat.id, c);
    }
    return map;
  }, [all, categories]);

  if (isLoading) {
    return <Skeleton className="h-48 rounded-xl" />;
  }

  const items = filtered;
  const visibleCategories = categories.filter((c) => visibleCategoryIds.has(c.id));

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

        {/* Kategori sekmeleri — yalnızca birden fazla kategori varsa göster */}
        {visibleCategories.length > 0 && (
          <Tabs
            value={String(activeTab)}
            onValueChange={(v) => setActiveTab(v === 'all' || v === 'none' ? v : Number(v))}
            className="mb-3"
          >
            <TabsList className="h-8 flex-wrap">
              <TabsTrigger value="all" className="text-xs px-2.5">
                Tümü ({counts.get('all') ?? 0})
              </TabsTrigger>
              {visibleCategories.map((cat) => (
                <TabsTrigger key={cat.id} value={String(cat.id)} className="text-xs px-2.5">
                  <span
                    className="h-1.5 w-1.5 rounded-full mr-1.5"
                    style={{ backgroundColor: cat.colorHex }}
                  />
                  {cat.name} ({counts.get(cat.id) ?? 0})
                </TabsTrigger>
              ))}
              {(counts.get('none') ?? 0) > 0 && (
                <TabsTrigger value="none" className="text-xs px-2.5">
                  Diğer ({counts.get('none')})
                </TabsTrigger>
              )}
            </TabsList>
          </Tabs>
        )}

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
            {data.attachments.length > 0 && (
              <AttachmentList announcementId={data.id} attachments={data.attachments} />
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
