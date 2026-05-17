import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Check, CheckCheck, Search, Sparkles } from 'lucide-react';
import AnnouncementCard from '../components/announcements/AnnouncementCard';
import AnnouncementsEmptyState from '../components/announcements/AnnouncementsEmptyState';
import AttachmentList from '../components/announcements/AttachmentList';
import CategoryPillBar, {
  type CategoryFilterValue,
} from '../components/announcements/CategoryPillBar';
import RichTextViewer from '../components/announcements/RichTextViewer';
import {
  useActiveAnnouncements,
  useAnnouncementDetail,
  useMarkAllAnnouncementsAsRead,
  useMarkAnnouncementAsRead,
} from '../hooks/useAnnouncements';
import { useAnnouncementCategories } from '../hooks/useAnnouncementCategories';
import { getAudienceBadgeLabel } from '../types/announcements';
import { formatDate } from '../lib/utils';

export default function AnnouncementsPage() {
  const { data, isLoading } = useActiveAnnouncements();
  const { data: categories = [] } = useAnnouncementCategories();
  const markAllReadMutation = useMarkAllAnnouncementsAsRead();
  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<CategoryFilterValue>('all');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [searchParams, setSearchParams] = useSearchParams();

  // Deep-link: ?openId=X ile bildirim dropdown'undan gelen duyuru otomatik açılır
  useEffect(() => {
    const openId = searchParams.get('openId');
    if (openId) {
      const id = Number(openId);
      if (Number.isFinite(id) && id > 0) {
        setSelectedId(id);
      }
      searchParams.delete('openId');
      setSearchParams(searchParams, { replace: true });
    }
  }, [searchParams, setSearchParams]);

  const all = useMemo(() => data ?? [], [data]);
  const unreadCount = useMemo(() => all.filter((a) => !a.isReadByMe).length, [all]);

  const filtered = useMemo(() => {
    let list = all;

    if (categoryFilter === 'none') {
      list = list.filter((a) => a.category === null);
    } else if (typeof categoryFilter === 'number') {
      list = list.filter((a) => a.category?.id === categoryFilter);
    }

    if (search.trim()) {
      const needle = search.toLowerCase();
      list = list.filter(
        (a) =>
          a.title.toLowerCase().includes(needle) ||
          a.contentPreview.toLowerCase().includes(needle),
      );
    }

    return list;
  }, [all, search, categoryFilter]);

  return (
    <>
      <Card className="overflow-hidden border-border/70 shadow-sm">
        {/* Üst şerit: kategori pill'leri + arama + okundu durumu/aksiyon */}
        <div className="border-b border-border/60 bg-gradient-to-r from-card via-card to-accent/20">
          {/* Kategori sekmeleri */}
          {all.length > 0 && (
            <div className="px-5 pt-4 pb-3">
              <CategoryPillBar
                categories={categories}
                items={all}
                value={categoryFilter}
                onChange={setCategoryFilter}
              />
            </div>
          )}

          {/* Arama + durum/aksiyon */}
          <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-3">
            <div className="relative max-w-md flex-1 min-w-[200px]">
              <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground/60" />
              <Input
                className="pl-9 h-9 rounded-lg bg-card border-border/70"
                placeholder="Duyuru ara..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>

            <div className="flex items-center gap-2">
              {unreadCount > 0 ? (
                <>
                  <span className="flex items-center gap-1.5 text-sm">
                    <span className="relative flex h-2 w-2">
                      <span className="absolute inset-0 animate-ping rounded-full bg-primary opacity-60" />
                      <span className="relative h-2 w-2 rounded-full bg-primary" />
                    </span>
                    <span className="font-semibold text-primary tabular-nums">{unreadCount}</span>
                    <span className="text-muted-foreground">okunmamış</span>
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => markAllReadMutation.mutate()}
                    disabled={markAllReadMutation.isPending}
                  >
                    <CheckCheck className="h-4 w-4 mr-1.5" />
                    Tümünü Okundu
                  </Button>
                </>
              ) : all.length > 0 ? (
                <span className="flex items-center gap-1.5 text-sm text-muted-foreground">
                  <Sparkles className="h-3.5 w-3.5 text-emerald-500" />
                  Tüm duyurular okundu
                </span>
              ) : null}
            </div>
          </div>
        </div>

        {/* İçerik */}
        <div className="p-5">
          {isLoading ? (
            <div className="space-y-3">
              {[1, 2, 3].map((i) => (
                <Skeleton key={i} className="h-24 rounded-xl" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <AnnouncementsEmptyState
              title={search ? 'Sonuç bulunamadı' : 'Aktif duyuru yok'}
              description={
                search
                  ? `"${search}" için duyuru bulamadık. Farklı bir arama deneyin.`
                  : 'Yeni duyurular yayınlandığında burada görünecek. Bildirim ziline göz atabilirsiniz.'
              }
              tone="primary"
            />
          ) : (
            <div className="space-y-3 stagger-children">
              {filtered.map((a) => (
                <AnnouncementCard
                  key={a.id}
                  announcement={a}
                  onClick={() => setSelectedId(a.id)}
                />
              ))}
            </div>
          )}
        </div>
      </Card>

      <AnnouncementDetailDialog id={selectedId} onClose={() => setSelectedId(null)} />
    </>
  );
}

function AnnouncementDetailDialog({ id, onClose }: { id: number | null; onClose: () => void }) {
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
      <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto p-0">
        {/* Renkli üst şerit */}
        <div
          className="h-1.5"
          style={{
            background: data?.category
              ? `linear-gradient(90deg, ${data.category.colorHex}, ${data.category.colorHex}88)`
              : 'linear-gradient(90deg, oklch(0.32 0.08 260), oklch(0.55 0.12 250))',
          }}
        />
        <div className="px-6 pt-5 pb-2">
          <DialogHeader>
            <DialogTitle className="text-lg leading-tight">
              {isLoading ? 'Yükleniyor...' : data?.title}
            </DialogTitle>
          </DialogHeader>
        </div>

        {data && (
          <div className="space-y-3 px-6 pb-2">
            <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
              {data.category && (
                <Badge
                  variant="outline"
                  className="text-[10px] border-0 font-medium"
                  style={{
                    backgroundColor: `${data.category.colorHex}1a`,
                    color: data.category.colorHex,
                  }}
                >
                  {data.category.name}
                </Badge>
              )}
              <Badge variant="outline">{getAudienceBadgeLabel(data)}</Badge>
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
            <RichTextViewer
              content={data.content}
              format={data.contentFormat}
              className="text-sm leading-relaxed"
            />
            {data.attachments.length > 0 && (
              <AttachmentList announcementId={data.id} attachments={data.attachments} />
            )}
          </div>
        )}

        <DialogFooter className="px-6 py-4 border-t border-border/60 bg-muted/20">
          <Button variant="outline" onClick={onClose}>
            Kapat
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
