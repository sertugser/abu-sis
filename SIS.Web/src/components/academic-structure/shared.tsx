import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import type { FacultyDto, DepartmentDto } from '../../hooks/useAdminAcademicStructure';
import {
  SELECT_CLS,
  DEGREE_LEVELS,
  EDUCATION_TYPES,
  type FacultyFormState,
  type DepartmentFormState,
  type ProgramFormState,
} from './shared-constants';

// ─── Faculty Form ────────────────────────────────────────────────

export function FacultyFormFields({
  form, onChange
}: {
  form: FacultyFormState; onChange: (f: FacultyFormState) => void;
}) {
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

export function DepartmentFormFields({
  form, onChange, faculties
}: {
  form: DepartmentFormState;
  onChange: (f: DepartmentFormState) => void;
  faculties: FacultyDto[];
}) {
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

export function ProgramFormFields({
  form, onChange, departments
}: {
  form: ProgramFormState;
  onChange: (f: ProgramFormState) => void;
  departments: DepartmentDto[];
}) {
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

export function DeleteConfirmDialog({
  open, onClose, onConfirm, name, isPending
}: {
  open: boolean; onClose: () => void; onConfirm: () => void; name: string; isPending: boolean;
}) {
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

export function DataTable({ children }: { children: React.ReactNode }) {
  return (
    <div className="bg-white border border-border rounded-xl overflow-hidden">
      <table className="w-full text-sm">{children}</table>
    </div>
  );
}

// ─── Page Header Helper ──────────────────────────────────────────

export function PageHeader({ title, description }: { title: string; description: string }) {
  return (
    <div className="mb-6">
      <h1 className="text-2xl font-bold text-slate-900">{title}</h1>
      <p className="text-slate-600 mt-1">{description}</p>
    </div>
  );
}
