import { useState } from 'react';
import { Plus, Edit2, Trash2, Building2, BookOpen, GraduationCap } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
  useCreateFaculty,
  useUpdateFaculty,
  useDeleteFaculty,
  useCreateDepartment,
  useUpdateDepartment,
  useDeleteDepartment,
  useCreateProgram,
  useUpdateProgram,
  useDeleteProgram,
  type FacultyDto,
  type DepartmentDto,
  type ProgramDto,
} from '../hooks/useAdminAcademicStructure';

type Tab = 'faculties' | 'departments' | 'programs';

const DEGREE_LEVELS = [
  { value: 'Associate', label: 'Ön Lisans' },
  { value: 'Bachelor', label: 'Lisans' },
  { value: 'Master', label: 'Yüksek Lisans' },
  { value: 'Doctorate', label: 'Doktora' },
];

const EDUCATION_TYPES = [
  { value: 'Semester', label: 'Dönemlik' },
  { value: 'Annual', label: 'Yıllık' },
];

const SELECT_CLS = 'w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring';

// ─── Faculty Form ────────────────────────────────────────────────

interface FacultyFormState { name: string; code: string; }
function emptyFacultyForm(): FacultyFormState { return { name: '', code: '' }; }

function FacultyFormFields({ form, onChange }: { form: FacultyFormState; onChange: (f: FacultyFormState) => void }) {
  return (
    <div className="space-y-3">
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Fakülte Adı</label>
        <Input value={form.name} onChange={e => onChange({ ...form, name: e.target.value })} placeholder="örn. Mühendislik Fakültesi" />
      </div>
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Kod</label>
        <Input value={form.code} onChange={e => onChange({ ...form, code: e.target.value.toUpperCase() })} placeholder="örn. MF" maxLength={10} />
      </div>
    </div>
  );
}

// ─── Department Form ─────────────────────────────────────────────

interface DepartmentFormState { name: string; code: string; facultyId: number; }

function DepartmentFormFields({
  form, onChange, faculties,
}: { form: DepartmentFormState; onChange: (f: DepartmentFormState) => void; faculties: FacultyDto[]; }) {
  return (
    <div className="space-y-3">
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Bölüm Adı</label>
        <Input value={form.name} onChange={e => onChange({ ...form, name: e.target.value })} placeholder="örn. Bilgisayar Mühendisliği" />
      </div>
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Kod</label>
        <Input value={form.code} onChange={e => onChange({ ...form, code: e.target.value.toUpperCase() })} placeholder="örn. BM" maxLength={10} />
      </div>
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Fakülte</label>
        <select className={SELECT_CLS} value={form.facultyId} onChange={e => onChange({ ...form, facultyId: Number(e.target.value) })}>
          <option value={0}>Fakülte seçin...</option>
          {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
        </select>
      </div>
    </div>
  );
}

// ─── Program Form ────────────────────────────────────────────────

interface ProgramFormState {
  name: string; code: string; departmentId: number;
  degreeLevel: string; educationType: string;
  maxStudyDurationSemesters: number; totalRequiredEcts: number;
}

function ProgramFormFields({
  form, onChange, departments,
}: { form: ProgramFormState; onChange: (f: ProgramFormState) => void; departments: DepartmentDto[]; }) {
  return (
    <div className="space-y-3">
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Program Adı</label>
        <Input value={form.name} onChange={e => onChange({ ...form, name: e.target.value })} placeholder="örn. Bilgisayar Mühendisliği Lisans" />
      </div>
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Kod</label>
        <Input value={form.code} onChange={e => onChange({ ...form, code: e.target.value.toUpperCase() })} placeholder="örn. BM-LIS" maxLength={20} />
      </div>
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Bölüm</label>
        <select className={SELECT_CLS} value={form.departmentId} onChange={e => onChange({ ...form, departmentId: Number(e.target.value) })}>
          <option value={0}>Bölüm seçin...</option>
          {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="text-sm font-medium text-foreground block mb-1">Derece Düzeyi</label>
          <select className={SELECT_CLS} value={form.degreeLevel} onChange={e => onChange({ ...form, degreeLevel: e.target.value })}>
            {DEGREE_LEVELS.map(d => <option key={d.value} value={d.value}>{d.label}</option>)}
          </select>
        </div>
        <div>
          <label className="text-sm font-medium text-foreground block mb-1">Eğitim Türü</label>
          <select className={SELECT_CLS} value={form.educationType} onChange={e => onChange({ ...form, educationType: e.target.value })}>
            {EDUCATION_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="text-sm font-medium text-foreground block mb-1">Maks. Süre (Dönem)</label>
          <Input type="number" min={1} max={20} value={form.maxStudyDurationSemesters} onChange={e => onChange({ ...form, maxStudyDurationSemesters: Number(e.target.value) })} />
        </div>
        <div>
          <label className="text-sm font-medium text-foreground block mb-1">Toplam AKTS</label>
          <Input type="number" min={1} max={500} value={form.totalRequiredEcts} onChange={e => onChange({ ...form, totalRequiredEcts: Number(e.target.value) })} />
        </div>
      </div>
    </div>
  );
}

// ─── Delete Confirm Dialog ───────────────────────────────────────

function DeleteConfirmDialog({
  open, onClose, onConfirm, name, isPending,
}: { open: boolean; onClose: () => void; onConfirm: () => void; name: string; isPending: boolean; }) {
  return (
    <Dialog open={open} onOpenChange={o => !o && onClose()}>
      <DialogContent className="max-w-sm">
        <DialogHeader><DialogTitle>Silme Onayı</DialogTitle></DialogHeader>
        <p className="text-sm text-muted-foreground">
          <strong className="text-foreground">{name}</strong> silinecek. Bu işlem geri alınamaz.
        </p>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>İptal</Button>
          <Button variant="destructive" onClick={onConfirm} disabled={isPending}>
            {isPending ? 'Siliniyor...' : 'Sil'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Shared Table Wrapper ────────────────────────────────────────

function DataTable({ children }: { children: React.ReactNode }) {
  return (
    <div className="bg-white border border-border rounded-xl overflow-hidden">
      <table className="w-full text-sm">{children}</table>
    </div>
  );
}

// ─── Faculties Tab ───────────────────────────────────────────────

function FacultiesTab() {
  const { data: faculties = [], isLoading } = useAdminFaculties();
  const createFaculty = useCreateFaculty();
  const updateFaculty = useUpdateFaculty();
  const deleteFaculty = useDeleteFaculty();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<FacultyDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<FacultyDto | null>(null);
  const [form, setForm] = useState<FacultyFormState>(emptyFacultyForm());
  const [error, setError] = useState('');

  function openCreate() { setForm(emptyFacultyForm()); setError(''); setCreateOpen(true); }
  function openEdit(f: FacultyDto) { setForm({ name: f.name, code: f.code }); setError(''); setEditItem(f); }

  async function handleCreate() {
    if (!form.name.trim() || !form.code.trim()) { setError('Ad ve kod zorunludur.'); return; }
    try { await createFaculty.mutateAsync(form); setCreateOpen(false); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleUpdate() {
    if (!editItem) return;
    if (!form.name.trim() || !form.code.trim()) { setError('Ad ve kod zorunludur.'); return; }
    try { await updateFaculty.mutateAsync({ id: editItem.id, ...form }); setEditItem(null); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleDelete() {
    if (!deleteItem) return;
    try { await deleteFaculty.mutateAsync(deleteItem.id); setDeleteItem(null); }
    catch (e: unknown) { alert((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.'); }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">{isLoading ? 'Yükleniyor...' : `${faculties.length} fakülte`}</p>
        <Button size="sm" onClick={openCreate}><Plus className="h-4 w-4 mr-1" /> Fakülte Ekle</Button>
      </div>

      <DataTable>
        <thead className="bg-muted/40 border-b border-border">
          <tr>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Kod</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Bölüm</th>
            <th className="px-4 py-3" />
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isLoading ? (
            <tr><td colSpan={4} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td></tr>
          ) : faculties.length === 0 ? (
            <tr><td colSpan={4} className="px-4 py-8 text-center text-muted-foreground">Henüz fakülte yok.</td></tr>
          ) : faculties.map(f => (
            <tr key={f.id} className="hover:bg-muted/20 transition-colors">
              <td className="px-4 py-3 font-medium text-foreground">{f.name}</td>
              <td className="px-4 py-3"><Badge variant="outline">{f.code}</Badge></td>
              <td className="px-4 py-3 text-muted-foreground">{f.departmentCount} bölüm</td>
              <td className="px-4 py-3">
                <div className="flex gap-1 justify-end">
                  <Button size="sm" variant="ghost" onClick={() => openEdit(f)}><Edit2 className="h-3.5 w-3.5" /></Button>
                  <Button size="sm" variant="ghost" onClick={() => setDeleteItem(f)}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </DataTable>

      <Dialog open={createOpen} onOpenChange={o => !o && setCreateOpen(false)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Fakülte Ekle</DialogTitle></DialogHeader>
          <FacultyFormFields form={form} onChange={f => { setForm(f); setError(''); }} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createFaculty.isPending}>{createFaculty.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editItem} onOpenChange={o => !o && setEditItem(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Fakülte Düzenle</DialogTitle></DialogHeader>
          <FacultyFormFields form={form} onChange={f => { setForm(f); setError(''); }} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditItem(null)}>İptal</Button>
            <Button onClick={handleUpdate} disabled={updateFaculty.isPending}>{updateFaculty.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DeleteConfirmDialog open={!!deleteItem} onClose={() => setDeleteItem(null)} onConfirm={handleDelete} name={deleteItem?.name ?? ''} isPending={deleteFaculty.isPending} />
    </div>
  );
}

// ─── Departments Tab ─────────────────────────────────────────────

function DepartmentsTab() {
  const { data: faculties = [] } = useAdminFaculties();
  const [facultyFilter, setFacultyFilter] = useState<number | undefined>(undefined);
  const { data: departments = [], isLoading } = useAdminDepartmentsByStructure(facultyFilter);
  const createDept = useCreateDepartment();
  const updateDept = useUpdateDepartment();
  const deleteDept = useDeleteDepartment();

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<DepartmentDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<DepartmentDto | null>(null);
  const [form, setForm] = useState<DepartmentFormState>({ name: '', code: '', facultyId: 0 });
  const [error, setError] = useState('');

  function openCreate() { setForm({ name: '', code: '', facultyId: faculties[0]?.id ?? 0 }); setError(''); setCreateOpen(true); }
  function openEdit(d: DepartmentDto) { setForm({ name: d.name, code: d.code, facultyId: d.facultyId }); setError(''); setEditItem(d); }

  async function handleCreate() {
    if (!form.name.trim() || !form.code.trim() || !form.facultyId) { setError('Tüm alanlar zorunludur.'); return; }
    try { await createDept.mutateAsync(form); setCreateOpen(false); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleUpdate() {
    if (!editItem) return;
    if (!form.name.trim() || !form.code.trim() || !form.facultyId) { setError('Tüm alanlar zorunludur.'); return; }
    try { await updateDept.mutateAsync({ id: editItem.id, ...form }); setEditItem(null); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleDelete() {
    if (!deleteItem) return;
    try { await deleteDept.mutateAsync(deleteItem.id); setDeleteItem(null); }
    catch (e: unknown) { alert((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.'); }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <select
            className="rounded-md border border-border bg-background px-3 py-1.5 text-sm text-foreground"
            value={facultyFilter ?? ''}
            onChange={e => setFacultyFilter(e.target.value ? Number(e.target.value) : undefined)}
          >
            <option value="">Tüm Fakülteler</option>
            {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
          </select>
          <p className="text-sm text-muted-foreground">{isLoading ? 'Yükleniyor...' : `${departments.length} bölüm`}</p>
        </div>
        <Button size="sm" onClick={openCreate}><Plus className="h-4 w-4 mr-1" /> Bölüm Ekle</Button>
      </div>

      <DataTable>
        <thead className="bg-muted/40 border-b border-border">
          <tr>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Kod</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Fakülte</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Program</th>
            <th className="px-4 py-3" />
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isLoading ? (
            <tr><td colSpan={5} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td></tr>
          ) : departments.length === 0 ? (
            <tr><td colSpan={5} className="px-4 py-8 text-center text-muted-foreground">Bölüm bulunamadı.</td></tr>
          ) : departments.map(d => (
            <tr key={d.id} className="hover:bg-muted/20 transition-colors">
              <td className="px-4 py-3 font-medium text-foreground">{d.name}</td>
              <td className="px-4 py-3"><Badge variant="outline">{d.code}</Badge></td>
              <td className="px-4 py-3 text-muted-foreground">{d.facultyName}</td>
              <td className="px-4 py-3 text-muted-foreground">{d.programCount} program</td>
              <td className="px-4 py-3">
                <div className="flex gap-1 justify-end">
                  <Button size="sm" variant="ghost" onClick={() => openEdit(d)}><Edit2 className="h-3.5 w-3.5" /></Button>
                  <Button size="sm" variant="ghost" onClick={() => setDeleteItem(d)}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </DataTable>

      <Dialog open={createOpen} onOpenChange={o => !o && setCreateOpen(false)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Bölüm Ekle</DialogTitle></DialogHeader>
          <DepartmentFormFields form={form} onChange={f => { setForm(f); setError(''); }} faculties={faculties} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createDept.isPending}>{createDept.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editItem} onOpenChange={o => !o && setEditItem(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Bölüm Düzenle</DialogTitle></DialogHeader>
          <DepartmentFormFields form={form} onChange={f => { setForm(f); setError(''); }} faculties={faculties} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditItem(null)}>İptal</Button>
            <Button onClick={handleUpdate} disabled={updateDept.isPending}>{updateDept.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DeleteConfirmDialog open={!!deleteItem} onClose={() => setDeleteItem(null)} onConfirm={handleDelete} name={deleteItem?.name ?? ''} isPending={deleteDept.isPending} />
    </div>
  );
}

// ─── Programs Tab ────────────────────────────────────────────────

const DEGREE_LABELS: Record<string, string> = {
  Associate: 'Ön Lisans', Bachelor: 'Lisans', Master: 'Yüksek Lisans', Doctorate: 'Doktora',
};

function ProgramsTab() {
  const { data: departments = [] } = useAdminDepartmentsByStructure(undefined);
  const [deptFilter, setDeptFilter] = useState<number | undefined>(undefined);
  const { data: programs = [], isLoading } = useAdminProgramsByStructure(deptFilter);
  const createProg = useCreateProgram();
  const updateProg = useUpdateProgram();
  const deleteProg = useDeleteProgram();

  const emptyProgramForm = (): ProgramFormState => ({
    name: '', code: '', departmentId: departments[0]?.id ?? 0,
    degreeLevel: 'Bachelor', educationType: 'Semester',
    maxStudyDurationSemesters: 8, totalRequiredEcts: 240,
  });

  const [createOpen, setCreateOpen] = useState(false);
  const [editItem, setEditItem] = useState<ProgramDto | null>(null);
  const [deleteItem, setDeleteItem] = useState<ProgramDto | null>(null);
  const [form, setForm] = useState<ProgramFormState>(emptyProgramForm());
  const [error, setError] = useState('');

  function openCreate() { setForm(emptyProgramForm()); setError(''); setCreateOpen(true); }
  function openEdit(p: ProgramDto) {
    setForm({ name: p.name, code: p.code, departmentId: p.departmentId, degreeLevel: p.degreeLevel, educationType: p.educationType, maxStudyDurationSemesters: p.maxStudyDurationSemesters, totalRequiredEcts: p.totalRequiredEcts });
    setError(''); setEditItem(p);
  }

  async function handleCreate() {
    if (!form.name.trim() || !form.code.trim() || !form.departmentId) { setError('Tüm zorunlu alanları doldurun.'); return; }
    try { await createProg.mutateAsync(form); setCreateOpen(false); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleUpdate() {
    if (!editItem) return;
    if (!form.name.trim() || !form.code.trim() || !form.departmentId) { setError('Tüm zorunlu alanları doldurun.'); return; }
    try { await updateProg.mutateAsync({ id: editItem.id, ...form }); setEditItem(null); }
    catch (e: unknown) { setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleDelete() {
    if (!deleteItem) return;
    try { await deleteProg.mutateAsync(deleteItem.id); setDeleteItem(null); }
    catch (e: unknown) { alert((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.'); }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <select
            className="rounded-md border border-border bg-background px-3 py-1.5 text-sm text-foreground"
            value={deptFilter ?? ''}
            onChange={e => setDeptFilter(e.target.value ? Number(e.target.value) : undefined)}
          >
            <option value="">Tüm Bölümler</option>
            {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
          <p className="text-sm text-muted-foreground">{isLoading ? 'Yükleniyor...' : `${programs.length} program`}</p>
        </div>
        <Button size="sm" onClick={openCreate}><Plus className="h-4 w-4 mr-1" /> Program Ekle</Button>
      </div>

      <DataTable>
        <thead className="bg-muted/40 border-b border-border">
          <tr>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Kod</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Bölüm</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Derece</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">AKTS</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Müfredat</th>
            <th className="px-4 py-3" />
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isLoading ? (
            <tr><td colSpan={7} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td></tr>
          ) : programs.length === 0 ? (
            <tr><td colSpan={7} className="px-4 py-8 text-center text-muted-foreground">Program bulunamadı.</td></tr>
          ) : programs.map(p => (
            <tr key={p.id} className="hover:bg-muted/20 transition-colors">
              <td className="px-4 py-3 font-medium text-foreground">{p.name}</td>
              <td className="px-4 py-3"><Badge variant="outline">{p.code}</Badge></td>
              <td className="px-4 py-3 text-muted-foreground">{p.departmentName}</td>
              <td className="px-4 py-3"><Badge variant="secondary">{DEGREE_LABELS[p.degreeLevel] ?? p.degreeLevel}</Badge></td>
              <td className="px-4 py-3 text-muted-foreground">{p.totalRequiredEcts}</td>
              <td className="px-4 py-3 text-muted-foreground">{p.curriculumCount}</td>
              <td className="px-4 py-3">
                <div className="flex gap-1 justify-end">
                  <Button size="sm" variant="ghost" onClick={() => openEdit(p)}><Edit2 className="h-3.5 w-3.5" /></Button>
                  <Button size="sm" variant="ghost" onClick={() => setDeleteItem(p)}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </DataTable>

      <Dialog open={createOpen} onOpenChange={o => !o && setCreateOpen(false)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Program Ekle</DialogTitle></DialogHeader>
          <ProgramFormFields form={form} onChange={f => { setForm(f); setError(''); }} departments={departments} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createProg.isPending}>{createProg.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!editItem} onOpenChange={o => !o && setEditItem(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Program Düzenle</DialogTitle></DialogHeader>
          <ProgramFormFields form={form} onChange={f => { setForm(f); setError(''); }} departments={departments} />
          {error && <p className="text-sm text-destructive">{error}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditItem(null)}>İptal</Button>
            <Button onClick={handleUpdate} disabled={updateProg.isPending}>{updateProg.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DeleteConfirmDialog open={!!deleteItem} onClose={() => setDeleteItem(null)} onConfirm={handleDelete} name={deleteItem?.name ?? ''} isPending={deleteProg.isPending} />
    </div>
  );
}

// ─── Main Page ───────────────────────────────────────────────────

const TABS: { id: Tab; label: string; icon: React.ComponentType<{ className?: string }> }[] = [
  { id: 'faculties', label: 'Fakülteler', icon: Building2 },
  { id: 'departments', label: 'Bölümler', icon: BookOpen },
  { id: 'programs', label: 'Programlar', icon: GraduationCap },
];

export default function AcademicStructurePage() {
  const [activeTab, setActiveTab] = useState<Tab>('faculties');

  return (
    <div className="space-y-5">
      {/* Tab Bar */}
      <div className="border-b border-border">
        <nav className="flex gap-0">
          {TABS.map(tab => {
            const Icon = tab.icon;
            const active = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-2 px-5 py-3 text-sm font-medium border-b-2 transition-colors ${
                  active
                    ? 'border-primary text-primary'
                    : 'border-transparent text-muted-foreground hover:text-foreground hover:border-border'
                }`}
              >
                <Icon className="h-4 w-4" />
                {tab.label}
              </button>
            );
          })}
        </nav>
      </div>

      {/* Content */}
      {activeTab === 'faculties' && <FacultiesTab />}
      {activeTab === 'departments' && <DepartmentsTab />}
      {activeTab === 'programs' && <ProgramsTab />}
    </div>
  );
}
