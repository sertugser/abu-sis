import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Plus, Trash2, RefreshCw, AlertTriangle, Sparkles, Coffee, Sunset, Moon, Copy, Settings } from 'lucide-react';
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
  type TimeSlotDto,
  type TimeSlotAllowedMode,
  type DayOfWeekName,
} from '../hooks/useTimeSlots';
import {
  useScheduleSettings,
  findOverlappingSlot,
  parseTimeToMinutes,
  minutesToTime,
  normalizeTime,
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
  // Saat alanları boş açılır — kullanıcı kendi doldurur, dialog açılırken çakışma uyarısı tetiklenmez.
  return { dayOfWeek: 1, startTime: '', endTime: '', allowedMode: 'Both' };
}

// Takvim ekseni ölçeği — 1.4 px / dk → 50 dk slot ≈ 70px (tıklanabilir, okunabilir)
const PX_PER_MIN = 1.4;
// Üst/alt tampon — ilk saat etiketi (örn. 09:00) translateY(-50%) ile çerçeve dışına taşmasın diye.
const TIMELINE_TOP_PAD = 8;

function formatTime(time: string): string {
  // Backend sends TimeOnly as "HH:mm:ss" — strip seconds
  return time?.length >= 5 ? time.substring(0, 5) : time;
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

  // Takvim ekseni: tüm slotların min/max'inden hesapla, saat sınırlarına yuvarla.
  // Slot yoksa 09–18 default göster (boş durum).
  const { axisStartMin, axisEndMin, hourMarks } = useMemo(() => {
    if (slots.length === 0) {
      return {
        axisStartMin: 9 * 60,
        axisEndMin: 18 * 60,
        hourMarks: [9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
      };
    }
    let minStart = 24 * 60;
    let maxEnd = 0;
    for (const s of slots) {
      const start = parseTimeToMinutes(normalizeTime(s.startTime));
      const end = parseTimeToMinutes(normalizeTime(s.endTime));
      if (start < minStart) minStart = start;
      if (end > maxEnd) maxEnd = end;
    }
    const startHour = Math.floor(minStart / 60);
    const endHour = Math.ceil(maxEnd / 60);
    const marks: number[] = [];
    for (let h = startHour; h <= endHour; h++) marks.push(h);
    return {
      axisStartMin: startHour * 60,
      axisEndMin: endHour * 60,
      hourMarks: marks,
    };
  }, [slots]);

  const timelineHeight = (axisEndMin - axisStartMin) * PX_PER_MIN + TIMELINE_TOP_PAD * 2;

  // Öğle arası bandının pozisyon hesabı — sadece slot'u olan gün kolonlarında çizilecek.
  // null dönerse hiç çizilmez (config geçersiz veya eksen dışı).
  const lunchInfo = useMemo(() => {
    const startMin = parseTimeToMinutes(normalizeTime(settings.lunchBreakStart));
    const endMin = parseTimeToMinutes(normalizeTime(settings.lunchBreakEnd));
    if (!Number.isFinite(startMin) || !Number.isFinite(endMin)) return null;
    if (endMin <= startMin) return null;
    if (endMin <= axisStartMin || startMin >= axisEndMin) return null;
    const clampedStart = Math.max(startMin, axisStartMin);
    const clampedEnd = Math.min(endMin, axisEndMin);
    return {
      top: (clampedStart - axisStartMin) * PX_PER_MIN + TIMELINE_TOP_PAD,
      height: (clampedEnd - clampedStart) * PX_PER_MIN,
      label: `${normalizeTime(settings.lunchBreakStart)}–${normalizeTime(settings.lunchBreakEnd)}`,
    };
  }, [settings.lunchBreakStart, settings.lunchBreakEnd, axisStartMin, axisEndMin]);

  // Yalnızca Schedule.WorkDays config'inde tanımlı günleri göster — backend bu günler
  // dışına slot kabul etmez. Admin hafta sonu isterse Şablon Ayarları → Çalışma Günleri'nden
  // ekler, UI otomatik yansıtır. Böylece "ekran izin verdi ama kayıt reddetti" tuzağı kalkar.
  // Sıralama: Pzt, Sal, Çar, Per, Cuma, Cts, Paz. (System.DayOfWeek: Sun=0..Sat=6)
  const workDaySet = useMemo(() => new Set(settings.workDays), [settings.workDays]);
  const DAY_ORDER: DayOfWeekName[] = [1, 2, 3, 4, 5, 6, 0];
  const visibleDays = DAY_ORDER
    .filter(d => workDaySet.has(d))
    .map(d => ({
      value: d,
      label: DAY_OF_WEEK_OPTIONS.find(o => o.value === d)!.label,
    }));
  // Form dropdown'u ve kopyalama hedefleri de aynı filtreye tabi.
  const dayOptions = DAY_OF_WEEK_OPTIONS.filter(d => workDaySet.has(d.value));

  function openCreate(presetDay?: DayOfWeekName) {
    const firstWorkDay = visibleDays[0]?.value ?? 1;
    setForm({
      ...emptyForm(),
      dayOfWeek: presetDay ?? firstWorkDay,
    });
    setFormError('');
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

  /**
   * Süre ve öğle-arası kuralı — backend ValidateConfiguredRulesAsync ile birebir aynı.
   * Form geçerliyken kayıt-bloklayıcı uyarı döndürür; böylece kullanıcı kaydetmeden önce görür.
   */
  const liveRuleError = useMemo<string | null>(() => {
    if (settings.isLoading) return null;
    if (!form.startTime || !form.endTime || form.endTime <= form.startTime) return null;

    const duration = parseTimeToMinutes(form.endTime) - parseTimeToMinutes(form.startTime);
    if (duration !== settings.slotDurationMinutes) {
      return `Slot süresi sistem ayarı gereği tam ${settings.slotDurationMinutes} dakika olmalı (şu an ${duration} dk).`;
    }

    if (settings.lunchBreakIsHard) {
      const lunchStart = parseTimeToMinutes(normalizeTime(settings.lunchBreakStart));
      const lunchEnd = parseTimeToMinutes(normalizeTime(settings.lunchBreakEnd));
      const start = parseTimeToMinutes(form.startTime);
      const end = parseTimeToMinutes(form.endTime);
      if (start < lunchEnd && end > lunchStart) {
        return `Öğle arası (${normalizeTime(settings.lunchBreakStart)}–${normalizeTime(settings.lunchBreakEnd)}) içine slot tanımlanamaz.`;
      }
    }
    return null;
  }, [form, settings]);

  function validate(): string | null {
    if (!form.startTime || !form.endTime) return 'Başlangıç ve bitiş saati zorunludur.';
    if (form.endTime <= form.startTime) return 'Bitiş başlangıçtan sonra olmalı.';
    return liveRuleError;
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
        // "Kaydet ve Yeni Ekle" — aynı güne hızlı ekleme için zamanları temizle, gün kalsın.
        setForm(f => ({ ...f, startTime: '', endTime: '' }));
        setFormError('');
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

    // Var olan hedef slotlarla çakışmayanları kopyala (hata yerine atla — kullanıcı dostu).
    // Çakışma ile backend reddini AYRI sayıyoruz; aksi halde "ders günü değil" hatası
    // yanlışlıkla "çakışma" diye raporlanıyordu.
    let copiedCount = 0;
    let overlapSkipped = 0;
    const failureReasons: string[] = [];
    for (const src of sourceSlots) {
      const overlap = findOverlappingSlot(
        targetExisting.map(s => ({ ...s, dayOfWeek: targetDay })),
        targetDay,
        normalizeTime(src.startTime),
        normalizeTime(src.endTime),
        null
      );
      if (overlap) { overlapSkipped++; continue; }
      try {
        await createSlot.mutateAsync({
          termId: selectedTermId,
          dayOfWeek: targetDay,
          startTime: src.startTime.length === 5 ? `${src.startTime}:00` : src.startTime,
          endTime: src.endTime.length === 5 ? `${src.endTime}:00` : src.endTime,
          allowedMode: src.allowedMode,
        });
        copiedCount++;
      } catch (e: unknown) {
        const reason = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
          ?? 'bilinmeyen hata';
        failureReasons.push(reason);
      }
    }
    setCopyFromDay(null);

    // Sonucu gerçek nedenleriyle bildir — çakışma ve backend reddi ayrı satırlarda.
    const parts: string[] = [`${copiedCount} slot kopyalandı`];
    if (overlapSkipped > 0) parts.push(`${overlapSkipped} tanesi çakışma nedeniyle atlandı`);
    if (failureReasons.length > 0) {
      const uniqueReasons = [...new Set(failureReasons)].join('; ');
      parts.push(`${failureReasons.length} tanesi eklenemedi: ${uniqueReasons}`);
    }
    if (overlapSkipped > 0 || failureReasons.length > 0) {
      alert(parts.join('. ') + '.');
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
            title="Mevcut tüm ders saatlerini siler ve Şablon Ayarları'na göre baştan oluşturur."
          >
            <RefreshCw className={`h-3.5 w-3.5 mr-1.5 ${regenerateSlots.isPending ? 'animate-spin' : ''}`} />
            Yeniden Üret
          </Button>
          <Button
            size="sm"
            disabled={!isTermEditable}
            onClick={() => openCreate()}
            title="Bu döneme tek bir yeni ders saati ekle"
          >
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
        <div className="flex-1 flex flex-col bg-white border border-slate-200 rounded-xl overflow-hidden min-h-0">
          {/* Sticky gün başlıkları */}
          <div className="grid border-b border-slate-200 bg-slate-50 shrink-0"
               style={{ gridTemplateColumns: `60px repeat(${visibleDays.length}, minmax(0, 1fr))` }}>
            <div className="border-r border-slate-200" />
            {visibleDays.map(day => {
              const daySlots = slotsByDay.get(day.value) ?? [];
              // Mod dağılımı tooltipi: "5 her ikisi · 3 online · 1 yüz-yüze" (sıfırlar atlanır)
              const bothCount = daySlots.filter(s => s.allowedMode === 'Both').length;
              const onlineCount = daySlots.filter(s => s.allowedMode === 'OnlineOnly').length;
              const f2fCount = daySlots.filter(s => s.allowedMode === 'FaceToFaceOnly').length;
              const parts: string[] = [];
              if (bothCount > 0) parts.push(`${bothCount} her ikisi`);
              if (onlineCount > 0) parts.push(`${onlineCount} online`);
              if (f2fCount > 0) parts.push(`${f2fCount} yüz-yüze`);
              const tooltip = parts.length > 0 ? parts.join(' · ') : 'Slot yok';
              return (
                <div key={day.value} className="px-2.5 py-1.5 flex items-center justify-between border-r border-slate-200 last:border-r-0 group/col">
                  <span className="text-sm font-semibold text-slate-700 truncate">{day.label}</span>
                  <div className="flex items-center gap-1.5 shrink-0">
                    <span className="text-[11px] text-slate-500 tabular-nums" title={tooltip}>{daySlots.length} slot</span>
                    {isTermEditable && (
                      <>
                        <button
                          onClick={() => openCreate(day.value)}
                          className="h-6 w-6 inline-flex items-center justify-center rounded hover:bg-slate-200 text-slate-500 opacity-40 group-hover/col:opacity-100 transition-opacity"
                          title={`${day.label} gününe yeni slot ekle`}
                        >
                          <Plus className="h-3.5 w-3.5" />
                        </button>
                        {daySlots.length > 0 && (
                          <button
                            onClick={() => setCopyFromDay(day.value)}
                            className="h-6 w-6 inline-flex items-center justify-center rounded hover:bg-slate-200 text-slate-500 opacity-40 group-hover/col:opacity-100 transition-opacity"
                            title={`${day.label} gününün slotlarını başka güne kopyala`}
                          >
                            <Copy className="h-3.5 w-3.5" />
                          </button>
                        )}
                      </>
                    )}
                  </div>
                </div>
              );
            })}
          </div>

          {/* Scroll'lanabilir takvim ızgarası */}
          <div className="flex-1 overflow-auto">
            <div className="grid relative bg-white"
                 style={{
                   gridTemplateColumns: `60px repeat(${visibleDays.length}, minmax(0, 1fr))`,
                   height: `${timelineHeight}px`,
                 }}>
              {/* Saat ekseni (sol kolon) */}
              <div className="relative border-r border-slate-200 bg-slate-50/40">
                {hourMarks.map(h => (
                  <div
                    key={h}
                    className="absolute left-0 right-0 text-[11px] text-slate-500 tabular-nums px-1.5"
                    style={{ top: `${(h * 60 - axisStartMin) * PX_PER_MIN + TIMELINE_TOP_PAD}px`, transform: 'translateY(-50%)' }}
                  >
                    {String(h).padStart(2, '0')}:00
                  </div>
                ))}
              </div>

              {/* Gün kolonları (her biri saat ızgarası + slot blokları) */}
              {(() => {
                // Etiket sadece orta dolu günde — diğerlerinde sadece amber bant.
                const filledIndices = visibleDays
                  .map((d, i) => ((slotsByDay.get(d.value)?.length ?? 0) > 0 ? i : -1))
                  .filter(i => i >= 0);
                const labelDayIdx = filledIndices.length > 0
                  ? filledIndices[Math.floor(filledIndices.length / 2)]
                  : -1;
                return visibleDays.map((day, dayIdx) => {
                  const daySlots = slotsByDay.get(day.value) ?? [];
                  return (
                  <div key={day.value} className="relative border-r border-slate-200 last:border-r-0">
                    {/* Saat çizgileri */}
                    {hourMarks.map(h => (
                      <div
                        key={h}
                        className="absolute left-0 right-0 border-t border-slate-100"
                        style={{ top: `${(h * 60 - axisStartMin) * PX_PER_MIN + TIMELINE_TOP_PAD}px` }}
                      />
                    ))}
                    {/* Öğle arası bandı — sadece slot'u olan günlerde, çerçevesiz amber dolgu.
                        Etiket sadece ilk dolu günde görünür (tekrar gürültüsü olmasın). */}
                    {lunchInfo && daySlots.length > 0 && (
                      <div
                        className="absolute pointer-events-none bg-amber-50/70 left-0 right-0 flex items-center justify-center overflow-hidden"
                        style={{ top: `${lunchInfo.top}px`, height: `${lunchInfo.height}px` }}
                      >
                        {dayIdx === labelDayIdx && (
                          <span className="text-[11px] font-medium text-amber-800 tabular-nums px-2 py-0.5 rounded bg-white/90 border border-amber-200 shadow-sm whitespace-nowrap">
                            Öğle Arası · {lunchInfo.label}
                          </span>
                        )}
                      </div>
                    )}
                    {/* Boş gün mesajı */}
                    {daySlots.length === 0 && (
                      <div className="absolute inset-0 flex items-center justify-center text-xs text-slate-300 pointer-events-none">
                        Slot yok
                      </div>
                    )}
                    {/* Slot blokları */}
                    {daySlots.map(slot => (
                      <SlotBlock
                        key={slot.id}
                        slot={slot}
                        axisStartMin={axisStartMin}
                        editable={isTermEditable}
                        onEdit={() => openEdit(slot)}
                        onDelete={() => handleDeleteClick(slot)}
                      />
                    ))}
                  </div>
                );
                });
              })()}
            </div>
          </div>
        </div>
      )}

      {/* Create/Edit dialog */}
      <Dialog open={createOpen || !!editItem} onOpenChange={o => { if (!o) { setCreateOpen(false); setEditItem(null); } }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {editItem
                ? 'Slot Düzenle'
                : `${DAY_OF_WEEK_OPTIONS.find(d => d.value === form.dayOfWeek)?.label ?? ''} · Yeni Slot`}
            </DialogTitle>
          </DialogHeader>

          <div className="space-y-3">
            <div>
              <label className="text-sm font-medium block mb-1">Gün</label>
              <select
                className={SELECT_CLS}
                value={form.dayOfWeek}
                onChange={e => setForm({ ...form, dayOfWeek: Number(e.target.value) as DayOfWeekName })}
              >
                {dayOptions.map(d => <option key={d.value} value={d.value}>{d.label}</option>)}
              </select>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-sm font-medium block mb-1">Başlangıç</label>
                <Input type="time" value={form.startTime} onChange={e => setForm({ ...form, startTime: e.target.value })} />
              </div>
              <div>
                <label className="text-sm font-medium block mb-1">Bitiş</label>
                <Input type="time" value={form.endTime} onChange={e => setForm({ ...form, endTime: e.target.value })} />
              </div>
            </div>

            {/* Süre — sistem ayarı tek bir geçerli süre dayatır (Schedule.SlotDurationMinutes).
                Buton başlangıca göre bitişi o süreye ayarlar; başka süre backend tarafından reddedilir. */}
            <div>
              <label className="text-xs font-medium text-slate-500 block mb-1.5">Süre</label>
              <div className="flex items-center gap-2 flex-wrap">
                {(() => {
                  const currentDur = parseTimeToMinutes(form.endTime) - parseTimeToMinutes(form.startTime);
                  const active = currentDur === settings.slotDurationMinutes;
                  return (
                    <button
                      type="button"
                      onClick={() => applyDuration(settings.slotDurationMinutes)}
                      disabled={!form.startTime}
                      title="Başlangıç saatine göre bitişi sistem süresine ayarlar"
                      className={`px-2.5 py-1 text-xs rounded-md border transition inline-flex items-center gap-1 disabled:opacity-40 disabled:cursor-not-allowed ${
                        active
                          ? 'bg-slate-900 text-white border-slate-900'
                          : 'bg-white text-slate-700 border-slate-200 hover:border-slate-400'
                      }`}
                    >
                      {settings.slotDurationMinutes} dk uygula
                    </button>
                  );
                })()}
                <span className="text-[11px] text-slate-400">
                  Tüm slotlar tam {settings.slotDurationMinutes} dakika olmalı (sistem ayarı).
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
              <p className="text-xs text-muted-foreground mt-1">
                {form.allowedMode === 'OnlineOnly'
                  ? 'Bu slotta yalnızca online ders açılabilir (örn. 18:00 sonrası).'
                  : form.allowedMode === 'FaceToFaceOnly'
                  ? 'Bu slotta yalnızca yüz-yüze ders açılabilir.'
                  : 'Bu slotta hem yüz-yüze hem online ders açılabilir.'}
              </p>
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

            {/* Kural uyarısı — süre / öğle arası (backend ile birebir). Kayıt engellenir. */}
            {liveRuleError && (
              <div className="p-2.5 rounded-md bg-red-50 border border-red-200 text-xs text-red-800 flex items-start gap-2">
                <AlertTriangle className="h-3.5 w-3.5 mt-0.5 shrink-0" />
                <span>{liveRuleError}</span>
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
                variant="ghost"
                onClick={() => handleCreate(false)}
                disabled={createSlot.isPending || !!liveOverlap || !!liveRuleError}
                title="Bu slotu kaydet ve dialog açık kalsın — aynı güne hızlı ekleme"
                className="text-slate-600"
              >
                <Plus className="h-3.5 w-3.5 mr-1" />
                Kaydet ve Yeni Ekle
              </Button>
            )}
            <Button
              onClick={editItem ? handleUpdate : () => handleCreate(true)}
              disabled={createSlot.isPending || updateSlot.isPending || !!liveOverlap || !!liveRuleError}
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
              Bu dönemdeki <strong>tüm ders saatleri silinecek</strong> ve Şablon Ayarları'ndaki değerlere göre yeniden oluşturulacak.
              Elle eklediğin/değiştirdiğin her şey kaybolur.
            </p>
            {regenerateConfirm && regenerateConfirm.dependentCount > 0 && (
              <div className="p-3 rounded-lg bg-amber-50 border border-amber-200 text-sm text-amber-800">
                Dikkat: Bu dönemde <strong>{regenerateConfirm.dependentCount}</strong> ders programı ataması var.
                Onaylarsan bu atamalar da silinir; ders şubelerinin slot bağlantısı kopar.
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
              {dayOptions.filter(d => d.value !== copyFromDay).map(d => (
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

// ── Slot bloğu — takvim ekseninde absolute pozisyonlu, tıklayınca düzenle ─

interface SlotBlockProps {
  slot: TimeSlotDto;
  axisStartMin: number;
  editable: boolean;
  onEdit: () => void;
  onDelete: () => void;
}

function SlotBlock({ slot, axisStartMin, editable, onEdit, onDelete }: SlotBlockProps) {
  const startMin = parseTimeToMinutes(normalizeTime(slot.startTime));
  const endMin = parseTimeToMinutes(normalizeTime(slot.endTime));
  // Saat çizgileriyle hizalı kalmak için aynı üst tamponu uygula.
  const top = (startMin - axisStartMin) * PX_PER_MIN + TIMELINE_TOP_PAD;
  // Bitişikteki bloklar arasında 2px ayrım için yüksekliği biraz kırp; min 22px (kısa slotlar okunsun).
  const height = Math.max((endMin - startMin) * PX_PER_MIN - 2, 22);

  const colorClass =
    slot.allowedMode === 'OnlineOnly'    ? 'bg-sky-50 border-sky-200 hover:bg-sky-100'    :
    slot.allowedMode === 'FaceToFaceOnly' ? 'bg-amber-50 border-amber-200 hover:bg-amber-100' :
                                            'bg-slate-50/70 border-slate-200 hover:bg-slate-100';
  const accentClass =
    slot.allowedMode === 'OnlineOnly'    ? 'bg-sky-400'    :
    slot.allowedMode === 'FaceToFaceOnly' ? 'bg-amber-400'  :
                                            'bg-slate-300';

  return (
    <div
      className={`absolute left-1 right-1 z-10 rounded-md border ${colorClass} transition-colors group overflow-hidden ${editable ? 'cursor-pointer' : ''}`}
      style={{ top: `${top + 1}px`, height: `${height}px` }}
      onClick={editable ? onEdit : undefined}
      title={`${formatTime(slot.startTime)}–${formatTime(slot.endTime)} · ${modeShortLabel(slot.allowedMode)}`}
    >
      {/* Sol mod şeridi */}
      <div className={`absolute left-0 top-0 bottom-0 w-1 ${accentClass}`} />

      <div className="h-full w-full flex items-center justify-center gap-2 px-2">
        <span className="text-base font-semibold text-slate-900 tabular-nums">
          {formatTime(slot.startTime)}–{formatTime(slot.endTime)}
        </span>
        {slot.allowedMode !== 'Both' && (
          <span
            className={
              slot.allowedMode === 'OnlineOnly'
                ? 'text-[11px] font-medium uppercase tracking-wide text-sky-700'
                : 'text-[11px] font-medium uppercase tracking-wide text-amber-700'
            }
          >
            {slot.allowedMode === 'OnlineOnly' ? 'Online' : 'Yüz-Yüze'}
          </span>
        )}
      </div>
      {editable && (
        <button
          onClick={e => { e.stopPropagation(); onDelete(); }}
          className="absolute top-1.5 right-1.5 opacity-0 group-hover:opacity-100 h-6 w-6 inline-flex items-center justify-center rounded-full bg-white/80 backdrop-blur-sm text-slate-500 ring-1 ring-slate-200/80 shadow-sm hover:bg-red-500 hover:text-white hover:ring-red-500 hover:shadow-md hover:scale-110 transition-all duration-150 ease-out"
          title="Sil"
          aria-label="Slotu sil"
        >
          <Trash2 className="h-3 w-3" />
        </button>
      )}
    </div>
  );
}
