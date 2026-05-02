import { useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Megaphone, Search } from 'lucide-react';
import AnnouncementCard from '../components/announcements/AnnouncementCard';
import { useActiveAnnouncements, useAnnouncementDetail } from '../hooks/useAnnouncements';
import { getAudienceBadgeLabel } from '../types/announcements';
import { formatDate } from '../lib/utils';

export default function AnnouncementsPage() {
  const { data, isLoading } = useActiveAnnouncements();
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState<number | null>(null);

  const filtered = useMemo(() => {
    const list = data ?? [];
    if (!search.trim()) return list;
    const needle = search.toLowerCase();
    return list.filter(
      (a) =>
        a.title.toLowerCase().includes(needle) ||
        a.contentPreview.toLowerCase().includes(needle)
    );
  }, [data, search]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-heading text-xl font-bold">Duyurular</h1>
        <p className="text-sm text-muted-foreground">Güncel duyuru ve bildirimler.</p>
      </div>

      <div className="relative max-w-md">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          className="pl-9"
          placeholder="Duyuru ara..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
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

  return (
    <Dialog open={id != null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{isLoading ? 'Yükleniyor...' : data?.title}</DialogTitle>
        </DialogHeader>

        {data && (
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <Badge variant="outline">
                {getAudienceBadgeLabel(data)}
              </Badge>
              <span>{formatDate(data.startsAt)}</span>
              <span>·</span>
              <span>{data.createdByName}</span>
            </div>
            <div className="text-sm whitespace-pre-wrap leading-relaxed">{data.content}</div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
