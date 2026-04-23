import { useState, useMemo } from 'react';
import { Plus, Edit2, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
  type ProgramDto,
} from '../hooks/useAdminAcademicStructure';
import {
  useProgramQuotas,
  useCreateProgramQuota,
  useUpdateProgramQuota,
  useDeleteProgramQuota,
  QUOTA_TYPE_OPTIONS,
  type ProgramQuotaDto,
  type QuotaTypeValue,
} from '../hooks/useProgramQuota';
import { SELECT_CLS } from '../components/academic-structure/shared-constants';

interface FormState {
  programId: number;
  year: number;
  type: QuotaTypeValue;
  quantity: number;
  description: string;
}

function emptyForm(programId = 0): FormState {
  return { programId, year: new Date().getFullYear(), type: 'Yks', quantity: 0, description: '' };
}

export default function ProgramQuotaPage() {
  const { data: faculties = [] } = useAdminFaculties();
  const [facultyFilter, setFacultyFilter] = useState<number | undefined>(undefined);
  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(undefined);
  const [departmentFilter, setDepartmentFilter] = useState<number | undefined>(undefined);
  const { data: allPrograms = [] } = useAdminProgramsByStructure(undefined);
  const [programFilter, setProgramFilter] = useState<number | undefined>(undefined);
  const [yearFilter, setYearFilter] = useState<number | undefined>(undefined);

  const { data: quotas = [], isLoading } = useProgramQuotas({ programId: programFilter, year: yearFilter });
  const createQuota = useCreateProgramQuota();
  const updateQuota = useUpdateProgramQuota();
  const deleteQuota = useDeleteProgramQuota();

  const visibleDepartments = useMemo(() => {
    if (!facultyFilter) return allDepartments;
    return allDepartments.filter(d => d.facultyId === facultyFilter);
  }, [allDepartments, facultyFilter]);

  const visiblePrograms = useMemo((): ProgramDto[] => {
    let result: ProgramDto[] = allPrograms;
    if (departmentFilter) result = result.filter(p => p.departmentId === departmentFilter);
    else if (facultyFilter) {
      const deptIds = new Set(visibleDepartments.map(d => d.id));
      result = result.filter(p => deptIds.has(p.departmentId));
    }
    return result;
  }, [allPrograms, departmentFilter, facultyFilter, visibleDepartments]);

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<ProgramQuotaDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<ProgramQuotaDto | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm());
  const [error, setError] = useState('');

  function openCreate() {
    setForm(emptyForm(programFilter ?? visiblePrograms[0]?.id ?? 0));
    setError('');
    setCreateOpen(true);
  }
  function openEdit(q: ProgramQuotaDto) {
    setForm({
      programId: q.programId,
      year: q.year,
      type: q.type,
      quantity: q.quantity,
      description: q.description ?? '',
    });
    setError('');
    setEditItem(q);
  }

  function validate(): string | null {
    if (!form.programId) return 'Program seçilmelidir.';
    if (form.quantity < 0) return 'Kontenjan negatif olamaz.';
    return null;
  }

  async function handleCreate() {
    const err = validate(); if (err) { setError(err); return; }
    try {
      await createQuota.mutateAsync({
        programId: form.programId,
        year: form.year,
        type: form.type,
        quantity: form.quantity,
        description: form.description.trim() || null,
      });
      setCreateOpen(false);
    } catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleUpdate() {
    if (!editItem) return;
    const err = validate(); if (err) { setError(err); return; }
    try {
      await updateQuota.mutateAsync({
        id: editItem.id,
        year: form.year,
        type: form.type,
        quantity: form.quantity,
        description: form.description.trim() || null,
      });
      setEditItem(null);
    } catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleDelete() {
    if (!deleteItem) return;
    try { await deleteQuota.mutateAsync(deleteItem.id); setDeleteItem(null); }
    catch (e: unknown) { alert((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.'); }
  }

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">Program Kontenjan Bilgileri</h1>
        <p className="text-slate-600 mt-1">Her program için yıllık kontenjan rakamları. Sadece bilgi kayıt — öğrenci kayıt akışında kullanılmaz.</p>
      </div>

      <div className="flex items-end justify-between mb-4 gap-4 flex-wrap">
        <div className="flex items-end gap-3 flex-wrap">
          <div>
            <label className="text-xs text-muted-foreground block mb-1">Fakülte</label>
            <select
              className={SELECT_CLS + ' max-w-xs'}
              value={facultyFilter ?? 0}
              onChange={e => {
                setFacultyFilter(Number(e.target.value) || undefined);
                setDepartmentFilter(undefined);
                setProgramFilter(undefined);
              }}
            >
              <option value={0}>Tümü</option>
              {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs text-muted-foreground block mb-1">Bölüm</label>
            <select
              className={SELECT_CLS + ' max-w-xs'}
              value={departmentFilter ?? 0}
              onChange={e => {
                setDepartmentFilter(Number(e.target.value) || undefined);
                setProgramFilter(undefined);
              }}
            >
              <option value={0}>Tümü</option>
              {visibleDepartments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs text-muted-foreground block mb-1">Program</label>
            <select
              className={SELECT_CLS + ' max-w-xs'}
              value={programFilter ?? 0}
              onChange={e => setProgramFilter(Number(e.target.value) || undefined)}
            >
              <option value={0}>Tümü</option>
              {visiblePrograms.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs text-muted-foreground block mb-1">Yıl</label>
            <Input
              type="number"
              min={2000}
              max={2100}
              placeholder="Tümü"
              className="w-24"
              value={yearFilter ?? ''}
              onChange={e => setYearFilter(Number(e.target.value) || undefined)}
            />
          </div>
        </div>
        <Button size="sm" onClick={openCreate} disabled={visiblePrograms.length === 0}>
          <Plus className="h-4 w-4 mr-1" /> Yeni Kontenjan
        </Button>
      </div>

      <div className="bg-white border border-border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Fakülte / Bölüm / Program</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Yıl</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Tip</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Kontenjan</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Açıklama</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading ? (
              <tr><td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td></tr>
            ) : quotas.length === 0 ? (
              <tr><td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">Henüz kontenjan tanımlı değil.</td></tr>
            ) : quotas.map(q => (
              <tr key={q.id} className="hover:bg-muted/20 transition-colors">
                <td className="px-4 py-3">
                  <div className="text-xs text-muted-foreground">{q.facultyName} / {q.departmentName}</div>
                  <div className="font-medium text-foreground">{q.programName}</div>
                </td>
                <td className="px-4 py-3 text-muted-foreground">{q.year}</td>
                <td className="px-4 py-3"><Badge variant="outline">{q.typeLabel}</Badge></td>
                <td className="px-4 py-3 font-semibold text-foreground">{q.quantity}</td>
                <td className="px-4 py-3 text-muted-foreground text-sm">{q.description ?? '—'}</td>
                <td className="px-4 py-3">
                  <div className="flex gap-1 justify-end">
                    <Button size="sm" variant="ghost" onClick={() => openEdit(q)}><Edit2 className="h-3.5 w-3.5" /></Button>
                    <Button size="sm" variant="ghost" onClick={() => setDeleteItem(q)}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Dialog open={createOpen || !!editItem} onOpenChange={o => { if (!o) { setCreateOpen(false); setEditItem(null); } }}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>{editItem ? 'Kontenjan Düzenle' : 'Yeni Kontenjan'}</DialogTitle></DialogHeader>
          <div className="space-y-3">
            {!editItem && (
              <div>
                <label className="text-sm font-medium block mb-1">Program</label>
                <select
                  className={SELECT_CLS}
                  value={form.programId}
                  onChange={e => setForm({ ...form, programId: Number(e.target.value) })}
                >
                  <option value={0}>Program seçin...</option>
                  {allPrograms.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select>
              </div>
            )}
            {editItem && (
              <div className="text-sm text-muted-foreground">
                Program: <strong>{editItem.programName}</strong> ({editItem.facultyName} / {editItem.departmentName})
              </div>
            )}
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-sm font-medium block mb-1">Yıl</label>
                <Input type="number" min={2000} max={2100} value={form.year} onChange={e => setForm({ ...form, year: Number(e.target.value) })} />
              </div>
              <div>
                <label className="text-sm font-medium block mb-1">Tip</label>
                <select
                  className={SELECT_CLS}
                  value={form.type}
                  onChange={e => setForm({ ...form, type: e.target.value as QuotaTypeValue })}
                >
                  {QUOTA_TYPE_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
              </div>
            </div>
            <div>
              <label className="text-sm font-medium block mb-1">Kontenjan</label>
              <Input type="number" min={0} max={99999} value={form.quantity} onChange={e => setForm({ ...form, quantity: Number(e.target.value) })} />
            </div>
            <div>
              <label className="text-sm font-medium block mb-1">Açıklama</label>
              <Input value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} placeholder="(opsiyonel)" />
            </div>
          </div>
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => { setCreateOpen(false); setEditItem(null); }}>İptal</Button>
            <Button
              onClick={editItem ? handleUpdate : handleCreate}
              disabled={createQuota.isPending || updateQuota.isPending}
            >
              {createQuota.isPending || updateQuota.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!deleteItem} onOpenChange={o => !o && setDeleteItem(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader><DialogTitle>Silme Onayı</DialogTitle></DialogHeader>
          <p className="text-sm text-muted-foreground">
            <strong className="text-foreground">{deleteItem?.programName}</strong> — {deleteItem?.year} {deleteItem?.typeLabel} kontenjanı silinecek.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteItem(null)}>İptal</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteQuota.isPending}>
              {deleteQuota.isPending ? 'Siliniyor...' : 'Sil'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
