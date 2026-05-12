import { useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Check, CheckCheck, Megaphone, Search } from 'lucide-react';
import AnnouncementCard from '../components/announcements/AnnouncementCard';
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
  const [categoryFilter, setCategoryFilter] = useState<number | 'all' | 'none'>('all');
  const [selectedId, setSelectedId] = useState<number | null>(null);

  const unreadCount = useMemo(() => (data ?? []).filter((a) => !a.isReadByMe).length, [data]);

  const filtered = useMemo(() => {
    let list = data ?? [];

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
          a.contentPreview.toLowerCase().includes(needle)
      );
    }

    return list;
  }, [data, search, categoryFilter]);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="font-heading text-xl font-bold">Duyurular</h1>
          <p className="text-sm text-muted-foreground">
            {unreadCount > 0
              ? `${unreadCount} okunmamış duyuru var.`
              : 'Tüm duyurular okundu.'}
          </p>
        </div>
        {unreadCount > 0 && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => markAllReadMutation.mutate()}
            disabled={markAllReadMutation.isPending}
          >
            <CheckCheck className="h-4 w-4 mr-1.5" />
            Tümünü Okundu İşaretle
          </Button>
        )}
      </div>

      <div className="flex items-center gap-3 flex-wrap">
        <div className="relative max-w-md flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            className="pl-9"
            placeholder="Duyuru ara..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        {categories.length > 0 && (
          <select
            value={categoryFilter}
            onChange={(e) => {
              const v = e.target.value;
              setCategoryFilter(v === 'all' || v === 'none' ? v : Number(v));
            }}
            className="bg-card border border-input rounded-md px-3 py-2 text-sm h-9"
          >
            <option value="all">Tüm Kategoriler</option>
            <option value="none">Kategorisiz</option>
            {categories.map((cat) => (
              <option key={cat.id} value={cat.id}>
                {cat.name}
              </option>
            ))}
          </select>
        )}
      </div>

      {isLoading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-24 rounded-xl" />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <Alert>
          <Megaphone className="h-4 w-4" />
          <AlertDescription>
            {search ? 'Arama sonucu bulunamadı.' : 'Aktif duyuru bulunmuyor.'}
          </AlertDescription>
        </Alert>
      ) : (
        <div className="space-y-3">
          {filtered.map((a) => (
            <AnnouncementCard key={a.id} announcement={a} onClick={() => setSelectedId(a.id)} />
          ))}
        </div>
      )}

      <AnnouncementDetailDialog id={selectedId} onClose={() => setSelectedId(null)} />
    </div>
  );
}

function AnnouncementDetailDialog({ id, onClose }: { id: number | null; onClose: () => void }) {
  const { data, isLoading } = useAnnouncementDetail(id, id != null);
  const markReadMutation = useMarkAnnouncementAsRead();

  // Detay yüklendiğinde duyuru henüz okunmamışsa otomatik olarak okundu işaretle.
  // Hukuki sorumluluk için kullanıcının duyuruyu görüntülediği anı kayıt altına alır.
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

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Kapat</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
