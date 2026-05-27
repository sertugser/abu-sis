import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { AlertTriangle, Bell, CalendarDays, CheckCircle2, FileDown, LogOut, RefreshCw, Repeat2, Save, Undo2, UserPlus, X } from 'lucide-react';
import { CalendarVisualizer, type VisualizerEvent } from '@/components/CalendarVisualizer';
import { AcademicTimeline } from '@/components/AcademicTimeline';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { downloadAcademicCalendarPdf } from '@/lib/academicCalendarPdf';
import { cn } from '@/lib/utils';
import { displayTurkishText } from '@/lib/turkishText';
import { usePermissions } from '@/hooks/usePermissions';
import {
  useAcademicCalendarTerms,
  useAcademicYearCalendars,
  type StudentAcademicCalendarDto,
} from '@/hooks/useAcademicCalendar';
import { useCalendarDraftEditor } from '@/hooks/useCalendarDraftEditor';
import {
  EDIT_CALENDAR_PERMISSION_KEYS,
  academicYearForTerm,
  academicYearLabel,
  buildPdfSections,
  eventToVisualizerEvent,
  fallbackPhaseEvents,
  fmtDate,
  latestText,
  rangeText,
  visibleEventsForCalendar,
} from '@/lib/academicCalendarHelpers';

// Bir fazın "şu an açık mı" durumunu canlı nabızlı bir rozetle gösterir.
function PhaseStatusBadge({ open }: { open: boolean }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide transition-colors',
        open ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-400',
      )}
    >
      <span className={cn('h-1.5 w-1.5 rounded-full', open ? 'bg-emerald-500 animate-pulse-soft' : 'bg-slate-300')} aria-hidden="true" />
      {open ? 'Açık' : 'Kapalı'}
    </span>
  );
}

// Tek bir dönemin faz özetini gösterir (Ders Kayıt / Ekle-Bırak / Çekilme).
// Yıl genelinde toplulaştırma yapmaz — her dönem kendi tarihleriyle ayrı satırda.
// Aktif dönem yeşil gradyan + canlı "AKTİF" rozetiyle vurgulanır; satır kademeli açılır.
function TermPhaseRow({ calendar, isActive, index }: { calendar: StudentAcademicCalendarDto; isActive: boolean; index: number }) {
  const phases: { icon: ReactNode; label: string; value: string; open: boolean }[] = [
    { icon: <UserPlus className="w-3.5 h-3.5 text-blue-500" />, label: 'Ders Kayıt', value: rangeText([[calendar.registrationStart, calendar.registrationEnd]]), open: calendar.isRegistrationOpen },
    { icon: <Repeat2 className="w-3.5 h-3.5 text-emerald-500" />, label: 'Ekle / Bırak', value: rangeText([[calendar.addDropStart, calendar.addDropEnd]]), open: calendar.isAddDropOpen },
    { icon: <LogOut className="w-3.5 h-3.5 text-rose-500" />, label: 'Çekilme', value: latestText([calendar.withdrawalDeadline]), open: calendar.isWithdrawalOpen },
  ];

  return (
    <div
      className={cn(
        'group relative flex flex-col overflow-hidden transition-all duration-300 sm:flex-row sm:items-stretch animate-fade-in',
        isActive
          ? 'bg-gradient-to-r from-emerald-50/80 via-emerald-50/25 to-transparent hover:from-emerald-100/80 hover:via-emerald-50/40'
          : 'hover:bg-slate-50/80',
      )}
      style={{ animationDelay: `${index * 70}ms` }}
    >
      {isActive && (
        <span className="absolute inset-y-0 left-0 w-1 bg-gradient-to-b from-emerald-400 to-emerald-600" aria-hidden="true" />
      )}

      <div className="sm:w-44 shrink-0 px-4 py-2.5 flex flex-col justify-center">
        <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Dönem</span>
        <span className="flex items-center gap-1.5 text-sm font-semibold text-slate-800">
          <span className="truncate">{displayTurkishText(calendar.termTypeLabel)}</span>
          {isActive && (
            <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-emerald-500/10 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide text-emerald-600">
              <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse-soft" aria-hidden="true" />
              Aktif
            </span>
          )}
        </span>
      </div>

      <div className="flex-1 grid grid-cols-1 sm:grid-cols-3 divide-y sm:divide-y-0 sm:divide-x divide-slate-100">
        {phases.map(phase => (
          <div key={phase.label} className="flex min-h-[60px] flex-col justify-center gap-1 px-3 py-2.5">
            <div className="flex items-center justify-between gap-2">
              <span className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                <span className="transition-transform duration-300 group-hover:scale-110">{phase.icon}</span>
                {phase.label}
              </span>
              {phase.value !== '-' && <PhaseStatusBadge open={phase.open} />}
            </div>
            <span className="truncate text-sm font-medium text-slate-800">{phase.value}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export default function AcademicCalendarPage() {
  const terms = useAcademicCalendarTerms();
  const { hasAnyPermission } = usePermissions();
  const [explicitYear, setExplicitYear] = useState<number | null>(null);
  const [isDownloadingPdf, setIsDownloadingPdf] = useState(false);
  const [missingToastPhase, setMissingToastPhase] = useState<'in' | 'out' | null>(null);
  // Bildirimin zilin tam altına oturması için zil ikonunun ekran konumu.
  const [toastAnchor, setToastAnchor] = useState<{ left: number; top: number; originX: number } | null>(null);
  const canEditCalendar = hasAnyPermission(EDIT_CALENDAR_PERMISSION_KEYS);

  const academicYears = useMemo(() => {
    const years = new Set((terms.data ?? []).map(academicYearForTerm));
    return Array.from(years).sort((left, right) => right - left);
  }, [terms.data]);

  const activeTerm = terms.data?.find(term => term.isActive && !term.isCancelled);
  const selectedYear = explicitYear ?? (activeTerm ? academicYearForTerm(activeTerm) : null) ?? academicYears[0] ?? null;

  const selectedYearTerms = useMemo(
    () => (terms.data ?? [])
      .filter(term => academicYearForTerm(term) === selectedYear)
      .sort((left, right) => left.termStart.localeCompare(right.termStart)),
    [selectedYear, terms.data],
  );

  const calendarQueries = useAcademicYearCalendars(selectedYearTerms.map(term => term.termId), canEditCalendar);
  const serverCalendars = calendarQueries
    .map(query => query.data)
    .filter((calendar): calendar is StudentAcademicCalendarDto => Boolean(calendar));
  const {
    calendars,
    hasDraftChanges,
    isCalendarMutating,
    canUndo,
    isSaving,
    notice,
    errorNotice,
    setNotice,
    setErrorNotice,
    handleEventDrop,
    getDropFeedback,
    handleInvalidEventDrop,
    handleUndoDraftChange,
    handleCancelDraftChanges,
    handleSaveDraftChanges,
  } = useCalendarDraftEditor(serverCalendars, selectedYear);

  const isLoading = terms.isLoading || calendarQueries.some(query => query.isLoading);
  const missingCalendarTerms = selectedYearTerms.filter(term => !term.isCancelled && !term.hasCalendar);
  const termIdsWithCalendar = new Set(selectedYearTerms.filter(term => term.hasCalendar).map(term => term.termId));
  const activeTermIds = new Set(selectedYearTerms.filter(term => term.isActive && !term.isCancelled).map(term => term.termId));
  const populatedCalendars = calendars.filter(calendar => termIdsWithCalendar.has(calendar.termId));
  const missingTermNames = missingCalendarTerms.map(term => term.termName).join(', ');

  // Eksik takvim varsa: kalıcı banner yerine, sayfaya her girişte (ve yıl değişiminde)
  // bildirim zilinden düşen bir uyarı göster; 5 sn sonra yumuşak çıkışla kapansın.
  useEffect(() => {
    if (!missingTermNames) {
      setMissingToastPhase(null);
      return;
    }
    // Bildirimi tam bildirim zilinin altına yerleştir; zil bulunamazsa sağ üste düş.
    const toastWidth = Math.min(384, window.innerWidth - 16);
    const bell = document.querySelector<HTMLElement>('[data-notification-bell]');
    if (bell) {
      const rect = bell.getBoundingClientRect();
      const bellCenterX = rect.left + rect.width / 2;
      const left = Math.min(Math.max(bellCenterX - toastWidth / 2, 8), window.innerWidth - toastWidth - 8);
      setToastAnchor({ left, top: rect.bottom + 8, originX: bellCenterX - left });
      // Zil bir kez sallansın — bildirim oradan çıkıyormuş gibi.
      bell.classList.add('animate-bell-shake');
    } else {
      setToastAnchor(null);
    }
    setMissingToastPhase('in');
    const shakeTimer = setTimeout(() => bell?.classList.remove('animate-bell-shake'), 750);
    const closeTimer = setTimeout(() => setMissingToastPhase('out'), 4600);
    const removeTimer = setTimeout(() => setMissingToastPhase(null), 5000);
    return () => {
      clearTimeout(shakeTimer);
      clearTimeout(closeTimer);
      clearTimeout(removeTimer);
      bell?.classList.remove('animate-bell-shake');
    };
  }, [missingTermNames, selectedYear]);

  const visualizerEvents = useMemo(
    () => calendars.flatMap(calendar => {
      const publicEvents = calendar.events
        .map(event => eventToVisualizerEvent(calendar.termId, event, canEditCalendar && !calendar.isCancelled))
        .filter((event): event is VisualizerEvent => event !== null);

      return publicEvents.length > 0 ? publicEvents : fallbackPhaseEvents(calendar);
    }),
    [calendars, canEditCalendar],
  );
  const pdfSections = useMemo(
    () => buildPdfSections(calendars, canEditCalendar),
    [calendars, canEditCalendar],
  );

  const yearRangeValue = selectedYear
    ? `${fmtDate(`${selectedYear}-09-01`)} - ${fmtDate(`${selectedYear + 1}-08-31`)}`
    : '-';

  async function handleDownloadPdf() {
    if (pdfSections.length === 0)
      return;

    try {
      setIsDownloadingPdf(true);
      await new Promise(resolve => window.requestAnimationFrame(() => resolve(null)));

      downloadAcademicCalendarPdf({
        documentTitle: `${academicYearLabel(selectedYear)} Akademik Takvim`,
        fileName: `akademik-takvim-${academicYearLabel(selectedYear)}.pdf`,
        sections: pdfSections,
      });
    } finally {
      setIsDownloadingPdf(false);
    }
  }

  return (
    <div className="w-full min-w-0 max-w-full space-y-3 overflow-x-hidden">
      <Dialog open={!!errorNotice} onOpenChange={(open) => !open && setErrorNotice(null)}>
        <DialogContent className="max-w-md border-red-200">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-red-900">
              <AlertTriangle className="h-5 w-5 text-red-600" />
              Hata
            </DialogTitle>
            <DialogDescription className="text-sm leading-6 text-slate-700">
              {errorNotice}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button type="button" onClick={() => setErrorNotice(null)}>
              Tamam
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!notice} onOpenChange={(open) => !open && setNotice(null)}>
        <DialogContent className="max-w-md border-emerald-200">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-emerald-900">
              <CheckCircle2 className="h-5 w-5 text-emerald-600" />
              İşlem tamamlandı
            </DialogTitle>
            <DialogDescription className="text-sm leading-6 text-slate-700">
              {notice}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button type="button" onClick={() => setNotice(null)}>
              Tamam
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {missingToastPhase && missingTermNames && (
        <div
          className={cn(
            'fixed z-50 w-[min(92vw,24rem)]',
            !toastAnchor && 'right-3 top-[4.25rem] sm:right-5',
            missingToastPhase === 'out'
              ? 'pointer-events-none -translate-y-2 opacity-0 transition-all duration-300 ease-out'
              : 'animate-bell-drop',
          )}
          style={toastAnchor ? { left: toastAnchor.left, top: toastAnchor.top, transformOrigin: `${toastAnchor.originX}px 0px` } : undefined}
          role="status"
          aria-live="polite"
        >
          <div className="overflow-hidden rounded-xl border border-amber-200 bg-white/95 shadow-lg shadow-amber-500/10 backdrop-blur">
            <div className="flex items-start gap-3 px-4 py-3">
              <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-amber-100 text-amber-600">
                <Bell className="h-4 w-4" />
              </span>
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-amber-900">Eksik akademik takvim</p>
                <p className="mt-0.5 text-xs leading-5 text-slate-600">
                  <span className="font-medium text-slate-700">{missingTermNames}</span> dönemi için takvim tanımlanmamış.
                </p>
              </div>
              <button
                type="button"
                onClick={() => setMissingToastPhase('out')}
                className="-mr-1 -mt-1 shrink-0 rounded-md p-1 text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-600"
                aria-label="Bildirimi kapat"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="h-0.5 w-full bg-amber-100">
              <div className="h-full bg-amber-400 animate-toast-countdown" />
            </div>
          </div>
        </div>
      )}

      <Tabs defaultValue="calendar" className="space-y-3">
        {/* Üst kart: Akademik yıl + özet kartlar. Görünüm switcher kartın üst header'ına
            entegre edildi — kart-dışı ayrı bir TabsList kutucuğu görünmesin diye. */}
        <div className="border border-slate-200 rounded-xl bg-white shadow-sm overflow-hidden animate-fade-in transition-shadow duration-300 hover:shadow-md">
          <div className="flex items-center justify-between gap-3 px-4 py-2 border-b border-slate-100 bg-slate-50/60">
            <span className="text-[10px] font-bold uppercase tracking-[0.18em] text-slate-500">
              Görünüm
            </span>
            <div className="flex items-center gap-2">
              <TabsList className="h-8 bg-slate-200/60">
                <TabsTrigger value="calendar" className="text-xs h-6 px-3">Takvim</TabsTrigger>
                <TabsTrigger value="timeline" className="text-xs h-6 px-3">Zaman Çizelgesi</TabsTrigger>
                <TabsTrigger value="list" className="text-xs h-6 px-3">Liste</TabsTrigger>
              </TabsList>
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="h-8 text-xs"
                onClick={handleDownloadPdf}
                disabled={isDownloadingPdf || isCalendarMutating || pdfSections.length === 0}
              >
                {isDownloadingPdf ? <RefreshCw className="h-3.5 w-3.5 animate-spin" /> : <FileDown className="h-3.5 w-3.5" />}
                {isDownloadingPdf ? 'PDF hazirlaniyor' : 'PDF indir'}
              </Button>
            </div>
          </div>

          <div className="flex flex-col lg:flex-row lg:items-stretch">
            <div className="lg:w-72 lg:flex-shrink-0 bg-slate-50 lg:border-r border-slate-200">
              <label htmlFor="academic-year-select" className="sr-only">Akademik yıl</label>
              <Select value={selectedYear?.toString() ?? ''} onValueChange={(value) => setExplicitYear(Number(value))}>
                <SelectTrigger id="academic-year-select" aria-label="Akademik yıl seçimi" className="border-0 focus:ring-0 shadow-none h-full min-h-[64px] bg-transparent rounded-none px-4 font-semibold text-slate-800">
                  <div className="flex flex-col items-start gap-0.5">
                    <span className="text-[10px] font-bold uppercase tracking-[0.18em] text-slate-400">Akademik yıl seçimi</span>
                    <SelectValue placeholder="Akademik yıl seçin..." />
                  </div>
                </SelectTrigger>
                <SelectContent>
                  {academicYears.map(year => (
                    <SelectItem key={year} value={year.toString()}>
                      {academicYearLabel(year)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex-1 border-t lg:border-t-0 border-slate-200 divide-y divide-slate-100">
              <div className="flex items-center gap-2 px-4 py-2 bg-slate-50/40">
                <CalendarDays className="w-4 h-4 text-indigo-500" />
                <span className="text-[10px] font-bold uppercase tracking-wider text-slate-500">Akademik Yıl</span>
                <span className="ml-auto text-sm font-medium text-slate-800">{yearRangeValue}</span>
              </div>
              {populatedCalendars.length === 0 ? (
                <div className="flex min-h-[56px] items-center px-4 py-3 text-sm text-slate-400">
                  {academicYearLabel(selectedYear)} için tanımlı dönem takvimi yok.
                </div>
              ) : (
                populatedCalendars.map((calendar, index) => (
                  <TermPhaseRow
                    key={calendar.termId}
                    calendar={calendar}
                    isActive={activeTermIds.has(calendar.termId)}
                    index={index}
                  />
                ))
              )}
            </div>
          </div>
        </div>


        {isLoading ? (
          <div className="border border-gray-200 rounded-xl bg-white shadow-sm p-10 text-center text-gray-400">
            Takvim yükleniyor...
          </div>
        ) : (
          <>
          <TabsContent value="calendar" className="space-y-3 mt-0">
            <CalendarVisualizer
              events={visualizerEvents}
              inlineYearView
              filterableLegend
              headerActions={hasDraftChanges ? (
                <div className="flex flex-wrap items-center justify-end gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    onClick={handleUndoDraftChange}
                    disabled={isCalendarMutating || !canUndo}
                  >
                    <Undo2 className="h-4 w-4" />
                    Geri al
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={handleCancelDraftChanges}
                    disabled={isCalendarMutating}
                  >
                    <X className="h-4 w-4" />
                    Iptal
                  </Button>
                  <Button
                    type="button"
                    onClick={handleSaveDraftChanges}
                    disabled={isCalendarMutating}
                  >
                    {isSaving ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                    Kaydet
                  </Button>
                </div>
              ) : undefined}
              academicYearStart={selectedYear ?? undefined}
              onEventDrop={canEditCalendar ? handleEventDrop : undefined}
              getDropFeedback={canEditCalendar ? getDropFeedback : undefined}
              onInvalidEventDrop={handleInvalidEventDrop}
              dragDisabled={isCalendarMutating}
            />
          </TabsContent>

          <TabsContent value="timeline" className="space-y-3 mt-0">
            <AcademicTimeline events={visualizerEvents} />
          </TabsContent>

          <TabsContent value="list" className="space-y-3">
            {(() => {
              // İçeriği olan dönemleri filtrele. Boş kartlar listede yer kaplamasın —
              // kullanıcı öğrenciye açık etkinlik aramaya geldiğinde info-only dönemler
              // ("Öğrenciye açık etkinlik tanımlanmamış") sayfayı kalabalıklaştırıyor.
              const populated = calendars
                .map(calendar => ({ calendar, rows: visibleEventsForCalendar(calendar, canEditCalendar) }))
                .filter(item => item.rows.length > 0);

              if (populated.length === 0) {
                return (
                  <div className="border border-slate-200 rounded-xl bg-white shadow-sm p-10 text-center text-slate-400">
                    {canEditCalendar
                      ? 'Bu akademik yıl için etkinlik bulunamadı.'
                      : 'Bu akademik yıl için öğrenciye açık etkinlik bulunamadı.'}
                  </div>
                );
              }

              return populated.map(({ calendar, rows }) => (
                <div key={calendar.termId} className="border border-slate-200 rounded-xl bg-white shadow-sm overflow-hidden">
                  <div className="flex items-center justify-between px-5 py-3 border-b border-slate-100 bg-slate-50">
                    <h3 className="text-sm font-semibold text-slate-800 uppercase tracking-wide">
                      {displayTurkishText(calendar.termName)}
                    </h3>
                    <span className="text-[11px] text-slate-500">
                      {fmtDate(calendar.termStart)} - {fmtDate(calendar.termEnd)}
                    </span>
                  </div>

                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-[60%]">Takvim Adı</TableHead>
                        <TableHead className="w-[20%]">Başlangıç Tarihi</TableHead>
                        <TableHead className="w-[20%]">Bitiş Tarihi</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {rows.map(event => (
                        <TableRow key={event.id}>
                          <TableCell className="font-medium text-slate-800">
                            {displayTurkishText(event.title)}
                          </TableCell>
                          <TableCell className="text-slate-600">
                            {fmtDate(event.startDate)}
                          </TableCell>
                          <TableCell className="text-slate-600">
                            {event.endDate && event.endDate !== event.startDate
                              ? fmtDate(event.endDate)
                              : ''}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              ));
            })()}
          </TabsContent>
          </>
        )}
      </Tabs>
    </div>
  );
}
