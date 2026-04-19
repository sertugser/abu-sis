import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Pin, Calendar } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatDate } from '../../lib/utils';
import { ANNOUNCEMENT_AUDIENCE_LABELS } from '../../types/announcements';
import type { AnnouncementList } from '../../types/announcements';

interface AnnouncementCardProps {
  announcement: AnnouncementList;
  onClick?: () => void;
  compact?: boolean;
}

export default function AnnouncementCard({ announcement, onClick, compact }: AnnouncementCardProps) {
  const expired =
    announcement.expiresAt != null && new Date(announcement.expiresAt) < new Date();

  return (
    <Card
      onClick={onClick}
      className={cn(
        'transition-all',
        announcement.isPinned && 'border-l-4 border-l-amber-500',
        onClick && 'cursor-pointer hover:shadow-md hover:-translate-y-0.5',
        expired && 'opacity-60'
      )}
    >
      <CardContent className={cn('space-y-2', compact ? 'p-3' : 'p-4')}>
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0">
            {announcement.isPinned && <Pin className="h-3.5 w-3.5 text-amber-500 shrink-0" />}
            <h3 className={cn('font-semibold text-foreground truncate', compact ? 'text-sm' : 'text-base')}>
              {announcement.title}
            </h3>
          </div>
          <Badge variant="outline" className="shrink-0 text-[10px]">
            {announcement.targetAudience === 'Department' && announcement.targetDepartmentName
              ? announcement.targetDepartmentName
              : ANNOUNCEMENT_AUDIENCE_LABELS[announcement.targetAudience]}
          </Badge>
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
          {expired && <Badge variant="secondary" className="text-[10px]">Süresi Doldu</Badge>}
        </div>
      </CardContent>
    </Card>
  );
}
