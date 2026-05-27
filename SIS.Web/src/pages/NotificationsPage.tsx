import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, CheckCheck, ChevronLeft, ChevronRight, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { cn } from '@/lib/utils';
import {
  useDeleteNotification,
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotificationsPage,
  type NotificationDto,
} from '@/hooks/useNotifications';

const TYPE_LABELS: Record<string, string> = {
  RegistrationSubmittedForApproval: 'Kayıt onay bekliyor',
  RegistrationApproved: 'Kayıt onaylandı',
  RegistrationRejected: 'Kayıt reddedildi',
  RegistrationWindowOpen: 'Kayıt açıldı',
  RegistrationWindowClosingSoon: 'Kayıt kapanıyor',
  GradesPublished: 'Notlar ilan edildi',
  AttendanceWarning: 'Devamsızlık uyarısı',
  AcademicCalendarMissing: 'Eksik takvim',
  TermPhaseChanged: 'Dönem fazı değişti',
  ScheduleApprovalRequested: 'Program onayı',
  Other: 'Diğer',
};

// Faz 1'de fiilen üretilen türler (filtre dropdown'u için).
const FILTERABLE_TYPES = ['RegistrationSubmittedForApproval', 'RegistrationApproved', 'RegistrationRejected'];

const SEVERITY_ACCENT: Record<string, string> = {
  Success: 'border-l-emerald-500',
  Warning: 'border-l-amber-500',
  Critical: 'border-l-red-500',
  Info: 'border-l-blue-500',
};

const PAGE_SIZE = 20;

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export default function NotificationsPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [type, setType] = useState<string>('all');
  const [onlyUnread, setOnlyUnread] = useState(false);

  const { data, isLoading } = useNotificationsPage({
    type: type === 'all' ? null : type,
    onlyUnread,
    page,
    pageSize: PAGE_SIZE,
  });
  const markRead = useMarkNotificationRead();
  const markAllRead = useMarkAllNotificationsRead();
  const remove = useDeleteNotification();

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;
  const totalCount = data?.totalCount ?? 0;

  function openItem(n: NotificationDto) {
    if (!n.isRead) markRead.mutate(n.id);
    if (n.link) navigate(n.link);
  }

  function changeFilter(next: () => void) {
    next();
    setPage(1);
  }

  return (
    <div className="mx-auto w-full max-w-3xl space-y-4 animate-fade-in">
      {/* Başlık */}
      <div className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white px-5 py-4 shadow-sm">
        <div className="flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-full bg-[#003366]/10 text-[#003366]">
            <Bell className="h-5 w-5" />
          </span>
          <div>
            <h1 className="text-lg font-bold text-slate-800">Bildirimler</h1>
            <p className="text-xs text-slate-500">{totalCount} kayıt</p>
          </div>
        </div>
        <Button variant="outline" size="sm" onClick={() => markAllRead.mutate()} disabled={markAllRead.isPending}>
          <CheckCheck className="h-4 w-4" />
          Tümünü okundu
        </Button>
      </div>

      {/* Filtreler */}
      <div className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm">
        <Select value={type} onValueChange={(v) => changeFilter(() => setType(v))}>
          <SelectTrigger className="h-9 w-56">
            <SelectValue placeholder="Tür" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tüm türler</SelectItem>
            {FILTERABLE_TYPES.map((t) => (
              <SelectItem key={t} value={t}>{TYPE_LABELS[t] ?? t}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <label className="flex h-9 cursor-pointer items-center gap-2 rounded-lg border border-slate-200 px-3 text-sm text-slate-700">
          <input
            type="checkbox"
            className="h-4 w-4 accent-[#003366]"
            checked={onlyUnread}
            onChange={(e) => changeFilter(() => setOnlyUnread(e.target.checked))}
          />
          Sadece okunmamış
        </label>
      </div>

      {/* Liste */}
      <div className="space-y-2">
        {isLoading ? (
          <div className="rounded-xl border border-slate-200 bg-white p-10 text-center text-slate-400">Yükleniyor…</div>
        ) : items.length === 0 ? (
          <div className="rounded-xl border border-slate-200 bg-white p-10 text-center text-slate-400">
            Gösterilecek bildirim yok.
          </div>
        ) : (
          items.map((n) => (
            <div
              key={n.id}
              onClick={() => openItem(n)}
              className={cn(
                'group flex items-start gap-3 rounded-xl border border-l-4 bg-white px-4 py-3 shadow-sm transition-all hover:shadow-md',
                SEVERITY_ACCENT[n.severity] ?? 'border-l-slate-300',
                n.link && 'cursor-pointer',
                !n.isRead && 'bg-blue-50/40',
              )}
            >
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  {!n.isRead && <span className="h-2 w-2 shrink-0 rounded-full bg-[#003366]" aria-hidden="true" />}
                  <p className={cn('truncate text-sm text-slate-800', n.isRead ? 'font-medium' : 'font-semibold')}>
                    {n.title}
                  </p>
                  <span className="ml-auto shrink-0 text-[10px] uppercase tracking-wide text-slate-400">
                    {TYPE_LABELS[n.type] ?? n.type}
                  </span>
                </div>
                <p className="mt-0.5 text-xs leading-5 text-slate-600">{n.body}</p>
                <p className="mt-1 text-[10px] text-slate-400">{fmtDateTime(n.createdAt)}</p>
              </div>
              <button
                type="button"
                onClick={(e) => { e.stopPropagation(); remove.mutate(n.id); }}
                className="shrink-0 rounded-md p-1.5 text-slate-300 transition-colors hover:bg-slate-100 hover:text-rose-500"
                aria-label="Bildirimi sil"
                title="Sil"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          ))
        )}
      </div>

      {/* Sayfalama */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-3">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="text-sm text-slate-600">{page} / {totalPages}</span>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      )}
    </div>
  );
}
