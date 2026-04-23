import { useMemo, useRef, useState } from 'react';
import { Info, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { PhaseCard } from '@/components/academic-calendar/PhaseCard';
import { CalendarEventsPanel } from '@/components/academic-calendar/CalendarEventsPanel';
import {
  useAdminCalendarTerms,
  useAdminAcademicCalendar,
  useUpsertAcademicCalendar,
  useUpdateCalendarPhaseOverride,
  useStudentCalendar,
  type UpsertAcademicCalendarDto
} from '@/hooks/useAcademicCalendar';

function toDateInput(iso: string) {
  return iso?.substring(0, 10) ?? '';
}
function toIso(d: string) {
  return d ? new Date(d).toISOString() : '';
}

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

  // Derive default form values from server data — no effect needed.
  const defaultForm = useMemo<UpsertAcademicCalendarDto | null>(() => {
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

  // Per-term local edits — keyed by termId so switching term resets edits.
  const [termEdits, setTermEdits] = useState<Record<number, Partial<UpsertAcademicCalendarDto>>>({});
  const form = defaultForm
    ? { ...defaultForm, ...(termId !== undefined ? (termEdits[termId] ?? {}) : {}) }
    : null;

  function updateField(field: keyof UpsertAcademicCalendarDto, value: string) {
    if (termId === undefined) return;
    setTermEdits(prev => ({
      ...prev,
      [termId]: { ...prev[termId], [field]: value },
    }));
  }

  // Clear stale edits when term changes (ref-based, no effect needed).
  const prevTermIdRef = useRef<number | undefined>(termId);
  if (prevTermIdRef.current !== termId) {
    prevTermIdRef.current = termId;
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
      alert('Tüm tarih alanları doldurulmalıdır.');
      return;
    }

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
    } catch {
      // Error displayed below the button via upsert.isError
    }
  }

  return (
    <div className="p-6 space-y-6 max-w-5xl mx-auto">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Akademik Takvim Yönetimi</h1>
        <p className="text-slate-600 mt-1">
          Seçilen dönem için kayıt, ekle/bırak, çekilme ve not giriş tarihlerini tanımlayın.
        </p>
      </div>

      <div className="flex items-end gap-4">
        <div className="flex-1 max-w-md">
          <Label>Dönem</Label>
          <Select value={termId?.toString() ?? ''} onValueChange={(v) => setExplicitTermId(Number(v))}>
            <SelectTrigger><SelectValue placeholder="Dönem seçin..." /></SelectTrigger>
            <SelectContent>
              {terms.data?.map(t => (
                <SelectItem key={t.termId} value={t.termId.toString()}>
                  {t.termName} {t.isCancelled && '(İptal)'} {t.hasCalendar ? '✓' : ''}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {selectedTerm?.isCancelled && (
          <Badge className="bg-red-100 text-red-700 border-red-300">Bu dönem iptal edilmiş</Badge>
        )}
      </div>

      {selectedTerm && !selectedTerm.hasCalendar && !selectedTerm.isCancelled && (
        <div className="rounded-lg border-2 border-blue-300 bg-blue-50 p-4 flex items-start gap-3">
          <Info className="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5" />
          <div>
            <h3 className="font-semibold text-blue-900">Bu dönemin akademik takvimi henüz oluşturulmamış</h3>
            <p className="text-sm text-blue-800 mt-1">
              Aşağıdaki formu doldurup kaydederek bu dönem için faz tarihlerini tanımlayın.
            </p>
          </div>
        </div>
      )}

      {form && termId && (
        <>
          <div className="rounded-xl border bg-slate-50 p-4 space-y-3">
            <h2 className="font-semibold text-slate-800">Tarihler</h2>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Kayıt Başlangıç</Label>
                <Input type="date" value={form.registrationStart} disabled={disabled}
                  onChange={e => updateField('registrationStart', e.target.value)} />
              </div>
              <div>
                <Label>Kayıt Bitiş</Label>
                <Input type="date" value={form.registrationEnd} disabled={disabled}
                  onChange={e => updateField('registrationEnd', e.target.value)} />
              </div>
              <div>
                <Label>Ekle/Bırak Başlangıç</Label>
                <Input type="date" value={form.addDropStart} disabled={disabled}
                  onChange={e => updateField('addDropStart', e.target.value)} />
              </div>
              <div>
                <Label>Ekle/Bırak Bitiş</Label>
                <Input type="date" value={form.addDropEnd} disabled={disabled}
                  onChange={e => updateField('addDropEnd', e.target.value)} />
              </div>
              <div>
                <Label>Çekilme Son Tarihi</Label>
                <Input type="date" value={form.withdrawalDeadline} disabled={disabled}
                  onChange={e => updateField('withdrawalDeadline', e.target.value)} />
              </div>
              <div />
              <div>
                <Label>Not Girişi Başlangıç</Label>
                <Input type="date" value={form.gradeEntryStart} disabled={disabled}
                  onChange={e => updateField('gradeEntryStart', e.target.value)} />
              </div>
              <div>
                <Label>Not Girişi Bitiş</Label>
                <Input type="date" value={form.gradeEntryEnd} disabled={disabled}
                  onChange={e => updateField('gradeEntryEnd', e.target.value)} />
              </div>
            </div>
            <Button onClick={handleSave} disabled={disabled || upsert.isPending}>
              <Save className="w-4 h-4 mr-2" />
              {upsert.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
            {upsert.isError && (
              <p className="text-red-600 text-sm">
                Hata: {(upsert.error as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Kaydetme başarısız'}
              </p>
            )}
          </div>

          {calendar.data && (
            <div className="space-y-3">
              <h2 className="font-semibold text-slate-800">Faz Durumları</h2>
              <div className="grid grid-cols-2 gap-3">
                <PhaseCard title="Kayıt Dönemi" phase="Registration"
                  start={calendar.data.registrationStart} end={calendar.data.registrationEnd}
                  override={calendar.data.registrationOverride}
                  isOpenComputed={calendar.data.isRegistrationOpen}
                  disabled={disabled}
                  onOverrideChange={(v) => override.mutate({ phase: 'Registration', value: v })} />
                <PhaseCard title="Ekle/Bırak" phase="AddDrop"
                  start={calendar.data.addDropStart} end={calendar.data.addDropEnd}
                  override={calendar.data.addDropOverride}
                  isOpenComputed={calendar.data.isAddDropOpen}
                  disabled={disabled}
                  onOverrideChange={(v) => override.mutate({ phase: 'AddDrop', value: v })} />
                <PhaseCard title="Çekilme" phase="Withdrawal"
                  start={calendar.data.termStart} end={calendar.data.withdrawalDeadline}
                  override={calendar.data.withdrawalOverride}
                  isOpenComputed={calendar.data.isWithdrawalOpen}
                  disabled={disabled}
                  onOverrideChange={(v) => override.mutate({ phase: 'Withdrawal', value: v })} />
                <PhaseCard title="Not Girişi" phase="GradeEntry"
                  start={calendar.data.gradeEntryStart} end={calendar.data.gradeEntryEnd}
                  override={calendar.data.gradeEntryOverride}
                  isOpenComputed={calendar.data.isGradeEntryOpen}
                  disabled={disabled}
                  onOverrideChange={(v) => override.mutate({ phase: 'GradeEntry', value: v })} />
              </div>
            </div>
          )}

          <div className="space-y-3">
            <h2 className="font-semibold text-slate-800">Takvim Etkinlikleri</h2>
            <CalendarEventsPanel events={events.data?.events ?? []} />
          </div>
        </>
      )}
    </div>
  );
}
