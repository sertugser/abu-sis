// SIS.Web/src/components/grade/ComponentPublishBadge.tsx
// Bileşen kolon başlığındaki küçük "İlan Et / İlan Edildi" rozeti.
// Tıklayınca o bileşeni ilan eder / ilanı geri alır (per-component publish).

import { Eye, EyeOff } from 'lucide-react';

interface ComponentPublishBadgeProps {
  published: boolean;
  publishedAt?: string | null;
  disabled?: boolean;
  onPublish: () => void;
  onUnpublish: () => void;
}

export function ComponentPublishBadge({
  published,
  publishedAt,
  disabled = false,
  onPublish,
  onUnpublish,
}: ComponentPublishBadgeProps) {
  if (published) {
    return (
      <button
        type="button"
        disabled={disabled}
        onClick={onUnpublish}
        title={`İlan: ${publishedAt ? new Date(publishedAt).toLocaleString('tr-TR') : ''} — tıklayınca geri alınır`}
        className="inline-flex items-center gap-1 rounded-md border border-emerald-500/40 bg-emerald-50 px-1.5 py-0.5 text-[10px] font-medium text-emerald-700 transition-colors hover:bg-emerald-100 disabled:cursor-not-allowed disabled:opacity-60"
      >
        <EyeOff className="h-3 w-3" />
        İlan Edildi
      </button>
    );
  }

  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onPublish}
      title="Öğrenci bu bileşenin notunu ve sınıf istatistiklerini görmeye başlar"
      className="inline-flex items-center gap-1 rounded-md border border-input bg-background px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground transition-colors hover:border-emerald-500/40 hover:bg-emerald-50 hover:text-emerald-700 disabled:cursor-not-allowed disabled:opacity-60"
    >
      <Eye className="h-3 w-3" />
      İlan Et
    </button>
  );
}
