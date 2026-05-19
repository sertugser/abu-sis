import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Plus, Edit2, Trash2, RefreshCw, AlertTriangle, Sparkles, Coffee, Sunset, Moon, Copy, Settings } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { ROUTES } from '@/lib/constants';
import { useAdminTerms } from '../hooks/useAdminTerms';
import {
  useTimeSlots,
  useCreateTimeSlot,
  useUpdateTimeSlot,
  useDeleteTimeSlot,
  useRegenerateTimeSlots,
  ALLOWED_MODE_OPTIONS,
  DAY_OF_WEEK_OPTIONS,
  allowedModeLabel,
  type TimeSlotDto,
  type TimeSlotAllowedMode,
  type DayOfWeekName,
} from '../hooks/useTimeSlots';
import {
  useScheduleSettings,
  suggestNextSlot,
  findOverlappingSlot,
  parseTimeToMinutes,
  minutesToTime,
  normalizeTime,
  DURATION_PRESETS,
  type ScheduleSettings,
} from '../hooks/useScheduleSettings';
import { SELECT_CLS } from '../components/academic-structure/shared-constants';
import { TemplateSettingsDialog } from '../components/time-slot/TemplateSettingsDialog';

interface SlotFormState {
  dayOfWeek: DayOfWeekName;
  startTime: string;
  endTime: string;
  allowedMode: TimeSlotAllowedMode;
}

function emptyForm(): SlotFormState {
  return { dayOfWeek: 'Monday', startTime: '09:00', endTime: '09:50', allowedMode: 'Both' };
}

function formatTime(time: string): string {
  // Backend sends TimeOnly as "HH:mm:ss" — strip seconds
  return time?.length >= 5 ? time.substring(0, 5) : time;
}

function modeBadgeClass(mode: TimeSlotAllowedMode): string {
  if (mode === 'OnlineOnly') return 'bg-sky-100 text-sky-700 border-sky-200';
  if (mode === 'FaceToFaceOnly') return 'bg-amber-100 text-amber-700 border-amber-200';
  return 'bg-slate-100 text-slate-700 border-slate-200';
}

function modeShortLabel(mode: TimeSlotAllowedMode): string {
  if (mode === 'OnlineOnly') return 'Online';
  if (mode === 'FaceToFaceOnly') return 'Yüz-Yüze';
  return 'Her İkisi';
}

export default function TimeSlotManagementPage() {
  const { data: terms = [] } = useAdminTerms();
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);

  // Terms yüklenince ilk active term'i seç — adjust-state-during-render
  // (React 19+: useEffect içinde setState yasaklandı, render'da bir kez set)
  if (selectedTermId == null && terms.length > 0) {
    const active = terms.find(t => t.isActive) ?? terms[0];
    setSelectedTermId(active.id);
  }

  const { data: slots = [], isLoading } = useTimeSlots(selectedTermId);
  const settings = useScheduleSettings();
  const createSlot = useCreateTimeSlot();
  const updateSlot = useUpdateTimeSlot();
  const deleteSlot = useDeleteTimeSlot();
  const regenerateSlots = useRegenerateTimeSlots();

  const [createOpen, setCreateOpen] = useState(false);
  const [templateSettingsOpen, setTemplateSettingsOpen] = useState(false);
  const [editItem, setEditItem] = useState<TimeSlotDto | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<{ slot: TimeSlotDto; dependentCount: number } | null>(null);
  const [regenerateConfirm, setRegenerateConfirm] = useState<{ dependentCount: number } | null>(null);
  const [copyFromDay, setCopyFromDay] = useState<DayOfWeekName | null>(null);
  const [form, setForm] = useState<SlotFormState>(emptyForm());
  const [formError, setFormError] = useState('');
  const [suggestionMessage, setSuggestionMessage] = useState<string>('');

  const selectedTerm = useMemo(
    () => terms.find(t => t.id === selectedTermId) ?? null,
    [terms, selectedTermId]
  );

  const isTermEditable = selectedTerm != null && !selectedTerm.isCancelled;

  // Grid: Day × Slot order
  const slotsByDay = useMemo(() => {
    const map = new Map<DayOfWeekName, TimeSlotDto[]>();
    for (const slot of slots) {
      const arr = map.get(slot.dayOfWeek) ?? [];
      arr.push(slot);
      map.set(slot.dayOfWeek, arr);
    }
    for (const arr of map.values()) arr.sort((a, b) => a.startTime.localeCompare(b.startTime));
    return map;
  }, [slots]);

  // Tüm 7 günü sırayla göster — admin hafta sonu için de slot ekleyebilsin.
  // Sıralama: Pzt, Sal, Çar, Per, Cuma, Cts, Paz.
  const DAY_ORDER: DayOfWeekName[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  const allowedDaySet = useMemo(() => new Set(settings.workDays), [settings.workDays]);
  const visibleDays = DAY_ORDER.filter(d => allowedDaySet.has(d) || slotsByDay.has(d)).map(d => ({
    value: d,
    label: DAY_OF_WEEK_OPTIONS.find(o => o.value === d)!.label,
  }));

  const daySelectOptions = useMemo(() => {
    const editableDays = DAY_OF_WEEK_OPTIONS.filter(option => allowedDaySet.has(option.value));
    if (editableDays.length > 0) return editableDays;
    return DAY_OF_WEEK_OPTIONS.filter(option => DAY_ORDER.slice(0, 5).includes(option.value));
  }, [allowedDaySet]);

  function isAllowedDay(day: DayOfWeekName): boolean {
    return allowedDaySet.has(day);
  }

  /** Akıllı Öner: seçili gün için bir sonraki slotu hesapla, form'a doldur + reason mesajı göster */
  function applySuggestion(forDay?: DayOfWeekName, durationMinutes?: number) {
    const day = forDay ?? form.dayOfWeek;
    const daySlots = (slotsByDay.get(day) ?? []).map(s => ({
      startTime: normalizeTime(s.startTime),
      endTime: normalizeTime(s.endTime),
    }));
    const suggestion = suggestNextSlot(day, daySlots, settings, durationMinutes);
    const dayLabel = DAY_OF_WEEK_OPTIONS.find(o => o.value === day)?.label ?? day;

    setForm(f => ({
      ...f,
      dayOfWeek: day,
      startTime: suggestion.startTime,
      endTime: suggestion.endTime,
      allowedMode: suggestion.allowedMode,
    }));
    setSuggestionMessage(
      `${dayLabel} ${suggestion.startTime}–${suggestion.endTime} önerildi (${suggestion.reason}).`
    );
    setFormError('');
  }

  function openCreate(presetDay?: DayOfWeekName) {
    const defaultDay = presetDay && isAllowedDay(presetDay)
      ? presetDay
      : daySelectOptions[0]?.value ?? 'Monday';
    const startTime = settings.morningStartTime;
    const endTime = minutesToTime(parseTimeToMinutes(startTime) + settings.slotDurationMinutes);

    setForm({
      ...emptyForm(),
      dayOfWeek: defaultDay,
      startTime,
      endTime,
      allowedMode: computeAllowedModeFor(defaultDay, startTime, settings),
    });
    setFormError('');
    setSuggestionMessage('');
    setCreateOpen(true);
  }

  function openEdit(slot: TimeSlotDto) {
    setForm({
      dayOfWeek: slot.dayOfWeek,
      startTime: formatTime(slot.startTime),
      endTime: formatTime(slot.endTime),
      allowedMode: slot.allowedMode,
    });
    setFormError('');
    setEditItem(slot);
  }

  /** Süre preset'lerinden birine tıklanınca — başlangıç aynı, bitiş = start + duration */
  function applyDuration(durationMinutes: number) {
    if (!form.startTime) return;
    const startMin = parseTimeToMinutes(form.startTime);
    setForm(f => ({ ...f, endTime: minutesToTime(startMin + durationMinutes) }));
    setFormError('');
  }

  /** Form state'teki start/end ile aynı gün içinde çakışan slot var mı? Edit durumunda kendini hariç tut. */
  const liveOverlap = useMemo(() => {
    if (!form.startTime || !form.endTime || form.endTime <= form.startTime) return null;
    return findOverlappingSlot(
      slots,
      form.dayOfWeek,
      form.startTime,
      form.endTime,
      editItem?.id ?? null
    );
  }, [slots, form, editItem]);

  function validate(): string | null {
    if (!form.startTime || !form.endTime) return 'Başlangıç ve bitiş saati zorunludur.';
    if (form.endTime <= form.startTime) return 'Bitiş başlangıçtan sonra olmalı.';
    if (!isAllowedDay(form.dayOfWeek)) return 'Seçilen gün sistemde ders günü olarak tanımlı değil.';

    const duration = parseTimeToMinutes(form.endTime) - parseTimeToMinutes(form.startTime);
    if (duration !== settings.slotDurationMinutes)
      return `Slot süresi sistem ayarı gereği tam ${settings.slotDurationMinutes} dakika olmalıdır.`;

    if (
      settings.lunchBreakIsHard &&
      form.startTime < settings.lunchBreakEnd &&
      form.endTime > settings.lunchBreakStart
    ) {
      return `Öğle arası sistem ayarı gereği ${settings.lunchBreakStart}–${settings.lunchBreakEnd} aralığında slot tanımlanamaz.`;
    }

    return null;
  }

  /** Create — closeAfter=false ise dialog açık kalır ve form bir sonraki öneriye hazırlanır. */
  async function handleCreate(closeAfter: boolean = true) {
    if (selectedTermId == null) return;
    const err = validate(); if (err) { setFormError(err); return; }
    try {
      await createSlot.mutateAsync({
        termId: selectedTermId,
        dayOfWeek: form.dayOfWeek,
        startTime: `${form.startTime}:00`,
        endTime: `${form.endTime}:00`,
        allowedMode: form.allowedMode,
      });
      if (closeAfter) {
        setCreateOpen(false);
      } else {
        // "Kaydet ve Yeni Ekle" — aynı gün için bir sonraki öneriyi hazırla
        // Not: slots listesi React-Query invalidate'inden sonra güncellenir, bu satır
        // bir önceki (stale) listeden öneri üretir. Kullanıcı istediğinde yeniden "Öner" basar.
        applySuggestion(form.dayOfWeek);
      }
    } catch (e: unknown) {
      setFormError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Slot oluşturulamadı.');
    }
  }

  /** Bir günün tüm slotlarını başka bir güne kopyala */
  async function copyDayTo(targetDay: DayOfWeekName) {
    if (!copyFromDay || selectedTermId == null) return;
    const sourceSlots = slotsByDay.get(copyFromDay) ?? [];
    const targetExisting = slotsByDay.get(targetDay) ?? [];
    if (sourceSlots.length === 0) return;

    // Var olan hedef slotlarla çakışmayanları kopyala (hata yerine atla — kullanıcı dostu)
    let copiedCount = 0;
    let skippedCount = 0;
    for (const src of sourceSlots) {
      const overlap = findOverlappingSlot(
        targetExisting.map(s => ({ ...s, dayOfWeek: targetDay })),
        targetDay,
        normalizeTime(src.startTime),
        normalizeTime(src.endTime),
        null
      );
      if (overlap) { skippedCount++; continue; }
      try {
        await createSlot.mutateAsync({
          termId: selectedTermId,
          dayOfWeek: targetDay,
          startTime: src.startTime.length === 5 ? `${src.startTime}:00` : src.startTime,
          endTime: src.endTime.length === 5 ? `${src.endTime}:00` : src.endTime,
          allowedMode: src.allowedMode,
        });
        copiedCount++;
      } catch {
        skippedCount++;
      }
    }
    setCopyFromDay(null);
    // Küçük bir geri bildirim — alert yerine sessiz başarı. İstersen toast eklenir.
    if (skippedCount > 0) {
      alert(`${copiedCount} slot kopyalandı, ${skippedCount} atlandı (çakışma).`);
    }
  }

  async function handleUpdate() {
    if (!editItem) return;
    const err = validate(); if (err) { setFormError(err); return; }
    try {
      await updateSlot.mutateAsync({
        id: editItem.id,
        dto: {
          dayOfWeek: form.dayOfWeek,
          startTime: `${form.startTime}:00`,
          endTime: `${form.endTime}:00`,
          allowedMode: form.allowedMode,
        },
      });
      setEditItem(null);
    } catch (e: unknown) {
      setFormError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Slot güncellenemedi.');
    }
  }

  async function handleDeleteClick(slot: TimeSlotDto) {
    // First attempt without force — backend tells us dependent count if any
    const res = await deleteSlot.mutateAsync({ id: slot.id, force: false, termId: slot.termId });
    if (res.result.deleted) return; // successfully deleted, nothing to confirm
    setDeleteConfirm({ slot, dependentCount: res.result.dependentScheduleCount });
  }

  async function confirmDelete() {
    if (!deleteConfirm) return;
    await deleteSlot.mutateAsync({ id: deleteConfirm.slot.id, force: true, termId: deleteConfirm.slot.termId });
    setDeleteConfirm(null);
  }

  async function handleRegenerateClick() {
    if (selectedTermId == null) return;
    const res = await regenerateSlots.mutateAsync({ termId: selectedTermId, force: false });
    if (res.regenerated) return;
    setRegenerateConfirm({ dependentCount: res.dependentScheduleCount });
  }

  async function confirmRegenerate() {
    if (selectedTermId == null) return;
    await regenerateSlots.mutateAsync({ termId: selectedTermId, force: true });
    setRegenerateConfirm(null);
  }

  return (
    <div className="flex flex-col h-[calc(100vh-120px)]">
      {/* ── Toolbar — tek satır, tam genişlik ─────────────────────── */}
      <div className="flex items-center gap-3 shrink-0 bg-white border border-slate-200 rounded-xl px-4 py-2 mb-3">
        <label className="text-xs font-semibold uppercase tracking-wider text-slate-500 shrink-0">Dönem</label>
        <select
          className="h-8 rounded-md border border-slate-200 bg-white px-2.5 text-sm font-medium text-slate-700 min-w-[200px]"
          value={selectedTermId ?? ''}
          onChange={e => setSelectedTermId(Number(e.target.value))}
        >
          {terms.map(t => (
            <option key={t.id} value={t.id}>
              {t.name} ({t.statusLabel})
            </option>
          ))}
        </select>

        {slots.length > 0 && (
          <span className="text-xs text-slate-500 tabular-nums">
            Toplam <strong className="text-slate-700">{slots.length}</strong> slot · {visibleDays.length} gün
          </span>
        )}

        <div className="ml-auto flex items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={() => setTemplateSettingsOpen(true)}
          >
            <Settings className="h-3.5 w-3.5 mr-1.5" />
            Şablon Ayarları
          </Button>
          <Button
            size="sm"
            variant="outline"
            disabled={!isTermEditable || regenerateSlots.isPending}
            onClick={handleRegenerateClick}
          >
            <RefreshCw className={`h-3.5 w-3.5 mr-1.5 ${regenerateSlots.isPending ? 'animate-spin' : ''}`} />
            Yeniden Üret
          </Button>
          <Button size="sm" disabled={!isTermEditable} onClick={() => openCreate()}>
            <Plus className="h-3.5 w-3.5 mr-1" /> Yeni Slot
          </Button>
        </div>
      </div>

      <TemplateSettingsDialog open={templateSettingsOpen} onOpenChange={setTemplateSettingsOpen} />

      {!isTermEditable && selectedTerm?.isCancelled && (
        <div className="shrink-0 mb-3 p-2.5 rounded-lg bg-amber-50 border border-amber-200 text-xs text-amber-800 flex items-center gap-2">
          <AlertTriangle className="h-3.5 w-3.5" />
          Bu dönem iptal edilmiş — slot düzenlenemez.
        </div>
      )}

      {/* ── Grid — tam yükseklik, tam genişlik ───────────────────── */}
      {isLoading ? (
        <div className="flex-1 flex items-center justify-center text-sm text-muted-foreground">
          Yükleniyor...
        </div>
      ) : slots.length === 0 ? (
        <div className="flex-1 flex flex-col items-center justify-center border border-dashed border-slate-200 rounded-xl bg-slate-50/50 text-sm text-slate-500 gap-2">
          <p>Bu dönem için TimeSlot yok.</p>
          {isTermEditable && (
            <Button size="sm" variant="outline" onClick={handleRegenerateClick}>
              <RefreshCw className="h-3.5 w-3.5 mr-1.5" />
              Config'ten otomatik oluştur
            </Button>
          )}
        </div>
      ) : (
        <div className="flex-1 grid gap-2 overflow-auto"
             style={{ gridTemplateColumns: `repeat(${visibleDays.length}, minmax(0, 1fr))` }}>
          {visibleDays.map(day => {
            const daySlots = slotsByDay.get(day.value) ?? [];
            return (
              <div key={day.value} className="flex flex-col bg-white border border-slate-200 rounded-xl overflow-hidden min-h-0 group/col">
                <div className="bg-slate-50 border-b border-slate-200 px-2.5 py-1.5 flex items-center justify-between shrink-0 gap-1">
                  <span className="text-sm font-semibold text-slate-700 truncate">{day.label}</span>
                  <div className="flex items-center gap-1 shrink-0">
                    <span className="text-[11px] text-slate-400 tabular-nums">{daySlots.length}</span>
                    {isTermEditable && (
                      <>
                        {isAllowedDay(day.value) && (
                          <button
                            onClick={() => openCreate(day.value)}
                            className="h-5 w-5 inline-flex items-center justify-center rounded hover:bg-slate-200 text-slate-500 opacity-0 group-hover/col:opacity-100 transition"
                            title={`${day.label} gününe yeni slot ekle (akıllı öner)`}
                          >
                            <Plus className="h-3 w-3" />
                          </button>
                        )}
                        {daySlots.length > 0 && (
                          <button
                            onClick={() => setCopyFromDay(day.value)}
                            className="h-5 w-5 inline-flex items-center justify-center rounded hover:bg-slate-200 text-slate-500 opacity-0 group-hover/col:opacity-100 transition"
                            title={`${day.label} gününün slotlarını başka güne kopyala`}
                          >
                            <Copy className="h-3 w-3" />
                          </button>
                        )}
                      </>
                    )}
                  </div>
                </div>
                <div className="flex-1 overflow-auto divide-y divide-slate-100">
                  {daySlots.length === 0 ? (
                    <div className="px-3 py-8 text-center text-xs text-slate-400">Slot yok</div>
                  ) : daySlots.map(slot => (
                    <SlotRow
                      key={slot.id}
                      slot={slot}
                      editable={isTermEditable}
                      onEdit={() => openEdit(slot)}
                      onDelete={() => handleDeleteClick(slot)}
                    />
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Create/Edit dialog */}
      <Dialog open={createOpen || !!editItem} onOpenChange={o => { if (!o) { setCreateOpen(false); setEditItem(null); } }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle className="flex items-center justify-between pr-6">
              <span>{editItem ? 'Slot Düzenle' : 'Yeni Slot'}</span>
              {!editItem && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => applySuggestion()}
                  className="gap-1.5"
                  title="Seçili gün için bir sonraki slotu hesapla"
                >
                  <Sparkles className="h-3.5 w-3.5" /> Akıllı Öner
                </Button>
              )}
            </DialogTitle>
          </DialogHeader>

          <div className="space-y-3">
            {/* Öneri feedback mesajı — Akıllı Öner tıklandığında görünür */}
            {suggestionMessage && (
              <div className="rounded-md bg-sky-50 border border-sky-200 px-3 py-2 text-xs text-sky-800 flex items-start gap-2">
                <Sparkles className="h-3.5 w-3.5 mt-0.5 shrink-0 text-sky-500" />
                <span>{suggestionMessage}</span>
              </div>
            )}

            <div>
              <label className="text-sm font-medium block mb-1">Gün</label>
              <select
                className={SELECT_CLS}
                value={form.dayOfWeek}
                onChange={e => { setForm({ ...form, dayOfWeek: e.target.value as DayOfWeekName }); setSuggestionMessage(''); }}
              >
                {[
                  ...daySelectOptions,
                  ...(daySelectOptions.some(option => option.value === form.dayOfWeek)
                    ? []
                    : DAY_OF_WEEK_OPTIONS.filter(option => option.value === form.dayOfWeek)),
                ].map(d => <option key={d.value} value={d.value}>{d.label}</option>)}
              </select>
              <p className="text-xs text-muted-foreground mt-1">
                Ders günleri: {daySelectOptions.map(day => day.label).join(', ')}
              </p>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-sm font-medium block mb-1">Başlangıç</label>
                <Input type="time" value={form.startTime} onChange={e => { setForm({ ...form, startTime: e.target.value }); setSuggestionMessage(''); }} />
                <p className="text-xs text-muted-foreground mt-1">En erken başlangıç: {settings.morningStartTime}</p>
              </div>
              <div>
                <label className="text-sm font-medium block mb-1">Bitiş</label>
                <Input type="time" value={form.endTime} onChange={e => { setForm({ ...form, endTime: e.target.value }); setSuggestionMessage(''); }} />
                <p className="text-xs text-muted-foreground mt-1">Sabit süre: {settings.slotDurationMinutes} dk</p>
              </div>
            </div>

            {/* Süre preset'leri */}
            <div>
              <label className="text-xs font-medium text-slate-500 block mb-1.5">Süre</label>
              <div className="flex items-center gap-1.5 flex-wrap">
                {DURATION_PRESETS.map(p => {
                  const currentDur = parseTimeToMinutes(form.endTime) - parseTimeToMinutes(form.startTime);
                  const active = currentDur === p.minutes;
                  return (
                    <button
                      key={p.minutes}
                      type="button"
                      onClick={() => applyDuration(p.minutes)}
                      className={`px-2.5 py-1 text-xs rounded-md border transition ${
                        active
                          ? 'bg-slate-900 text-white border-slate-900'
                          : 'bg-white text-slate-700 border-slate-200 hover:border-slate-400'
                      }`}
                    >
                      {p.label}
                    </button>
                  );
                })}
                <span className="ml-auto text-[11px] text-slate-400 tabular-nums">
                  {Math.max(0, parseTimeToMinutes(form.endTime) - parseTimeToMinutes(form.startTime))} dk
                </span>
              </div>
            </div>

            <div>
              <label className="text-sm font-medium block mb-1">Slot Türü</label>
              <select
                className={SELECT_CLS}
                value={form.allowedMode}
                onChange={e => setForm({ ...form, allowedMode: e.target.value as TimeSlotAllowedMode })}
              >
                {ALLOWED_MODE_OPTIONS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select>
              <p className="text-xs text-muted-foreground mt-1">{allowedModeLabel(form.allowedMode)}</p>
            </div>

            {/* Canlı çakışma uyarısı */}
            {liveOverlap && (
              <div className="p-2.5 rounded-md bg-red-50 border border-red-200 text-xs text-red-800 flex items-start gap-2">
                <AlertTriangle className="h-3.5 w-3.5 mt-0.5 shrink-0" />
                <div>
                  <strong className="block">Çakışma var</strong>
                  <span>Aynı gün {normalizeTime(liveOverlap.startTime)}–{normalizeTime(liveOverlap.endTime)} aralığı mevcut (#{liveOverlap.slotOrder}).</span>
                </div>
              </div>
            )}

            {/* Config bilgi kartı — sadece yeni slot oluştururken gösterilir */}
            {!editItem && <ConfigInfoCard settings={settings} />}
          </div>

          {formError && <p className="text-sm text-destructive">{formError}</p>}
          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => { setCreateOpen(false); setEditItem(null); }}>
              İptal
            </Button>
            {!editItem && (
              <Button
                variant="outline"
                onClick={() => handleCreate(false)}
                disabled={createSlot.isPending}
                title="Bu slotu kaydet ve dialog açık kalsın — bir sonraki slot otomatik önerilir"
              >
                <Plus className="h-3.5 w-3.5 mr-1" />
                Kaydet ve Yeni Ekle
              </Button>
            )}
            <Button
              onClick={editItem ? handleUpdate : () => handleCreate(true)}
              disabled={createSlot.isPending || updateSlot.isPending}
            >
              {createSlot.isPending || updateSlot.isPending ? 'Kaydediliyor...' : 'Kaydet ve Kapat'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete warning dialog */}
      <Dialog open={!!deleteConfirm} onOpenChange={o => !o && setDeleteConfirm(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-amber-600" />
              Silme Uyarısı
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <p className="text-sm text-foreground">
              <strong>{deleteConfirm?.slot.displayName}</strong> silinecek.
            </p>
            {deleteConfirm && deleteConfirm.dependentCount > 0 && (
              <div className="p-3 rounded-lg bg-amber-50 border border-amber-200 text-sm text-amber-800">
                Bu slota bağlı <strong>{deleteConfirm.dependentCount}</strong> ders programı kaydı var.
                Onaylarsan bu atamalar da silinir; bağlı ders şubeleri atama siz olur.
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteConfirm(null)}>İptal</Button>
            <Button variant="destructive" onClick={confirmDelete} disabled={deleteSlot.isPending}>
              {deleteSlot.isPending ? 'Siliniyor...' : 'Evet, Sil'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Regenerate warning dialog */}
      <Dialog open={!!regenerateConfirm} onOpenChange={o => !o && setRegenerateConfirm(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-amber-600" />
              Yeniden Üretme Uyarısı
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <p className="text-sm text-foreground">
              Mevcut tüm slotlar silinip SystemConfig parametrelerinden yeniden üretilecek.
            </p>
            {regenerateConfirm && regenerateConfirm.dependentCount > 0 && (
              <div className="p-3 rounded-lg bg-amber-50 border border-amber-200 text-sm text-amber-800">
                Bu dönemde <strong>{regenerateConfirm.dependentCount}</strong> ders programı kaydı var.
                Onaylarsan bu atamalar da silinir.
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRegenerateConfirm(null)}>İptal</Button>
            <Button variant="destructive" onClick={confirmRegenerate} disabled={regenerateSlots.isPending}>
              {regenerateSlots.isPending ? 'Üretiliyor...' : 'Evet, Yeniden Üret'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Gün kopyalama dialog'u */}
      <Dialog open={!!copyFromDay} onOpenChange={o => !o && setCopyFromDay(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Copy className="h-4 w-4 text-slate-500" />
              Günü Kopyala
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <p className="text-sm text-slate-700">
              <strong>{copyFromDay && DAY_OF_WEEK_OPTIONS.find(d => d.value === copyFromDay)?.label}</strong> gününün
              {' '}{copyFromDay ? slotsByDay.get(copyFromDay)?.length ?? 0 : 0} slotunu hangi güne kopyalayalım?
            </p>
            <p className="text-[11px] text-slate-500">
              Hedef günde aynı saat varsa o slot atlanır, çakışma üretilmez.
            </p>
            <div className="grid grid-cols-2 gap-2">
              {daySelectOptions.filter(d => d.value !== copyFromDay).map(d => (
                <Button
                  key={d.value}
                  size="sm"
                  variant="outline"
                  onClick={() => copyDayTo(d.value)}
                  disabled={createSlot.isPending}
                  className="justify-start"
                >
                  {d.label}
                </Button>
              ))}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCopyFromDay(null)}>İptal</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

// ── Config Bilgi Kartı — dialog içinde öğle/cutoff/akşam gösterir ────

function ConfigInfoCard({ settings }: { settings: ScheduleSettings }) {
  if (settings.isLoading) return null;
  return (
    <div className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-[11px] text-slate-600">
      <div className="flex items-center justify-between mb-1.5">
        <span className="font-semibold text-slate-500 uppercase tracking-wider">Dönem Kuralları</span>
        <Link
          to={ROUTES.SYSTEM_CONFIG}
          className="inline-flex items-center gap-1 text-[10px] text-slate-500 hover:text-slate-900"
        >
          <Settings className="h-3 w-3" /> Ayarla
        </Link>
      </div>
      <div className="grid grid-cols-2 gap-x-3 gap-y-1">
        <InfoRow icon={<Sparkles className="h-3 w-3" />} label="Ders günleri" value={settings.workDays.map(dayLabelFor).join(', ')} />
        <InfoRow icon={<Coffee className="h-3 w-3" />} label="Öğle arası" value={`${settings.lunchBreakStart}–${settings.lunchBreakEnd}`} />
        <InfoRow icon={<Sunset className="h-3 w-3" />} label="Yüz-yüze cutoff" value={settings.faceToFaceCutoffTime} />
        <InfoRow icon={<Moon className="h-3 w-3" />} label="Akşam slotu" value={`${settings.eveningStartTime}+`} />
        <InfoRow icon={<Sparkles className="h-3 w-3" />} label="Varsayılan süre" value={`${settings.slotDurationMinutes} dk`} />
      </div>
    </div>
  );
}

function InfoRow({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className="flex items-center gap-1.5">
      <span className="text-slate-400">{icon}</span>
      <span className="text-slate-500">{label}:</span>
      <span className="font-medium text-slate-700 tabular-nums">{value}</span>
    </div>
  );
}

function dayLabelFor(day: DayOfWeekName): string {
  return DAY_OF_WEEK_OPTIONS.find(option => option.value === day)?.label ?? day;
}

function computeAllowedModeFor(
  day: DayOfWeekName,
  startTime: string,
  settings: ScheduleSettings,
): TimeSlotAllowedMode {
  if (parseTimeToMinutes(startTime) >= parseTimeToMinutes(settings.faceToFaceCutoffTime)) {
    return 'OnlineOnly';
  }

  if (settings.weekendOnlineOnly && (day === 'Saturday' || day === 'Sunday')) {
    return 'OnlineOnly';
  }

  return 'Both';
}

// ── Slot satırı — kompakt, hover'da aksiyon butonları ────────────────────

interface SlotRowProps {
  slot: TimeSlotDto;
  editable: boolean;
  onEdit: () => void;
  onDelete: () => void;
}

function SlotRow({ slot, editable, onEdit, onDelete }: SlotRowProps) {
  return (
    <div className="group px-2.5 py-1.5 hover:bg-slate-50/80 transition-colors">
      <div className="flex items-center gap-2">
        <div className="flex-1 min-w-0">
          <div className="flex items-baseline gap-1.5">
            <span className="text-sm font-semibold text-slate-800 tabular-nums">
              {formatTime(slot.startTime)}–{formatTime(slot.endTime)}
            </span>
            <span className="text-[11px] text-slate-400 tabular-nums">#{slot.slotOrder}</span>
          </div>
          <span className={`inline-flex items-center mt-0.5 px-1.5 py-0 rounded text-[10px] font-medium border ${modeBadgeClass(slot.allowedMode)}`}>
            {modeShortLabel(slot.allowedMode)}
          </span>
        </div>
        {editable && (
          <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
            <button
              onClick={onEdit}
              className="h-6 w-6 inline-flex items-center justify-center rounded hover:bg-slate-200 text-slate-600"
              title="Düzenle"
            >
              <Edit2 className="h-3 w-3" />
            </button>
            <button
              onClick={onDelete}
              className="h-6 w-6 inline-flex items-center justify-center rounded hover:bg-red-50 text-red-600"
              title="Sil"
            >
              <Trash2 className="h-3 w-3" />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
