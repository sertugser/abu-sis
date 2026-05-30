import { useMemo, useState } from 'react';
import { CalendarDays, Plus, Save, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { useAdminTerms } from '../../hooks/useAdminTerms';
import {
  useDeleteProgramOverride,
  useProgramLookup,
  useTermAttendancePolicy,
  useUpsertTermAttendancePolicy,
  type AttendanceMethodValue,
  type TermAttendancePolicyDto,
  type UpsertTermAttendancePolicyRequest,
} from '../../hooks/useAttendancePolicy';
import { getErrorMessage } from '../../lib/errorMessage';

const METHOD_OPTIONS: { value: AttendanceMethodValue; label: string }[] = [
  { value: 'Separate', label: 'Ayrı (teori/uygulama ayrı sınır)' },
  { value: 'TotalHours', label: 'Toplam (teori+uygulama tek sınır)' },
  { value: 'Passive', label: 'Kapalı (devamsızlıktan kalma yok)' },
  { value: 'MarkOnly', label: 'Sadece işaretle (kalmaya etki etmez)' },
];

export default function AttendancePolicyPanel() {
  const termsQuery = useAdminTerms();
  const [termId, setTermId] = useState<number | null>(null);

  const effectiveTermId = termId ?? termsQuery.data?.find(t => t.isActive)?.id ?? termsQuery.data?.[0]?.id ?? null;

  const policyQuery = useTermAttendancePolicy(effectiveTermId);
  const programsQuery = useProgramLookup();

  const generalPolicy = policyQuery.data?.policies.find(p => p.programId == null) ?? null;
  const overrides = policyQuery.data?.policies.filter(p => p.programId != null) ?? [];

  return (
    <div className="overflow-hidden rounded-[28px] border border-slate-200 bg-white shadow-[0_18px_40px_rgba(15,23,42,0.05)]">
      <div className="flex flex-col gap-3 border-b border-slate-200 px-6 py-5 xl:flex-row xl:items-center xl:justify-between">
        <div className="space-y-1">
          <h2 className="flex items-center gap-2 text-base font-semibold text-slate-900">
            <CalendarDays className="h-4 w-4 text-slate-500" />
            Dönem / Program Bazlı Devamsızlık Limitleri
          </h2>
          <p className="text-xs text-slate-500">
            Her dönem kendi limitini taşır (geçmiş dönem dondurulur). Program override girilmezse o dönemin geneli, o da yoksa
            aşağıdaki global varsayılan uygulanır.
          </p>
        </div>
        <div className="w-full xl:w-72">
          <Select
            value={effectiveTermId != null ? String(effectiveTermId) : ''}
            onValueChange={value => setTermId(Number(value))}
            disabled={termsQuery.isLoading}
          >
            <SelectTrigger>
              <SelectValue placeholder="Dönem seç" />
            </SelectTrigger>
            <SelectContent>
              {(termsQuery.data ?? []).map(term => (
                <SelectItem key={term.id} value={String(term.id)}>
                  {term.name}{term.isActive ? ' • aktif' : ''}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="space-y-5 px-6 py-5">
        {policyQuery.isLoading ? (
          <Skeleton className="h-40 rounded-xl" />
        ) : (
          <>
            {policyQuery.data && (
              <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-xs text-slate-600">
                <span className="font-semibold text-slate-700">Global varsayılan: </span>
                {policyQuery.data.globalDefault.termWeekCount} hafta · Teori %{policyQuery.data.globalDefault.theoryAbsenceLimitPercent}
                {' '}· Uygulama %{policyQuery.data.globalDefault.labAbsenceLimitPercent} · {methodLabel(policyQuery.data.globalDefault.method)}
                <span className="ml-1 text-slate-400">(yeni dönem açılınca kopyalanır)</span>
              </div>
            )}

            {/* Dönem geneli */}
            <PolicyEditor
              termId={effectiveTermId}
              title="Dönem Geneli (tüm programlar)"
              programId={null}
              initial={generalPolicy ?? (policyQuery.data ? policyQuery.data.globalDefault : null)}
            />

            {/* Program override'ları */}
            <div className="space-y-3">
              <h3 className="text-sm font-semibold text-slate-800">Program Override'ları</h3>
              {overrides.length === 0 && (
                <p className="text-xs text-slate-500">Bu dönemde program özel limiti yok. Aşağıdan ekleyebilirsiniz.</p>
              )}
              {overrides.map(override => (
                <PolicyEditor
                  key={override.programId}
                  termId={effectiveTermId}
                  title={override.programName ?? `Program #${override.programId}`}
                  programId={override.programId}
                  initial={override}
                  deletable
                />
              ))}

              <AddOverrideForm
                termId={effectiveTermId}
                fallback={generalPolicy ?? policyQuery.data?.globalDefault ?? null}
                existingProgramIds={overrides.map(o => o.programId!).filter((id): id is number => id != null)}
                programs={(programsQuery.data ?? []).map(p => ({ id: p.id, name: p.name }))}
              />
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function methodLabel(method: string) {
  return METHOD_OPTIONS.find(o => o.value === method)?.label ?? method;
}

interface PolicyEditorProps {
  termId: number | null;
  title: string;
  programId: number | null;
  initial: TermAttendancePolicyDto | null;
  deletable?: boolean;
}

function PolicyEditor({ termId, title, programId, initial, deletable }: PolicyEditorProps) {
  const upsert = useUpsertTermAttendancePolicy(termId);
  const remove = useDeleteProgramOverride(termId);
  const [error, setError] = useState<string | null>(null);

  const [form, setForm] = useState(() => toForm(initial));
  // initial referansı değişince (dönem değişimi) formu senkronla
  const initialKey = useMemo(
    () => `${termId}-${programId}-${initial?.termWeekCount}-${initial?.theoryAbsenceLimitPercent}-${initial?.labAbsenceLimitPercent}-${initial?.method}`,
    [termId, programId, initial],
  );
  const [prevKey, setPrevKey] = useState(initialKey);
  if (initialKey !== prevKey) {
    setPrevKey(initialKey);
    setForm(toForm(initial));
  }

  const handleSave = () => {
    setError(null);
    const req = toRequest(programId, form);
    if (req == null) {
      setError('Hafta 1-52, yüzdeler 0-100 aralığında olmalı.');
      return;
    }
    upsert.mutate(req, { onError: e => setError(getErrorMessage(e, 'Kaydedilemedi.')) });
  };

  return (
    <div className="rounded-xl border border-slate-200 px-4 py-3">
      <div className="mb-3 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-slate-800">{title}</span>
          {programId == null && <Badge variant="secondary" className="text-[10px]">varsayılan</Badge>}
        </div>
        {deletable && programId != null && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 gap-1 text-red-600 hover:bg-red-50 hover:text-red-700"
            onClick={() => remove.mutate(programId)}
            disabled={remove.isPending}
          >
            <Trash2 className="h-3.5 w-3.5" />
            Sil
          </Button>
        )}
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-[110px_110px_110px_minmax(0,1fr)_auto] lg:items-end">
        <Field label="Hafta">
          <Input type="number" min={1} max={52} value={form.weekCount}
            onChange={e => setForm(f => ({ ...f, weekCount: e.target.value }))} />
        </Field>
        <Field label="Teori %">
          <Input type="number" min={0} max={100} value={form.theory}
            onChange={e => setForm(f => ({ ...f, theory: e.target.value }))} />
        </Field>
        <Field label="Uygulama %">
          <Input type="number" min={0} max={100} value={form.lab}
            onChange={e => setForm(f => ({ ...f, lab: e.target.value }))} />
        </Field>
        <Field label="Yöntem">
          <Select value={form.method} onValueChange={v => setForm(f => ({ ...f, method: v as AttendanceMethodValue }))}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {METHOD_OPTIONS.map(o => <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>)}
            </SelectContent>
          </Select>
        </Field>
        <Button type="button" size="sm" className="gap-1" onClick={handleSave} disabled={upsert.isPending}>
          <Save className="h-3.5 w-3.5" />
          {upsert.isPending ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      </div>
      {error && <p className="mt-2 text-xs text-red-600">{error}</p>}
    </div>
  );
}

function AddOverrideForm({
  termId,
  fallback,
  existingProgramIds,
  programs,
}: {
  termId: number | null;
  fallback: TermAttendancePolicyDto | null;
  existingProgramIds: number[];
  programs: { id: number; name: string }[];
}) {
  const upsert = useUpsertTermAttendancePolicy(termId);
  const [programId, setProgramId] = useState<string>('');
  const [error, setError] = useState<string | null>(null);

  const available = programs.filter(p => !existingProgramIds.includes(p.id));

  const handleAdd = () => {
    setError(null);
    if (!programId) {
      setError('Program seçin.');
      return;
    }
    const base = fallback ?? { termWeekCount: 14, theoryAbsenceLimitPercent: 30, labAbsenceLimitPercent: 20, method: 'Separate' as AttendanceMethodValue };
    upsert.mutate(
      {
        programId: Number(programId),
        termWeekCount: base.termWeekCount,
        theoryAbsenceLimitPercent: base.theoryAbsenceLimitPercent,
        labAbsenceLimitPercent: base.labAbsenceLimitPercent,
        method: base.method,
      },
      { onSuccess: () => setProgramId(''), onError: e => setError(getErrorMessage(e, 'Eklenemedi.')) },
    );
  };

  return (
    <div className="flex flex-col gap-2 rounded-xl border border-dashed border-slate-300 px-4 py-3 sm:flex-row sm:items-end">
      <div className="flex-1 space-y-1">
        <span className="text-xs font-medium text-slate-600">Program override ekle</span>
        <Select value={programId} onValueChange={setProgramId} disabled={available.length === 0}>
          <SelectTrigger>
            <SelectValue placeholder={available.length === 0 ? 'Eklenecek program yok' : 'Program seç'} />
          </SelectTrigger>
          <SelectContent>
            {available.map(p => <SelectItem key={p.id} value={String(p.id)}>{p.name}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>
      <Button type="button" variant="outline" size="sm" className="gap-1" onClick={handleAdd} disabled={upsert.isPending || !programId}>
        <Plus className="h-3.5 w-3.5" />
        Ekle
      </Button>
      {error && <p className="text-xs text-red-600 sm:self-center">{error}</p>}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <span className="text-xs font-medium text-slate-600">{label}</span>
      {children}
    </div>
  );
}

interface PolicyForm {
  weekCount: string;
  theory: string;
  lab: string;
  method: AttendanceMethodValue;
}

function toForm(p: TermAttendancePolicyDto | null): PolicyForm {
  return {
    weekCount: String(p?.termWeekCount ?? 14),
    theory: String(p?.theoryAbsenceLimitPercent ?? 30),
    lab: String(p?.labAbsenceLimitPercent ?? 20),
    method: p?.method ?? 'Separate',
  };
}

function toRequest(programId: number | null, form: PolicyForm): UpsertTermAttendancePolicyRequest | null {
  const weekCount = Number(form.weekCount);
  const theory = Number(form.theory);
  const lab = Number(form.lab);
  if (!Number.isFinite(weekCount) || weekCount < 1 || weekCount > 52) return null;
  if (!Number.isFinite(theory) || theory < 0 || theory > 100) return null;
  if (!Number.isFinite(lab) || lab < 0 || lab > 100) return null;
  return {
    programId,
    termWeekCount: Math.trunc(weekCount),
    theoryAbsenceLimitPercent: theory,
    labAbsenceLimitPercent: lab,
    method: form.method,
  };
}
