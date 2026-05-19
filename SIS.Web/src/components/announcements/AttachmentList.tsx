import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Download, FileText, Image as ImageIcon, Loader2, Trash2 } from 'lucide-react';
import { downloadAnnouncementAttachment } from '../../api/announcements.api';
import type { AnnouncementAttachmentSummary } from '../../types/announcements';

interface AttachmentListProps {
  announcementId: number;
  attachments: AnnouncementAttachmentSummary[];
  /** true ise her eke "sil" butonu gösterilir (yöneticilere). */
  canDelete?: boolean;
  /** onDelete sağlanırsa sil butonu çağrılır. */
  onDelete?: (attachmentId: number) => void;
  isDeleting?: boolean;
}

export default function AttachmentList({
  announcementId,
  attachments,
  canDelete = false,
  onDelete,
  isDeleting,
}: AttachmentListProps) {
  const [downloadingId, setDownloadingId] = useState<number | null>(null);
  const [errorId, setErrorId] = useState<number | null>(null);

  if (attachments.length === 0) return null;

  async function handleDownload(att: AnnouncementAttachmentSummary) {
    setErrorId(null);
    setDownloadingId(att.id);
    try {
      await downloadAnnouncementAttachment(announcementId, att.id, att.fileName);
    } catch {
      setErrorId(att.id);
    } finally {
      setDownloadingId(null);
    }
  }

  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Ekler ({attachments.length})
      </p>
      <ul className="space-y-1.5">
        {attachments.map((att) => (
          <li
            key={att.id}
            className="flex items-center gap-2 rounded-md border border-border/60 bg-muted/30 px-3 py-2 text-sm"
          >
            <AttachmentIcon mimeType={att.mimeType} />
            <div className="min-w-0 flex-1">
              <div className="truncate font-medium" title={att.fileName}>
                {att.fileName}
              </div>
              <div className="text-[11px] text-muted-foreground">
                {formatBytes(att.sizeBytes)}
                {errorId === att.id && <span className="text-destructive ml-2">İndirme başarısız.</span>}
              </div>
            </div>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              onClick={() => handleDownload(att)}
              disabled={downloadingId === att.id}
              title="İndir"
            >
              {downloadingId === att.id ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Download className="h-4 w-4" />
              )}
            </Button>
            {canDelete && onDelete && (
              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8"
                onClick={() => onDelete(att.id)}
                disabled={isDeleting}
                title="Eki Sil"
              >
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

function AttachmentIcon({ mimeType }: { mimeType: string }) {
  if (mimeType.startsWith('image/'))
    return <ImageIcon className="h-4 w-4 text-muted-foreground shrink-0" />;
  return <FileText className="h-4 w-4 text-muted-foreground shrink-0" />;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}
