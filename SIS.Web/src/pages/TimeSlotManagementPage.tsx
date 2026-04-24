import { useState, useMemo, useEffect } from 'react';
import { Plus, Edit2, Trash2, RefreshCw, Clock, AlertTriangle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
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
} from '../hooks/useTimeSlots';
import { SELECT_CLS } from '../components/academic-structure/shared-constants';

interface SlotFormState {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  allowedMode: TimeSlotAllowedMode;
}

function emptyForm(): SlotFormState {
  return { dayOfWeek: 1, startTime: '09:00', endTime: '09:50', allowedMode: 'Both' };
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

  useEffect(() => {
    if (selectedTermId == null && terms.length > 0) {
      const active = terms.find(t => t.isActive) ?? terms[0];
      setSelectedTermId(active.id);
    }
  }, [terms, selectedTermId]);

  const { data: slots = [], isLoading } = useTimeSlots(selectedTermId);
  const createSlot = useCreateTimeSlot();
  const updateSlot = useUpdateTimeSlot();
  const deleteSlot = useDeleteTimeSlot();
  const regenerateSlots = useRegenerateTimeSlots();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<TimeSlotDto | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<{ slot: TimeSlotDto; dependentCount: number } | null>(null);
  const [regenerateConfirm, setRegenerateConfirm] = useState<{ dependentCount: number } | null>(null);
  const [form, setForm] = useState<SlotFormState>(emptyForm());
  const [formError, setFormError] = useState('');

  const selectedTerm = useMemo(
    () => terms.find(t => t.id === selectedTermId) ?? null,
    [terms, selectedTermId]
  );

  const isTermEditable = selectedTerm != null && !selectedTerm.isCancelled;

  // Grid: Day × Slot order
  const slotsByDay = useMemo(() => {
    const map = new Map<number, TimeSlotDto[]>();
    for (const slot of slots) {
      const arr = map.get(slot.dayOfWeek) ?? [];
      arr.push(slot);
      map.set(slot.dayOfWeek, arr);
    }
    for (const arr of map.values()) arr.sort((a, b) => a.startTime.localeCompare(b.startTime));
    return map;
  }, [slots]);

  const visibleDays = DAY_OF_WEEK_OPTIONS.filter(d =>
    (slotsByDay.get(d.value)?.length ?? 0) > 0 || d.value >= 1 && d.value <= 5
  );

  function openCreate() {
    setForm(emptyForm());
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

  function validate(): string | null {
    if (!form.startTime || !form.endTime) return 'Başlangıç ve bitiş saati zorunludur.';
    if (form.endTime <= form.startTime) return 'Bitiş başlangıçtan sonra olmalı.';
    return null;
  }

  async function handleCreate() {
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
      setCreateOpen(false);
    } catch (e: unknown) {
      setFormError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Slot oluşturulamadı.');
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
    <div className="p-6 max-w-7xl mx-auto">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900 flex items-center gap-2">
          <Clock className="h-6 w-6 text-sky-600" />
          Ders Saatleri Yönetimi
        </h1>
        <p className="text-slate-600 mt-1">
          Dönem bazlı TimeSlot grid'i — admin slot ekleyebilir, düzenleyebilir, silebilir veya config'ten yeniden üretebilir.
          Her slot için <strong>AllowedMode</strong> (Her İkisi / Online / Yüz-Yüze) belirlenir;
          otomatik programlama bu kurala göre yerleştirme yapar.
        </p>
      </div>

      {/* Term selector + actions */}
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <div className="flex items-center gap-2">
          <label className="text-sm font-medium text-slate-700">Dönem:</label>
          <select
            className={SELECT_CLS}
            value={selectedTermId ?? ''}
            onChange={e => setSelectedTermId(Number(e.target.value))}
          >
            {terms.map(t => (
              <option key={t.id} value={t.id}>
                {t.name} ({t.statusLabel})
              </option>
            ))}
          </select>
        </div>

        <div className="ml-auto flex items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            disabled={!isTermEditable || regenerateSlots.isPending}
            onClick={handleRegenerateClick}
          >
            <RefreshCw className={`h-4 w-4 mr-1 ${regenerateSlots.isPending ? 'animate-spin' : ''}`} />
            Slotları Yeniden Üret
          </Button>
          <Button size="sm" disabled={!isTermEditable} onClick={openCreate}>
            <Plus className="h-4 w-4 mr-1" /> Yeni Slot
          </Button>
        </div>
      </div>

      {!isTermEditable && selectedTerm?.isCancelled && (
        <div className="mb-4 p-3 rounded-lg bg-amber-50 border border-amber-200 text-sm text-amber-800 flex items-center gap-2">
          <AlertTriangle className="h-4 w-4" />
          Bu dönem iptal edilmiş — slot düzenlenemez.
        </div>
      )}

      {/* Day columns grid */}
      {isLoading ? (
        <div className="text-center py-12 text-muted-foreground">Yükleniyor...</div>
      ) : slots.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground border border-dashed border-border rounded-xl">
          Bu dönem için TimeSlot yok. "Slotları Yeniden Üret" ile config'ten otomatik oluşturabilirsin.
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-3">
          {visibleDays.map(day => {
            const daySlots = slotsByDay.get(day.value) ?? [];
            return (
              <div key={day.value} className="bg-white border border-border rounded-xl overflow-hidden">
                <div className="bg-muted/40 border-b border-border px-3 py-2 text-sm font-semibold text-foreground">
                  {day.label}
                  <span className="ml-2 text-xs text-muted-foreground">({daySlots.length} slot)</span>
                </div>
                <div className="divide-y divide-border">
                  {daySlots.length === 0 ? (
                    <div className="px-3 py-6 text-center text-xs text-muted-foreground">Slot yok</div>
                  ) : daySlots.map(slot => (
                    <div key={slot.id} className="px-3 py-2 hover:bg-muted/20 transition-colors">
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex-1 min-w-0">
                          <div className="text-sm font-medium text-foreground tabular-nums">
                            {formatTime(slot.startTime)}–{formatTime(slot.endTime)}
                          </div>
                          <div className="text-xs text-muted-foreground mt-0.5">
                            {slot.slotOrder}. Ders
                          </div>
                          <Badge variant="outline" className={`mt-1 text-[10px] ${modeBadgeClass(slot.allowedMode)}`}>
                            {modeShortLabel(slot.allowedMode)}
                          </Badge>
                        </div>
                        {isTermEditable && (
                          <div className="flex flex-col gap-0.5">
                            <Button size="sm" variant="ghost" className="h-6 w-6 p-0" onClick={() => openEdit(slot)}>
                              <Edit2 className="h-3 w-3" />
                            </Button>
                            <Button size="sm" variant="ghost" className="h-6 w-6 p-0" onClick={() => handleDeleteClick(slot)}>
                              <Trash2 className="h-3 w-3 text-destructive" />
                            </Button>
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Create/Edit dialog */}
      <Dialog open={createOpen || !!editItem} onOpenChange={o => { if (!o) { setCreateOpen(false); setEditItem(null); } }}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{editItem ? 'Slot Düzenle' : 'Yeni Slot'}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <label className="text-sm font-medium block mb-1">Gün</label>
              <select
                className={SELECT_CLS}
                value={form.dayOfWeek}
                onChange={e => setForm({ ...form, dayOfWeek: Number(e.target.value) })}
              >
                {DAY_OF_WEEK_OPTIONS.map(d => <option key={d.value} value={d.value}>{d.label}</option>)}
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
                {allowedModeLabel(form.allowedMode)} — otomatik programlama bu slotta sadece uyumlu dersleri yerleştirir.
              </p>
            </div>
          </div>
          {formError && <p className="text-sm text-destructive">{formError}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => { setCreateOpen(false); setEditItem(null); }}>İptal</Button>
            <Button
              onClick={editItem ? handleUpdate : handleCreate}
              disabled={createSlot.isPending || updateSlot.isPending}
            >
              {createSlot.isPending || updateSlot.isPending ? 'Kaydediliyor...' : 'Kaydet'}
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
    </div>
  );
}
