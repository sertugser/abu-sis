import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CalendarDays, CheckCircle2, GitBranch, RefreshCw, RotateCcw, Save, Wand2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import { CALENDAR_EVENT_LABELS, EVENT_TYPE_OPTIONS, calendarEventBadgeVariant } from '@/lib/calendarEventTypes';
import {
  type CalendarConstraintInputDto,
  type CalendarDraftDto,
  type CalendarTemplateDto,
  type GenerateCalendarResultDto,
  type GeneratedCalendarEventDto,
  type SemesterType,
  type UpsertCalendarTemplateDto,
  useApplyDraft,
  useCalendarDraft,
  useCalendarTemplates,
  useGenerateCalendar,
  useHolidayDefinitions,
  useRollbackDraft,
  useSaveCalendarTemplates,
  useUpdateDraftEvent,
  useWhatIfCalendar,
} from '../hooks/useCalendarGenerator';

type CalendarView = GenerateCalendarResultDto | CalendarDraftDto;

const SEMESTERS: { value: SemesterType; label: string }[] = [
  { value: 'Fall', label: 'Guz' },
  { value: 'Spring', label: 'Bahar' },
  { value: 'Summer', label: 'Yaz' },
];

const DEFAULT_EVENT = 'ClassesStart';

function defaultAcademicYear() {
  const now = new Date();
  const start = now.getMonth() >= 6 ? now.getFullYear() : now.getFullYear() - 1;
  return `${start}-${start + 1}`;
}

function academicYearStart(academicYear: string) {
  const parsed = Number(academicYear.split('-')[0]);
  return Number.isFinite(parsed) ? parsed : new Date().getFullYear();
}

function dateInput(value?: string | null) {
  return value ? value.substring(0, 10) : '';
}

function utcDate(value: string | null) {
  return value ? `${value}T00:00:00.000Z` : null;
}

function fmt(value?: string | null) {
  if (!value) return '-';
  return new Date(value).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' });
}

function daySpan(ev: GeneratedCalendarEventDto) {
  const start = dateInput(ev.startDate);
  const end = dateInput(ev.endDate);
  return end && end !== start ? `${fmt(ev.startDate)} - ${fmt(ev.endDate)}` : fmt(ev.startDate);
}

function getDraftId(calendar: CalendarView | null, selectedDraftId: number | null) {
  if (!calendar) return selectedDraftId;
  return 'id' in calendar ? calendar.id : calendar.draftId;
}

function getErrorMessage(error: unknown) {
  const fallback = 'Islem tamamlanamadi.';
  if (!error || typeof error !== 'object') return fallback;
  const maybe = error as { response?: { data?: { message?: string; error?: string } }; message?: string };
  return maybe.response?.data?.message ?? maybe.response?.data?.error ?? maybe.message ?? fallback;
}

function templatePayload(templates: CalendarTemplateDto[]): UpsertCalendarTemplateDto[] {
  return templates.map(({ name, chainCode, isActive, items }) => ({ name, chainCode, isActive, items }));
}

function qualityClass(score: number) {
  if (score >= 90) return 'bg-emerald-50 text-emerald-700 border-emerald-200';
  if (score >= 75) return 'bg-yellow-50 text-yellow-700 border-yellow-200';
  if (score >= 50) return 'bg-orange-50 text-orange-700 border-orange-200';
  return 'bg-red-50 text-red-700 border-red-200';
}

export default function AcademicCalendarGeneratorPage() {
  const initialYear = defaultAcademicYear();
  const startYear = academicYearStart(initialYear);
  const [activeTab, setActiveTab] = useState('generator');
  const [academicYear, setAcademicYear] = useState(initialYear);
  const [fallStartDate, setFallStartDate] = useState(`${startYear}-09-22`);
  const [springStartDate, setSpringStartDate] = useState(`${startYear + 1}-02-23`);
  const [includeSummer, setIncludeSummer] = useState(true);
  const [forceOverwrite, setForceOverwrite] = useState(false);
  const [durationText, setDurationText] = useState('{}');
  const [constraints, setConstraints] = useState<CalendarConstraintInputDto[]>([]);
  const [constraintEvent, setConstraintEvent] = useState(DEFAULT_EVENT);
  const [pinDate, setPinDate] = useState('');
  const [pinEndDate, setPinEndDate] = useState('');
  const [blockFrom, setBlockFrom] = useState('');
  const [blockTo, setBlockTo] = useState('');
  const [selectedSemester, setSelectedSemester] = useState<SemesterType>('Fall');
  const [selectedDraftId, setSelectedDraftId] = useState<number | null>(null);
  const [generated, setGenerated] = useState<GenerateCalendarResultDto | null>(null);
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({ title: '', startDate: '', endDate: '', note: '' });
  const [templateText, setTemplateText] = useState('');
  const [templateDirty, setTemplateDirty] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  const templates = useCalendarTemplates();
  const holidays = useHolidayDefinitions();
  const draft = useCalendarDraft(selectedDraftId);
  const generate = useGenerateCalendar();
  const updateDraftEvent = useUpdateDraftEvent();
  const whatIf = useWhatIfCalendar();
  const applyDraft = useApplyDraft();
  const rollbackDraft = useRollbackDraft();
  const saveTemplates = useSaveCalendarTemplates();

  const calendar = (draft.data ?? generated) as CalendarView | null;
  const activeDraftId = getDraftId(calendar, selectedDraftId);

  const semesterEvents = useMemo(
    () => (calendar?.events ?? []).filter(e => e.semester === selectedSemester).sort((a, b) => a.startDate.localeCompare(b.startDate)),
    [calendar, selectedSemester],
  );

  const selectedEvent = useMemo(
    () => semesterEvents.find(e => e.draftEventId === selectedEventId) ?? null,
    [semesterEvents, selectedEventId],
  );

  const whatIfDiffs = useMemo(() => {
    if (!whatIf.data || !calendar) return [];
    const current = new Map(calendar.events.map(e => [e.draftEventId, e]));
    return whatIf.data.events
      .map(next => ({ next, prev: current.get(next.draftEventId) }))
      .filter(({ next, prev }) => prev && (dateInput(prev.startDate) !== dateInput(next.startDate) || dateInput(prev.endDate) !== dateInput(next.endDate)))
      .slice(0, 12);
  }, [calendar, whatIf.data]);

  useEffect(() => {
    const parsedStart = academicYearStart(academicYear);
    setFallStartDate(`${parsedStart}-09-22`);
    setSpringStartDate(`${parsedStart + 1}-02-23`);
  }, [academicYear]);

  useEffect(() => {
    if (templates.data && !templateDirty) {
      setTemplateText(JSON.stringify(templatePayload(templates.data), null, 2));
    }
  }, [templates.data, templateDirty]);

  function selectEvent(ev: GeneratedCalendarEventDto) {
    setSelectedEventId(ev.draftEventId);
    setEditForm({ title: ev.title, startDate: dateInput(ev.startDate), endDate: dateInput(ev.endDate), note: ev.note ?? '' });
    whatIf.reset();
  }

  function addPinConstraint() {
    if (!pinDate) return;
    setConstraints(prev => [...prev, {
      constraintType: 'Pin',
      targetEventType: constraintEvent,
      targetChainCode: null,
      parameterJson: JSON.stringify({ startDate: utcDate(pinDate), endDate: utcDate(pinEndDate || pinDate) }),
      isHard: true,
      priority: 1,
      description: `${CALENDAR_EVENT_LABELS[constraintEvent] ?? constraintEvent} sabitlendi`,
    }]);
    setPinDate('');
    setPinEndDate('');
  }

  function addProhibitionConstraint() {
    if (!blockFrom || !blockTo) return;
    setConstraints(prev => [...prev, {
      constraintType: 'Prohibition',
      targetEventType: null,
      targetChainCode: null,
      parameterJson: JSON.stringify({ from: utcDate(blockFrom), to: utcDate(blockTo) }),
      isHard: true,
      priority: 1,
      description: `Yasak aralik: ${blockFrom} - ${blockTo}`,
    }]);
    setBlockFrom('');
    setBlockTo('');
  }

  function handleGenerate() {
    setNotice(null);
    let durationOverrides: Record<string, number> = {};
    try {
      durationOverrides = JSON.parse(durationText || '{}') as Record<string, number>;
    } catch {
      setNotice('Sure override JSON gecersiz.');
      return;
    }
    generate.mutate({
      academicYear,
      fallStartDate: utcDate(fallStartDate),
      springStartDate: utcDate(springStartDate),
      includeSummer,
      constraints,
      templateDurationOverrides: durationOverrides,
    }, {
      onSuccess: data => {
        setGenerated(data);
        setSelectedDraftId(data.draftId);
        setSelectedSemester('Fall');
        setSelectedEventId(null);
        setActiveTab('draft');
        setNotice(`Taslak #${data.draftId} olusturuldu.`);
      },
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  function handleWhatIf() {
    if (!activeDraftId || !selectedEvent) return;
    whatIf.mutate({
      draftId: activeDraftId,
      dto: {
        draftEventId: selectedEvent.draftEventId,
        startDate: utcDate(editForm.startDate) ?? selectedEvent.startDate,
        endDate: utcDate(editForm.endDate || editForm.startDate),
      },
    }, { onError: error => setNotice(getErrorMessage(error)) });
  }

  function handleUpdateEvent() {
    if (!activeDraftId || !selectedEvent) return;
    updateDraftEvent.mutate({
      draftId: activeDraftId,
      eventId: selectedEvent.draftEventId,
      dto: {
        startDate: utcDate(editForm.startDate) ?? selectedEvent.startDate,
        endDate: utcDate(editForm.endDate || editForm.startDate),
        title: editForm.title,
        note: editForm.note || null,
      },
    }, {
      onSuccess: data => {
        setGenerated(null);
        setSelectedDraftId(data.id);
        setSelectedEventId(null);
        setNotice('Taslak etkinligi guncellendi; bagimli tarihler yeniden hesaplandi.');
      },
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  function handleApply() {
    if (!activeDraftId) return;
    applyDraft.mutate({ draftId: activeDraftId, forceOverwrite }, {
      onSuccess: result => setNotice(`${result.message} Donemler: ${result.termIds.join(', ')}`),
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  function handleRollback() {
    if (!activeDraftId) return;
    rollbackDraft.mutate(activeDraftId, {
      onSuccess: result => setNotice(`${result.message} Donemler: ${result.termIds.join(', ')}`),
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  function handleSaveTemplates() {
    try {
      const parsed = JSON.parse(templateText) as UpsertCalendarTemplateDto[];
      if (!Array.isArray(parsed)) {
        setNotice('Sablon JSON dizi formatinda olmali.');
        return;
      }
      saveTemplates.mutate(parsed, {
        onSuccess: data => {
          setTemplateDirty(false);
          setTemplateText(JSON.stringify(templatePayload(data), null, 2));
          setNotice('Sablonlar kaydedildi.');
        },
        onError: error => setNotice(getErrorMessage(error)),
      });
    } catch {
      setNotice('Sablon JSON gecersiz.');
    }
  }

  function renderGeneratorTab() {
    return (
      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="grid gap-4 md:grid-cols-4">
            <label className="space-y-1 text-sm font-medium text-slate-700">
              Akademik yil
              <Input value={academicYear} onChange={e => setAcademicYear(e.target.value)} placeholder="2025-2026" />
            </label>
            <label className="space-y-1 text-sm font-medium text-slate-700">
              Guz ders baslangici
              <Input type="date" value={fallStartDate} onChange={e => setFallStartDate(e.target.value)} />
            </label>
            <label className="space-y-1 text-sm font-medium text-slate-700">
              Bahar ders baslangici
              <Input type="date" value={springStartDate} onChange={e => setSpringStartDate(e.target.value)} />
            </label>
            <div className="flex items-end">
              <label className="flex h-10 items-center gap-3 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700">
                <Switch checked={includeSummer} onCheckedChange={setIncludeSummer} />
                Yaz okulu dahil
              </label>
            </div>
          </div>

          <div className="mt-5 grid gap-4 lg:grid-cols-2">
            <div className="rounded-lg border border-slate-200 p-3">
              <div className="mb-3 flex items-center gap-2 font-semibold text-slate-800">
                <GitBranch className="h-4 w-4" />
                Pin tarih
              </div>
              <div className="grid gap-3 sm:grid-cols-3">
                <select
                  className="h-10 rounded-lg border border-slate-200 bg-white px-3 text-sm"
                  value={constraintEvent}
                  onChange={e => setConstraintEvent(e.target.value)}
                >
                  {EVENT_TYPE_OPTIONS.map(option => (
                    <option key={option.value} value={option.value}>{option.label}</option>
                  ))}
                </select>
                <Input type="date" value={pinDate} onChange={e => setPinDate(e.target.value)} />
                <Input type="date" value={pinEndDate} onChange={e => setPinEndDate(e.target.value)} />
              </div>
              <Button type="button" size="sm" className="mt-3" onClick={addPinConstraint}>
                Kisit ekle
              </Button>
            </div>

            <div className="rounded-lg border border-slate-200 p-3">
              <div className="mb-3 flex items-center gap-2 font-semibold text-slate-800">
                <AlertTriangle className="h-4 w-4" />
                Yasak aralik
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <Input type="date" value={blockFrom} onChange={e => setBlockFrom(e.target.value)} />
                <Input type="date" value={blockTo} onChange={e => setBlockTo(e.target.value)} />
              </div>
              <Button type="button" size="sm" className="mt-3" onClick={addProhibitionConstraint}>
                Aralik ekle
              </Button>
            </div>
          </div>

          <label className="mt-4 block space-y-1 text-sm font-medium text-slate-700">
            Sure override JSON
            <Textarea
              value={durationText}
              onChange={e => setDurationText(e.target.value)}
              className="font-mono text-xs"
              placeholder='{"final_exam": 12}'
            />
          </label>

          <div className="mt-4 flex flex-wrap items-center gap-3">
            <Button onClick={handleGenerate} disabled={generate.isPending}>
              {generate.isPending ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Wand2 className="h-4 w-4" />}
              Taslak uret
            </Button>
            <span className="text-xs text-slate-500">Taslaklar 30 gun saklanir; apply sadece generator kaynakli etkinlikleri gunceller.</span>
          </div>
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="font-semibold text-slate-900">Aktif kisitlar</h2>
          <div className="mt-3 space-y-2">
            {constraints.length === 0 ? (
              <p className="text-sm text-slate-500">Henuz ek kisit yok.</p>
            ) : constraints.map((constraint, index) => (
              <div key={`${constraint.constraintType}-${index}`} className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 px-3 py-2">
                <div>
                  <Badge variant={constraint.isHard ? 'destructive' : 'secondary'}>{constraint.constraintType}</Badge>
                  <p className="mt-1 text-sm text-slate-700">{constraint.description}</p>
                </div>
                <Button type="button" size="sm" variant="ghost" onClick={() => setConstraints(prev => prev.filter((_, i) => i !== index))}>
                  Sil
                </Button>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  function renderDraftTab() {
    if (!calendar) {
      return (
        <div className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-500">
          Once bir taslak uret.
        </div>
      );
    }

    return (
      <div className="space-y-4">
        <div className="grid gap-4 lg:grid-cols-3">
          <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm lg:col-span-2">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="font-semibold text-slate-900">Taslak #{activeDraftId}</h2>
                <p className="text-sm text-slate-500">{calendar.academicYear} icin {calendar.events.length} etkinlik uretildi.</p>
              </div>
              <div className="flex flex-wrap gap-2">
                {SEMESTERS.map(tab => (
                  <Button
                    key={tab.value}
                    type="button"
                    size="sm"
                    variant={selectedSemester === tab.value ? 'default' : 'outline'}
                    onClick={() => {
                      setSelectedSemester(tab.value);
                      setSelectedEventId(null);
                      whatIf.reset();
                    }}
                  >
                    {tab.label}
                  </Button>
                ))}
              </div>
            </div>

            <div className="mt-4 overflow-hidden rounded-lg border border-slate-200">
              <div className="grid grid-cols-[minmax(160px,1.5fr)_130px_120px_100px] bg-slate-50 px-3 py-2 text-xs font-semibold uppercase text-slate-500">
                <span>Etkinlik</span>
                <span>Tarih</span>
                <span>Zincir</span>
                <span>Durum</span>
              </div>
              <div className="max-h-[560px] overflow-auto">
                {semesterEvents.map(ev => (
                  <button
                    key={ev.draftEventId}
                    type="button"
                    onClick={() => selectEvent(ev)}
                    className={cn(
                      'grid w-full grid-cols-[minmax(160px,1.5fr)_130px_120px_100px] items-center gap-2 border-t border-slate-100 px-3 py-2 text-left text-sm hover:bg-slate-50',
                      selectedEventId === ev.draftEventId && 'bg-cyan-50',
                    )}
                  >
                    <span className="min-w-0">
                      <span className="block truncate font-medium text-slate-800">{ev.title}</span>
                      <span className="text-xs text-slate-500">{ev.eventTypeLabel}</span>
                    </span>
                    <span className="text-xs text-slate-600">{daySpan(ev)}</span>
                    <span className="text-xs text-slate-500">{ev.chainCode}</span>
                    <span className="flex flex-wrap gap-1">
                      <Badge variant={calendarEventBadgeVariant(ev.eventType)}>{ev.isHoliday ? 'Tatil' : 'Akis'}</Badge>
                      {ev.isFailed && <Badge variant="destructive">Hata</Badge>}
                    </span>
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
              <h2 className="font-semibold text-slate-900">What-if duzenleme</h2>
              {!selectedEvent ? (
                <p className="mt-2 text-sm text-slate-500">Bir etkinlik sec, tarih alanini degistir ve domino farkini hesapla.</p>
              ) : (
                <div className="mt-3 space-y-3">
                  <label className="block space-y-1 text-sm font-medium text-slate-700">
                    Baslik
                    <Input value={editForm.title} onChange={e => setEditForm(prev => ({ ...prev, title: e.target.value }))} />
                  </label>
                  <div className="grid grid-cols-2 gap-3">
                    <label className="block space-y-1 text-sm font-medium text-slate-700">
                      Baslangic
                      <Input type="date" value={editForm.startDate} onChange={e => setEditForm(prev => ({ ...prev, startDate: e.target.value }))} />
                    </label>
                    <label className="block space-y-1 text-sm font-medium text-slate-700">
                      Bitis
                      <Input type="date" value={editForm.endDate} onChange={e => setEditForm(prev => ({ ...prev, endDate: e.target.value }))} />
                    </label>
                  </div>
                  <label className="block space-y-1 text-sm font-medium text-slate-700">
                    Not
                    <Textarea value={editForm.note} onChange={e => setEditForm(prev => ({ ...prev, note: e.target.value }))} />
                  </label>
                  <div className="flex flex-wrap gap-2">
                    <Button type="button" size="sm" variant="outline" onClick={handleWhatIf} disabled={whatIf.isPending}>
                      <GitBranch className="h-4 w-4" />
                      Dominoyu goster
                    </Button>
                    <Button type="button" size="sm" onClick={handleUpdateEvent} disabled={updateDraftEvent.isPending}>
                      <Save className="h-4 w-4" />
                      Taslaga yaz
                    </Button>
                  </div>
                </div>
              )}
            </div>
            {renderDraftSidePanels(calendar)}
          </div>
        </div>

        {renderDraftSummaries(calendar)}
      </div>
    );
  }

  function renderDraftSidePanels(current: CalendarView) {
    return (
      <>
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="font-semibold text-slate-900">Domino farki</h2>
          {whatIfDiffs.length === 0 ? (
            <p className="mt-2 text-sm text-slate-500">Hesaplanmis fark yok.</p>
          ) : (
            <div className="mt-3 space-y-2">
              {whatIfDiffs.map(({ next, prev }) => (
                <div key={next.draftEventId} className="rounded-lg border border-slate-200 px-3 py-2 text-xs">
                  <div className="font-semibold text-slate-800">{next.title}</div>
                  <div className="mt-1 text-slate-500">
                    {fmt(prev?.startDate)} -&gt; {fmt(next.startDate)}
                  </div>
                </div>
              ))}
              <div className={cn('rounded-lg border px-3 py-2 text-sm font-semibold', qualityClass(whatIf.data?.qualityScore ?? current.qualityScore))}>
                What-if kalite skoru: {whatIf.data?.qualityScore}
              </div>
            </div>
          )}
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="font-semibold text-slate-900">Onayla ve kaydet</h2>
              <p className="text-xs text-slate-500">Published ve sonrasi icin force gerekir.</p>
            </div>
            <Switch checked={forceOverwrite} onCheckedChange={setForceOverwrite} />
          </div>
          <div className="mt-3 flex flex-wrap gap-2">
            <Button type="button" onClick={handleApply} disabled={!activeDraftId || applyDraft.isPending}>
              <CheckCircle2 className="h-4 w-4" />
              Onayla
            </Button>
            <Button type="button" variant="outline" onClick={handleRollback} disabled={!activeDraftId || rollbackDraft.isPending}>
              <RotateCcw className="h-4 w-4" />
              Geri al
            </Button>
          </div>
        </div>
      </>
    );
  }

  function renderDraftSummaries(current: CalendarView) {
    return (
      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="font-semibold text-slate-900">Uyarilar</h2>
          <div className="mt-3 space-y-2">
            {current.warnings.length === 0 ? (
              <p className="text-sm text-slate-500">Uyari yok.</p>
            ) : current.warnings.map((warning, index) => (
              <div key={`${warning.code}-${index}`} className="rounded-lg border border-slate-200 px-3 py-2 text-sm">
                <Badge variant={warning.severity === 'Error' ? 'destructive' : warning.severity === 'Warning' ? 'warning' : 'secondary'}>
                  {warning.severity}
                </Badge>
                <span className="ml-2 text-slate-700">{warning.message}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="font-semibold text-slate-900">Donem ozeti</h2>
          <div className="mt-3 grid gap-2 sm:grid-cols-3">
            {current.termSummaries.map(summary => (
              <div key={summary.semester} className="rounded-lg border border-slate-200 px-3 py-2 text-sm">
                <div className="font-semibold text-slate-800">{SEMESTERS.find(s => s.value === summary.semester)?.label}</div>
                <div className="mt-1 text-xs text-slate-500">{fmt(summary.termStart)} - {fmt(summary.termEnd)}</div>
                <div className="mt-2 text-xs text-slate-600">
                  {summary.teachingDays} ders gunu, {summary.holidayDaysInTerm} tatil gunu, {summary.examDays} sinav gunu
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  function renderTemplatesTab() {
    return (
      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="font-semibold text-slate-900">Sablon JSON</h2>
              <p className="text-sm text-slate-500">MAIN, ADMISSIONS ve SUMMER zincirleri burada duzenlenir.</p>
            </div>
            <Button type="button" onClick={handleSaveTemplates} disabled={saveTemplates.isPending}>
              <Save className="h-4 w-4" />
              Kaydet
            </Button>
          </div>
          <Textarea
            value={templateText}
            onChange={e => {
              setTemplateText(e.target.value);
              setTemplateDirty(true);
            }}
            className="mt-4 min-h-[520px] font-mono text-xs"
            placeholder="Sablonlar yukleniyor..."
          />
        </div>

        <div className="space-y-4">
          <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <h2 className="font-semibold text-slate-900">Tatil tanimlari</h2>
            <div className="mt-3 max-h-[360px] space-y-2 overflow-auto">
              {holidays.isLoading ? (
                <p className="text-sm text-slate-500">Yukleniyor...</p>
              ) : holidays.data?.map(holiday => (
                <div key={holiday.id} className="rounded-lg border border-slate-200 px-3 py-2 text-sm">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-semibold text-slate-800">{holiday.name}</span>
                    <Badge variant={holiday.blocksAcademicEvents ? 'success' : 'outline'}>{holiday.type}</Badge>
                  </div>
                  <div className="mt-1 text-xs text-slate-500">
                    {holiday.fixedMonth ? `${holiday.fixedDay}.${holiday.fixedMonth}` : `Hicri ${holiday.hijriDay}.${holiday.hijriMonth}`} - {holiday.durationDays} gun
                  </div>
                  {holiday.overrides.length > 0 && (
                    <div className="mt-1 text-xs text-slate-500">
                      Override: {holiday.overrides.map(o => `${o.year}: ${dateInput(o.startDate)}-${dateInput(o.endDate)}`).join(', ')}
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <h2 className="font-semibold text-slate-900">Olay tipleri</h2>
            <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
              {EVENT_TYPE_OPTIONS.map(option => (
                <div key={option.value} className="rounded-lg border border-slate-200 px-2 py-1">
                  <span className="font-mono text-slate-500">{option.value}</span>
                  <span className="block text-slate-700">{option.label}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="flex items-center gap-2 text-sm font-semibold text-slate-500">
            <CalendarDays className="h-4 w-4" />
            Akademik isler
          </div>
          <h1 className="mt-1 text-2xl font-bold text-slate-900">Akademik Takvim Olusturma</h1>
          <p className="mt-1 max-w-3xl text-sm text-slate-600">
            Guz, Bahar ve Yaz donemlerini birlikte uret; tatil cakismalarini, domino etkisini ve not ilani siralamasini tek taslakta kontrol et.
          </p>
        </div>
        <div className={cn('rounded-lg border px-4 py-3 text-sm font-semibold', qualityClass(calendar?.qualityScore ?? 0))}>
          Kalite skoru: {calendar?.qualityScore ?? '-'}
        </div>
      </div>

      {notice && (
        <div className="rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 shadow-sm">
          {notice}
        </div>
      )}

      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-4">
        <TabsList className="bg-white">
          <TabsTrigger value="generator">Kisit & Uret</TabsTrigger>
          <TabsTrigger value="draft">Taslak & Domino</TabsTrigger>
          <TabsTrigger value="templates">Sablon & Tatil</TabsTrigger>
        </TabsList>
        <TabsContent value="generator">{renderGeneratorTab()}</TabsContent>
        <TabsContent value="draft">{renderDraftTab()}</TabsContent>
        <TabsContent value="templates">{renderTemplatesTab()}</TabsContent>
      </Tabs>
    </div>
  );
}
