import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Pin, Calendar, Paperclip } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatDate } from '../../lib/utils';
import { getAudienceBadgeLabel } from '../../types/announcements';
import type { AnnouncementList } from '../../types/announcements';

interface AnnouncementCardProps {
  announcement: AnnouncementList;
  onClick?: () => void;
  compact?: boolean;
}

export default function AnnouncementCard({ announcement, onClick, compact }: AnnouncementCardProps) {
  const expired =
    announcement.expiresAt != null && new Date(announcement.expiresAt) < new Date();
  const unread = !announcement.isReadByMe;

  return (
    <Card
      onClick={onClick}
      className={cn(
        'transition-all relative',
        announcement.isPinned && 'border-l-4 border-l-amber-500',
        unread && !announcement.isPinned && 'border-l-4 border-l-primary',
        onClick && 'cursor-pointer hover:shadow-md hover:-translate-y-0.5',
        expired && 'opacity-60'
      )}
    >
      <CardContent className={cn('space-y-2', compact ? 'p-3' : 'p-4')}>
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0">
            {announcement.isPinned && <Pin className="h-3.5 w-3.5 text-amber-500 shrink-0" />}
            {unread && (
              <span
                className="h-2 w-2 rounded-full bg-primary shrink-0"
                aria-label="Okunmamış duyuru"
                title="Okunmamış"
              />
            )}
            <h3
              className={cn(
                'text-foreground truncate',
                compact ? 'text-sm' : 'text-base',
                unread ? 'font-bold' : 'font-semibold'
              )}
            >
              {announcement.title}
            </h3>
          </div>
          <div className="flex items-center gap-1.5 shrink-0">
            {announcement.category && (
              <Badge
                variant="outline"
                className="text-[10px] max-w-[100px] truncate border-0"
                style={{
                  backgroundColor: `${announcement.category.colorHex}1a`,
                  color: announcement.category.colorHex,
                }}
              >
                {announcement.category.name}
              </Badge>
            )}
            <Badge variant="outline" className="text-[10px] max-w-[160px] truncate">
              {getAudienceBadgeLabel(announcement)}
            </Badge>
          </div>
        </div>

        <p className={cn('text-muted-foreground', compact ? 'text-xs line-clamp-2' : 'text-sm line-clamp-3')}>
          {announcement.contentPreview}
        </p>

        <div className="flex items-center gap-3 text-[11px] text-muted-foreground">
          <span className="flex items-center gap-1">
            <Calendar className="h-3 w-3" />
            {formatDate(announcement.startsAt)}
          </span>
          <span>· {announcement.createdByName}</span>
          {announcement.attachmentCount > 0 && (
            <span className="flex items-center gap-1" title={`${announcement.attachmentCount} dosya eki`}>
              <Paperclip className="h-3 w-3" />
              {announcement.attachmentCount}
            </span>
          )}
          {expired && <Badge variant="secondary" className="text-[10px]">Süresi Doldu</Badge>}
        </div>
      </CardContent>
    </Card>
  );
}
