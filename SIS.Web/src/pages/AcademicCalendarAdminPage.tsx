import { useMemo, useState } from 'react';
import { AlertTriangle, CalendarClock, CheckCircle2, Info, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { PhaseRow } from '@/components/academic-calendar/PhaseRow';
import { CalendarEventsPanel } from '@/components/academic-calendar/CalendarEventsPanel';
import {
  useAdminCalendarTerms,
  useAdminAcademicCalendar,
  useUpsertAcademicCalendar,
  useUpdateCalendarPhaseOverride,
  useStudentCalendar,
  type UpsertAcademicCalendarDto,
} from '@/hooks/useAcademicCalendar';

// ─── Helpers ─────────────────────────────────────────────────────

function toDateInput(iso: string) {
  return iso?.substring(0, 10) ?? '';
}
function toIso(d: string) {
  return d ? new Date(d).toISOString() : '';
}

// ─── Page ────────────────────────────────────────────────────────

export default function AcademicCalendarAdminPage() {
  const terms = useAdminCalendarTerms();
  const [explicitTermId, setExplicitTermId] = useState<number | undefined>(undefined);

  const defaultTermId = useMemo(() => {
    if (!terms.data || terms.data.length === 0) return undefined;
    const active = terms.data.find(t => t.isActive && !t.isCancelled);
    return active?.termId ?? terms.data[0].termId;
  }, [terms.data]);

  const termId = explicitTermId ?? defaultTermId;

  const calendar = useAdminAcademicCalendar(termId);
  const events = useStudentCalendar(termId);
  const upsert = useUpsertAcademicCalendar(termId ?? 0);
  const override = useUpdateCalendarPhaseOverride(termId ?? 0);

  const selectedTerm = useMemo(
    () => terms.data?.find(t => t.termId === termId),
    [terms.data, termId]
  );

  // ─── Form state — server veriyi baseline, kullanıcı düzenlemesi overlay ───

  const baseline = useMemo<UpsertAcademicCalendarDto | null>(() => {
    if (calendar.data) {
      return {
        registrationStart: toDateInput(calendar.data.registrationStart),
        registrationEnd: toDateInput(calendar.data.registrationEnd),
        addDropStart: toDateInput(calendar.data.addDropStart),
        addDropEnd: toDateInput(calendar.data.addDropEnd),
        withdrawalDeadline: toDateInput(calendar.data.withdrawalDeadline),
        gradeEntryStart: toDateInput(calendar.data.gradeEntryStart),
        gradeEntryEnd: toDateInput(calendar.data.gradeEntryEnd),
      };
    }
    if (calendar.isError) {
      return {
        registrationStart: '', registrationEnd: '',
        addDropStart: '', addDropEnd: '',
        withdrawalDeadline: '',
        gradeEntryStart: '', gradeEntryEnd: '',
      };
    }
    return null;
  }, [calendar.data, calendar.isError]);

  const [termEdits, setTermEdits] = useState<Record<number, Partial<UpsertAcademicCalendarDto>>>({});
  const [validationError, setValidationError] = useState<string>('');
  const [successMessage, setSuccessMessage] = useState<string>('');

  const edits = termId !== undefined ? (termEdits[termId] ?? {}) : {};
  const form = baseline ? { ...baseline, ...edits } : null;
  const hasPendingChanges = Object.keys(edits).length > 0;

  function updateField(field: keyof UpsertAcademicCalendarDto, value: string) {
    if (termId === undefined) return;
    setValidationError('');
    setSuccessMessage('');
    setTermEdits(prev => ({
      ...prev,
      [termId]: { ...prev[termId], [field]: value },
    }));
  }

  const disabled = selectedTerm?.isCancelled ?? false;

  async function handleSave() {
    if (!form || !termId) return;

    const required = [
      form.registrationStart, form.registrationEnd,
      form.addDropStart, form.addDropEnd,
      form.withdrawalDeadline,
      form.gradeEntryStart, form.gradeEntryEnd,
    ];
    if (required.some(d => !d)) {
      setValidationError('Tüm tarih alanları doldurulmalıdır.');
      return;
    }

    setValidationError('');
    try {
      await upsert.mutateAsync({
        registrationStart: toIso(form.registrationStart),
        registrationEnd: toIso(form.registrationEnd),
        addDropStart: toIso(form.addDropStart),
        addDropEnd: toIso(form.addDropEnd),
        withdrawalDeadline: toIso(form.withdrawalDeadline),
        gradeEntryStart: toIso(form.gradeEntryStart),
        gradeEntryEnd: toIso(form.gradeEntryEnd),
      });
      setTermEdits(prev => {
        const next = { ...prev };
        delete next[termId];
        return next;
      });
      setSuccessMessage('Akademik takvim kaydedildi.');
      setTimeout(() => setSuccessMessage(''), 3000);
    } catch {
      // Hata banner'da gösterilir.
    }
  }

  const saveErrorMessage = upsert.isError
    ? (upsert.error as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Kaydetme başarısız.'
    : null;

  const termStart = calendar.data?.termStart ?? '';

  return (
    <div className="space-y-5">
      {/* ── Toolbar ──────────────────────────────────────────── */}
      <TermToolbar
        termId={termId}
        terms={terms.data ?? []}
        isLoading={terms.isLoading}
        selectedTerm={selectedTerm}
        onChange={setExplicitTermId}
      />

      {/* ── Banner'lar ───────────────────────────────────────── */}
      {selectedTerm?.isCancelled && <CancelledBanner />}
      {selectedTerm && !selectedTerm.hasCalendar && !selectedTerm.isCancelled && <MissingCalendarBanner />}

      {/* ── Fazlar ───────────────────────────────────────────── */}
      {form && termId && calendar.data && (
        <div className="bg-card border border-border rounded-xl overflow-hidden">
          <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted/40">
            <div className="flex items-center gap-2">
              <CalendarClock className="w-4 h-4 text-muted-foreground" />
              <h2 className="text-base font-semibold text-foreground">Fazlar ve Tarihleri</h2>
            </div>
            <p className="text-xs text-muted-foreground">
              Tarihler tüm fazlar için kronolojik olmalıdır. Override anlık uygulanır.
            </p>
          </div>

          <div className="p-4 space-y-3">
            <PhaseRow
              title="Kayıt Dönemi"
              phase="Registration"
              startDate={form.registrationStart}
              endDate={form.registrationEnd}
              onStartChange={v => updateField('registrationStart', v)}
              onEndChange={v => updateField('registrationEnd', v)}
              isOpenComputed={calendar.data.isRegistrationOpen}
              override={calendar.data.registrationOverride}
              onOverrideChange={v => override.mutate({ phase: 'Registration', value: v })}
              disabled={disabled}
            />
            <PhaseRow
              title="Ekle / Bırak"
              phase="AddDrop"
              startDate={form.addDropStart}
              endDate={form.addDropEnd}
              onStartChange={v => updateField('addDropStart', v)}
              onEndChange={v => updateField('addDropEnd', v)}
              isOpenComputed={calendar.data.isAddDropOpen}
              override={calendar.data.addDropOverride}
              onOverrideChange={v => override.mutate({ phase: 'AddDrop', value: v })}
              disabled={disabled}
            />
            <PhaseRow
              title="Çekilme"
              phase="Withdrawal"
              startDate={toDateInput(termStart)}
              endDate={form.withdrawalDeadline}
              onEndChange={v => updateField('withdrawalDeadline', v)}
              startLabel="Dönem Başlangıcı"
              endLabel="Son Tarih"
              startReadonly
              startHint="Otomatik · dönem tarihinden"
              isOpenComputed={calendar.data.isWithdrawalOpen}
              override={calendar.data.withdrawalOverride}
              onOverrideChange={v => override.mutate({ phase: 'Withdrawal', value: v })}
              disabled={disabled}
            />
            <PhaseRow
              title="Not Girişi"
              phase="GradeEntry"
              startDate={form.gradeEntryStart}
              endDate={form.gradeEntryEnd}
              onStartChange={v => updateField('gradeEntryStart', v)}
              onEndChange={v => updateField('gradeEntryEnd', v)}
              isOpenComputed={calendar.data.isGradeEntryOpen}
              override={calendar.data.gradeEntryOverride}
              onOverrideChange={v => override.mutate({ phase: 'GradeEntry', value: v })}
              disabled={disabled}
            />
          </div>

          {/* Footer: feedback + save */}
          <div className="px-4 py-3 border-t border-border bg-muted/10 flex items-center justify-between gap-4 flex-wrap">
            <FormFeedback
              validationError={validationError}
              saveError={saveErrorMessage}
              successMessage={successMessage}
              hasPendingChanges={hasPendingChanges && !disabled}
            />
            <Button onClick={handleSave} disabled={disabled || upsert.isPending || !hasPendingChanges}>
              <Save className="w-4 h-4 mr-1.5" />
              {upsert.isPending ? 'Kaydediliyor...' : 'Tarihleri Kaydet'}
            </Button>
          </div>
        </div>
      )}

      {/* ── Form ilk oluşturma modu (takvim henüz yok) ─────── */}
      {form && termId && !calendar.data && (
        <div className="bg-card border border-border rounded-xl overflow-hidden">
          <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted/40">
            <div className="flex items-center gap-2">
              <CalendarClock className="w-4 h-4 text-muted-foreground" />
              <h2 className="text-base font-semibold text-foreground">Faz Tarihleri</h2>
            </div>
            <p className="text-xs text-muted-foreground">İlk kaydetmeden sonra override seçenekleri aktif olur.</p>
          </div>
          <div className="p-4 space-y-3">
            <InitialPhaseRow
              title="Kayıt Dönemi"
              startDate={form.registrationStart}
              endDate={form.registrationEnd}
              onStartChange={v => updateField('registrationStart', v)}
              onEndChange={v => updateField('registrationEnd', v)}
              disabled={disabled}
            />
            <InitialPhaseRow
              title="Ekle / Bırak"
              startDate={form.addDropStart}
              endDate={form.addDropEnd}
              onStartChange={v => updateField('addDropStart', v)}
              onEndChange={v => updateField('addDropEnd', v)}
              disabled={disabled}
            />
            <InitialPhaseRow
              title="Çekilme"
              startDate={toDateInput(termStart)}
              endDate={form.withdrawalDeadline}
              onEndChange={v => updateField('withdrawalDeadline', v)}
              startLabel="Dönem Başlangıcı"
              endLabel="Son Tarih"
              startReadonly
              startHint="Otomatik · dönem tarihinden"
              disabled={disabled}
            />
            <InitialPhaseRow
              title="Not Girişi"
              startDate={form.gradeEntryStart}
              endDate={form.gradeEntryEnd}
              onStartChange={v => updateField('gradeEntryStart', v)}
              onEndChange={v => updateField('gradeEntryEnd', v)}
              disabled={disabled}
            />
          </div>
          <div className="px-4 py-3 border-t border-border bg-muted/10 flex items-center justify-between gap-4 flex-wrap">
            <FormFeedback
              validationError={validationError}
              saveError={saveErrorMessage}
              successMessage={successMessage}
              hasPendingChanges={hasPendingChanges && !disabled}
            />
            <Button onClick={handleSave} disabled={disabled || upsert.isPending || !hasPendingChanges}>
              <Save className="w-4 h-4 mr-1.5" />
              {upsert.isPending ? 'Kaydediliyor...' : 'Takvimi Oluştur'}
            </Button>
          </div>
        </div>
      )}

      {/* ── Takvim Etkinlikleri ──────────────────────────────── */}
      {termId && (
        <div className="bg-card border border-border rounded-xl overflow-hidden">
          <div className="px-4 py-3 border-b border-border bg-muted/40">
            <h2 className="text-base font-semibold text-foreground">Takvim Etkinlikleri</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Bu döneme tanımlı resmi tatiller, sınav haftaları ve diğer akademik etkinlikler.
            </p>
          </div>
          <div className="p-4">
            <CalendarEventsPanel events={events.data?.events ?? []} />
          </div>
        </div>
      )}

      {/* ── Hiç dönem yok ────────────────────────────────────── */}
      {!termId && !terms.isLoading && (
        <div className="bg-card border border-border rounded-xl p-8 text-center text-muted-foreground text-sm">
          Henüz dönem tanımlanmamış. Akademik takvim düzenlemek için önce bir dönem oluşturun.
        </div>
      )}
    </div>
  );
}

// ─── Toolbar ─────────────────────────────────────────────────────

interface TermToolbarProps {
  termId: number | undefined;
  terms: Array<{ termId: number; termName: string; hasCalendar: boolean; isCancelled: boolean; isActive: boolean }>;
  isLoading: boolean;
  selectedTerm: { hasCalendar: boolean; isCancelled: boolean; isActive: boolean } | undefined;
  onChange: (id: number) => void;
}

function TermToolbar({ termId, terms, isLoading, selectedTerm, onChange }: TermToolbarProps) {
  return (
    <div className="bg-card border border-border rounded-xl px-4 py-3">
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div className="flex items-end gap-4 flex-wrap min-w-0 flex-1">
          <div className="flex-1 min-w-[260px] max-w-md space-y-1">
            <label className="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">
              Dönem
            </label>
            <Select value={termId?.toString() ?? ''} onValueChange={(v) => onChange(Number(v))}>
              <SelectTrigger className="h-9">
                <SelectValue placeholder={isLoading ? 'Yükleniyor...' : 'Dönem seçin...'} />
              </SelectTrigger>
              <SelectContent>
                {terms.map(t => (
                  <SelectItem key={t.termId} value={t.termId.toString()}>
                    <span className="flex items-center gap-2">
                      <span>{t.termName}</span>
                      {t.isActive && !t.isCancelled && (
                        <span className="text-[10px] text-emerald-600 font-medium">● Aktif</span>
                      )}
                      {t.isCancelled && (
                        <span className="text-[10px] text-red-600 font-medium">● İptal</span>
                      )}
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {selectedTerm && (
            <div className="flex items-center gap-2 flex-wrap pb-1">
              {selectedTerm.isCancelled ? (
                <Badge className="bg-red-100 text-red-700 border-red-300">İptal edilmiş</Badge>
              ) : selectedTerm.hasCalendar ? (
                <Badge className="bg-emerald-100 text-emerald-700 border-emerald-300">Takvim tanımlı</Badge>
              ) : (
                <Badge className="bg-amber-100 text-amber-700 border-amber-300">Takvim eksik</Badge>
              )}
              {selectedTerm.isActive && !selectedTerm.isCancelled && (
                <Badge variant="outline" className="border-emerald-300 text-emerald-700">Aktif dönem</Badge>
              )}
            </div>
          )}
        </div>

        <div className="text-[11px] text-muted-foreground text-right">
          <p className="font-medium">Toplam {terms.length} dönem</p>
          <p>Faz override'ları anlık uygulanır</p>
        </div>
      </div>
    </div>
  );
}

// ─── Banners ─────────────────────────────────────────────────────

function CancelledBanner() {
  return (
    <div className="rounded-xl border border-red-300 bg-red-50 p-4 flex items-start gap-3">
      <AlertTriangle className="w-5 h-5 text-red-700 flex-shrink-0 mt-0.5" />
      <div className="min-w-0">
        <h3 className="font-semibold text-red-900">Bu dönem iptal edilmiş</h3>
        <p className="text-sm text-red-800 mt-0.5">
          İptal edilmiş dönemlerde akademik takvim ve faz override'ları düzenlenemez. İptali geri almak için
          <strong className="font-semibold"> Akademik Dönem Tanımları</strong> sayfasını kullanın.
        </p>
      </div>
    </div>
  );
}

function MissingCalendarBanner() {
  return (
    <div className="rounded-xl border border-amber-200 bg-amber-50/70 p-4 flex items-start gap-3">
      <Info className="w-5 h-5 text-amber-700 flex-shrink-0 mt-0.5" />
      <div className="min-w-0">
        <h3 className="font-semibold text-amber-900">Bu dönemin akademik takvimi henüz oluşturulmamış</h3>
        <p className="text-sm text-amber-800 mt-0.5">
          Tarih alanlarını doldurup <strong className="font-semibold">Takvimi Oluştur</strong> ile kaydedin.
          Takvim tanımlanana kadar öğrenciler için kayıt, ekle/bırak, çekilme ve not girişi fazları kapalıdır.
        </p>
      </div>
    </div>
  );
}

// ─── Form feedback ───────────────────────────────────────────────

function FormFeedback({
  validationError,
  saveError,
  successMessage,
  hasPendingChanges,
}: {
  validationError: string;
  saveError: string | null;
  successMessage: string;
  hasPendingChanges: boolean;
}) {
  if (validationError) {
    return <span className="text-sm text-destructive inline-flex items-center gap-1.5">
      <AlertTriangle className="w-4 h-4" />
      {validationError}
    </span>;
  }
  if (saveError) {
    return <span className="text-sm text-destructive inline-flex items-center gap-1.5">
      <AlertTriangle className="w-4 h-4" />
      {saveError}
    </span>;
  }
  if (successMessage) {
    return <span className="text-sm font-medium text-success inline-flex items-center gap-1.5">
      <CheckCircle2 className="w-4 h-4" />
      {successMessage}
    </span>;
  }
  if (hasPendingChanges) {
    return <span className="text-sm text-muted-foreground inline-flex items-center gap-1.5">
      <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
      Kaydedilmemiş değişiklikler
    </span>;
  }
  return <span className="text-xs text-muted-foreground italic">Değişiklik yok</span>;
}

// ─── İlk oluşturma için sade satır (override yok) ────────────────

function InitialPhaseRow({
  title,
  startDate,
  endDate,
  onStartChange,
  onEndChange,
  startLabel = 'Başlangıç',
  endLabel = 'Bitiş',
  startReadonly = false,
  startHint,
  disabled = false,
}: {
  title: string;
  startDate: string;
  endDate: string;
  onStartChange?: (value: string) => void;
  onEndChange: (value: string) => void;
  startLabel?: string;
  endLabel?: string;
  startReadonly?: boolean;
  startHint?: string;
  disabled?: boolean;
}) {
  return (
    <div className="rounded-lg border border-border bg-card">
      <div className="px-4 py-2.5 border-b border-border bg-muted/30">
        <h3 className="text-sm font-semibold text-foreground">{title}</h3>
      </div>
      <div className="p-4 grid grid-cols-1 md:grid-cols-2 gap-3 md:items-end">
        <div className="space-y-1">
          <label className="text-xs font-medium text-muted-foreground">{startLabel}</label>
          <input
            type="date"
            value={startDate}
            readOnly={startReadonly}
            disabled={disabled || startReadonly}
            onChange={e => onStartChange?.(e.target.value)}
            className={`w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring ${startReadonly ? 'cursor-default opacity-75' : ''}`}
          />
          {startHint && <p className="text-[11px] text-muted-foreground leading-tight">{startHint}</p>}
        </div>
        <div className="space-y-1">
          <label className="text-xs font-medium text-muted-foreground">{endLabel}</label>
          <input
            type="date"
            value={endDate}
            disabled={disabled}
            onChange={e => onEndChange(e.target.value)}
            className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
      </div>
    </div>
  );
}
