import { useState } from 'react';
import { Plus, Edit2, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import {
  useOsymRegistrationCalendars,
  useCreateOsymRegistrationCalendar,
  useUpdateOsymRegistrationCalendar,
  useDeleteOsymRegistrationCalendar,
  OSYM_TYPE_OPTIONS,
  type OsymRegistrationCalendarDto,
  type CreateOsymRegistrationCalendarDto,
  type OsymRegistrationTypeValue,
} from '../hooks/useOsymRegistrationCalendar';
import { SELECT_CLS } from '../components/academic-structure/shared-constants';

interface FormState {
  year: number;
  type: OsymRegistrationTypeValue;
  name: string;
  startDate: string;
  endDate: string;
  description: string;
  isActive: boolean;
}

function emptyForm(): FormState {
  return {
    year: new Date().getFullYear(),
    type: 'YksPlacement',
    name: '',
    startDate: '',
    endDate: '',
    description: '',
    isActive: true,
  };
}

function toDateInput(iso: string) { return iso?.substring(0, 10) ?? ''; }
function toIso(d: string) { return d ? new Date(d).toISOString() : ''; }

export default function OsymRegistrationCalendarPage() {
  const { data: calendars = [], isLoading } = useOsymRegistrationCalendars();
  const createCal = useCreateOsymRegistrationCalendar();
  const updateCal = useUpdateOsymRegistrationCalendar();
  const deleteCal = useDeleteOsymRegistrationCalendar();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<OsymRegistrationCalendarDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<OsymRegistrationCalendarDto | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm());
  const [error, setError] = useState('');

  function openCreate() { setForm(emptyForm()); setError(''); setCreateOpen(true); }
  function openEdit(c: OsymRegistrationCalendarDto) {
    setForm({
      year: c.year,
      type: c.type,
      name: c.name,
      startDate: toDateInput(c.startDate),
      endDate: toDateInput(c.endDate),
      description: c.description ?? '',
      isActive: c.isActive,
    });
    setError('');
    setEditItem(c);
  }

  function validate(): string | null {
    if (!form.name.trim()) return 'Ad zorunludur.';
    if (!form.startDate || !form.endDate) return 'Başlangıç ve bitiş tarihleri zorunludur.';
    if (new Date(form.startDate) >= new Date(form.endDate)) return 'Başlangıç, bitişten önce olmalı.';
    return null;
  }

  async function handleCreate() {
    const err = validate(); if (err) { setError(err); return; }
    const dto: CreateOsymRegistrationCalendarDto = {
      year: form.year,
      type: form.type,
      name: form.name,
      startDate: toIso(form.startDate),
      endDate: toIso(form.endDate),
      description: form.description.trim() || null,
      isActive: form.isActive,
    };
    try { await createCal.mutateAsync(dto); setCreateOpen(false); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleUpdate() {
    if (!editItem) return;
    const err = validate(); if (err) { setError(err); return; }
    const dto = {
      id: editItem.id,
      year: form.year,
      type: form.type,
      name: form.name,
      startDate: toIso(form.startDate),
      endDate: toIso(form.endDate),
      description: form.description.trim() || null,
      isActive: form.isActive,
    };
    try { await updateCal.mutateAsync(dto); setEditItem(null); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleDelete() {
    if (!deleteItem) return;
    try { await deleteCal.mutateAsync(deleteItem.id); setDeleteItem(null); }
    catch (e: unknown) { alert((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.'); }
  }

  const fmt = (iso: string) => new Date(iso).toLocaleDateString('tr-TR');

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">ÖSYM Kayıt Takvimi</h1>
        <p className="text-slate-600 mt-1">YKS yerleştirme, yatay/dikey geçiş ve yabancı uyruklu öğrenci başvuru tarihleri.</p>
      </div>

      <div className="flex items-center justify-between mb-4">
        <p className="text-sm text-muted-foreground">{isLoading ? 'Yükleniyor...' : `${calendars.length} kayıt`}</p>
        <Button size="sm" onClick={openCreate}><Plus className="h-4 w-4 mr-1" /> Yeni Kayıt Takvimi</Button>
      </div>

      <div className="bg-white border border-border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Yıl</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Tip</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Başlangıç</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Bitiş</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading ? (
              <tr><td colSpan={7} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td></tr>
            ) : calendars.length === 0 ? (
              <tr><td colSpan={7} className="px-4 py-8 text-center text-muted-foreground">Henüz kayıt takvimi yok.</td></tr>
            ) : calendars.map(c => (
              <tr key={c.id} className="hover:bg-muted/20 transition-colors">
                <td className="px-4 py-3 text-muted-foreground">{c.year}</td>
                <td className="px-4 py-3 text-muted-foreground">{c.typeLabel}</td>
                <td className="px-4 py-3 font-medium text-foreground">{c.name}</td>
                <td className="px-4 py-3 text-muted-foreground">{fmt(c.startDate)}</td>
                <td className="px-4 py-3 text-muted-foreground">{fmt(c.endDate)}</td>
                <td className="px-4 py-3">
                  {c.isActive
                    ? <Badge className="bg-emerald-100 text-emerald-700">Aktif</Badge>
                    : <Badge className="bg-slate-100 text-slate-600">Pasif</Badge>}
                </td>
                <td className="px-4 py-3">
                  <div className="flex gap-1 justify-end">
                    <Button size="sm" variant="ghost" onClick={() => openEdit(c)}><Edit2 className="h-3.5 w-3.5" /></Button>
                    <Button size="sm" variant="ghost" onClick={() => setDeleteItem(c)}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Create/Edit modal — shared form */}
      <Dialog open={createOpen || !!editItem} onOpenChange={o => { if (!o) { setCreateOpen(false); setEditItem(null); } }}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>{editItem ? 'Kayıt Takvimi Düzenle' : 'Yeni Kayıt Takvimi'}</DialogTitle></DialogHeader>
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-sm font-medium block mb-1">Yıl</label>
                <Input type="number" min={2000} max={2100} value={form.year} onChange={e => setForm({ ...form, year: Number(e.target.value) })} />
              </div>
              <div>
                <label className="text-sm font-medium block mb-1">Tip</label>
                <select className={SELECT_CLS} value={form.type} onChange={e => setForm({ ...form, type: e.target.value as OsymRegistrationTypeValue })}>
                  {OSYM_TYPE_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
              </div>
            </div>
            <div>
              <label className="text-sm font-medium block mb-1">Ad</label>
              <Input value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="örn. 2026 YKS Yerleştirme" />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-sm font-medium block mb-1">Başlangıç</label>
                <Input type="date" value={form.startDate} onChange={e => setForm({ ...form, startDate: e.target.value })} />
              </div>
              <div>
                <label className="text-sm font-medium block mb-1">Bitiş</label>
                <Input type="date" value={form.endDate} onChange={e => setForm({ ...form, endDate: e.target.value })} />
              </div>
            </div>
            <div>
              <label className="text-sm font-medium block mb-1">Açıklama</label>
              <Input value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} placeholder="(opsiyonel)" />
            </div>
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={form.isActive} onChange={e => setForm({ ...form, isActive: e.target.checked })} />
              Aktif
            </label>
          </div>
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => { setCreateOpen(false); setEditItem(null); }}>İptal</Button>
            <Button
              onClick={editItem ? handleUpdate : handleCreate}
              disabled={createCal.isPending || updateCal.isPending}
            >
              {createCal.isPending || updateCal.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete confirm */}
      <Dialog open={!!deleteItem} onOpenChange={o => !o && setDeleteItem(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader><DialogTitle>Silme Onayı</DialogTitle></DialogHeader>
          <p className="text-sm text-muted-foreground">
            <strong className="text-foreground">{deleteItem?.name}</strong> silinecek.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteItem(null)}>İptal</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteCal.isPending}>
              {deleteCal.isPending ? 'Siliniyor...' : 'Sil'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
