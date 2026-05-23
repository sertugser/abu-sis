import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { fmt } from '@/lib/calendarGenerator/dates';
import { draftStatusBadgeVariant } from '@/lib/calendarGenerator/processOptions';
import type { CalendarDraftSummaryDto } from '@/hooks/useCalendarGenerator';

interface DraftHistoryDialogProps {
  open: boolean;
  drafts?: CalendarDraftSummaryDto[];
  isLoading: boolean;
  isFetching: boolean;
  deletePending: boolean;
  onClose: () => void;
  onRefetch: () => void;
  onOpenDraft: (summary: CalendarDraftSummaryDto) => void;
  onDeleteDraft: (summary: CalendarDraftSummaryDto) => void;
  onDeleteAll: () => void;
}

export function DraftHistoryDialog({
  open,
  drafts,
  isLoading,
  isFetching,
  deletePending,
  onClose,
  onRefetch,
  onOpenDraft,
  onDeleteDraft,
  onDeleteAll,
}: DraftHistoryDialogProps) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 px-4 py-6">
      <div className="w-full max-w-4xl rounded-lg border border-slate-200 bg-white p-5 shadow-xl">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">Taslak geçmişi</h2>
            <p className="mt-1 text-sm text-slate-600">
              Önceden üretilen taslakları açabilir veya artık gerek olmayanları silebilirsin.
            </p>
          </div>
          <div className="flex gap-2">
            <Button type="button" size="sm" variant="outline" onClick={onRefetch} disabled={isFetching}>
              Yenile
            </Button>
            <Button
              type="button"
              size="sm"
              variant="destructive"
              onClick={onDeleteAll}
              disabled={deletePending || isLoading || (drafts?.length ?? 0) === 0}
            >
              Tümünü Sil
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={onClose}>
              Kapat
            </Button>
          </div>
        </div>

        <div className="mt-4 max-h-[70vh] overflow-auto">
          {isLoading ? (
            <p className="rounded-lg border border-slate-200 px-3 py-4 text-sm text-slate-500">Taslaklar yükleniyor...</p>
          ) : (drafts?.length ?? 0) === 0 ? (
            <p className="rounded-lg border border-slate-200 px-3 py-4 text-sm text-slate-500">Henüz taslak yok.</p>
          ) : (
            <div className="space-y-2">
              {drafts?.map(summary => (
                <div key={summary.id} className="grid gap-3 rounded-lg border border-slate-200 px-3 py-3 text-sm md:grid-cols-[1fr_auto] md:items-center">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-semibold text-slate-900">Taslak #{summary.id}</span>
                      <Badge variant={draftStatusBadgeVariant(summary.status)}>{summary.status}</Badge>
                    </div>
                    <div className="mt-2 text-slate-700">
                      {summary.academicYear} · {summary.eventCount} etkinlik · {summary.warningCount} uyarı
                    </div>
                    <div className="mt-1 text-xs text-slate-500">
                      Güncelleme: {fmt(summary.updatedAt)} · Saklama bitişi: {fmt(summary.expiresAt)}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2 md:justify-end">
                    <Button type="button" size="sm" variant="outline" onClick={() => onOpenDraft(summary)}>
                      Aç
                    </Button>
                    <Button type="button" size="sm" variant="destructive" onClick={() => onDeleteDraft(summary)} disabled={deletePending}>
                      Sil
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
