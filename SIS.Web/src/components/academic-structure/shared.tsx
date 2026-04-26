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

export function FacultyFormFields({
  form,
  onChange,
}: {
  form: FacultyFormState;
  onChange: (f: FacultyFormState) => void;
}) {
  return (
    <div className="space-y-3">
      <div>
        <label className="mb-1 block text-sm font-medium text-foreground">Fakülte Adı</label>
        <Input
          value={form.name}
          onChange={e => onChange({ ...form, name: e.target.value })}
          placeholder="Örn. Mühendislik Fakültesi"
        />
      </div>
      <div>
        <label className="mb-1 block text-sm font-medium text-foreground">Kod</label>
        <Input
          value={form.code}
          onChange={e => onChange({ ...form, code: e.target.value.toUpperCase() })}
          placeholder="Örn. MF"
          maxLength={10}
        />
      </div>
    </div>
  );
}

export function DepartmentFormFields({
  form,
  onChange,
  faculties,
}: {
  form: DepartmentFormState;
  onChange: (f: DepartmentFormState) => void;
  faculties: FacultyDto[];
}) {
  return (
    <div className="space-y-3">
      <div>
        <label className="mb-1 block text-sm font-medium text-foreground">Bölüm Adı</label>
        <Input
          value={form.name}
          onChange={e => onChange({ ...form, name: e.target.value })}
          placeholder="Örn. Bilgisayar Mühendisliği"
        />
      </div>
      <div>
        <label className="mb-1 block text-sm font-medium text-foreground">Kod</label>
        <Input
          value={form.code}
          onChange={e => onChange({ ...form, code: e.target.value.toUpperCase() })}
          placeholder="Örn. BM"
          maxLength={10}
        />
      </div>
      <div>
        <label className="mb-1 block text-sm font-medium text-foreground">Fakülte</label>
        <select
          className={SELECT_CLS}
          value={form.facultyId}
          onChange={e => onChange({ ...form, facultyId: Number(e.target.value) })}
        >
          <option value={0}>Fakülte seçin...</option>
          {faculties.map(f => (
            <option key={f.id} value={f.id}>
              {f.name}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}

export function ProgramFormFields({
  form,
  onChange,
  departments,
}: {
  form: ProgramFormState;
  onChange: (f: ProgramFormState) => void;
  departments: DepartmentDto[];
}) {
  return (
    <div className="space-y-3">
      <div className="rounded-lg border border-slate-200 bg-slate-50/70 px-3 py-2.5 text-xs text-slate-600">
        Program tanımı tek başına öğrenci akışlarını açmaz. Müfredat tanımı sonraki adımda zorunludur; dış sistem kodu ise entegrasyon hazırlığı için isteğe bağlıdır.
      </div>

      <div>
        <label className="mb-1 block text-sm font-medium text-foreground">Program Adı</label>
        <Input
          value={form.name}
          onChange={e => onChange({ ...form, name: e.target.value })}
          placeholder="Örn. Bilgisayar Mühendisliği Lisans"
        />
        <p className="mt-1 text-xs text-muted-foreground">
          Kullanıcıların listelerde ve seçim ekranlarında göreceği açık program adı.
        </p>
      </div>

      <div>
        <label className="mb-1 block text-sm font-medium text-foreground">Kod</label>
        <Input
          value={form.code}
          onChange={e => onChange({ ...form, code: e.target.value.toUpperCase() })}
          placeholder="Örn. BM-LIS"
          maxLength={20}
        />
        <p className="mt-1 text-xs text-muted-foreground">
          Sistem içi benzersiz kod. Raporlama ve ilerideki eşleştirmelerde tekrar kullanılır.
        </p>
      </div>

      <div>
        <label className="mb-1 block text-sm font-medium text-foreground">ÖSYM / Dış Sistem Kodu</label>
        <Input
          value={form.externalSystemCode}
          onChange={e => onChange({ ...form, externalSystemCode: e.target.value.toUpperCase() })}
          placeholder="Örn. 104210236"
          maxLength={30}
        />
        <p className="mt-1 text-xs text-muted-foreground">
          Şimdilik zorunlu değil. Daha sonra ÖSYM veya diğer dış servis entegrasyonlarında kullanılabilir.
        </p>
      </div>

      <div>
        <label className="mb-1 block text-sm font-medium text-foreground">Bölüm</label>
        <select
          className={SELECT_CLS}
          value={form.departmentId}
          onChange={e => onChange({ ...form, departmentId: Number(e.target.value) })}
        >
          <option value={0}>Bölüm seçin...</option>
          {departments.map(d => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </select>
        <p className="mt-1 text-xs text-muted-foreground">
          Program hangi bölüm altında açılacaksa onu seçin.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="mb-1 block text-sm font-medium text-foreground">Derece Düzeyi</label>
          <select
            className={SELECT_CLS}
            value={form.degreeLevel}
            onChange={e => onChange({ ...form, degreeLevel: e.target.value })}
          >
            {DEGREE_LEVELS.map(d => (
              <option key={d.value} value={d.value}>
                {d.label}
              </option>
            ))}
          </select>
          <p className="mt-1 text-xs text-muted-foreground">Ön lisans, lisans, yüksek lisans veya doktora.</p>
        </div>

        <div>
          <label className="mb-1 block text-sm font-medium text-foreground">Eğitim Türü</label>
          <select
            className={SELECT_CLS}
            value={form.educationType}
            onChange={e => onChange({ ...form, educationType: e.target.value })}
          >
            {EDUCATION_TYPES.map(t => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
          <p className="mt-1 text-xs text-muted-foreground">Dönemlik veya yıllık işleyişi belirler.</p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="mb-1 block text-sm font-medium text-foreground">Azami Süre (Dönem)</label>
          <Input
            type="number"
            min={1}
            max={20}
            value={form.maxStudyDurationSemesters}
            onChange={e => onChange({ ...form, maxStudyDurationSemesters: Number(e.target.value) })}
          />
          <p className="mt-1 text-xs text-muted-foreground">
            Normal eğitim süresi değil, uzatma ve ilişik kesme kurallarında kullanılan üst sınır.
          </p>
        </div>

        <div>
          <label className="mb-1 block text-sm font-medium text-foreground">Toplam AKTS</label>
          <Input
            type="number"
            min={1}
            max={500}
            value={form.totalRequiredEcts}
            onChange={e => onChange({ ...form, totalRequiredEcts: Number(e.target.value) })}
          />
          <p className="mt-1 text-xs text-muted-foreground">Mezuniyet için hedeflenen toplam kredi yükü.</p>
        </div>
      </div>
    </div>
  );
}

export function DeleteConfirmDialog({
  open,
  onClose,
  onConfirm,
  name,
  isPending,
}: {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  name: string;
  isPending: boolean;
}) {
  return (
    <Dialog open={open} onOpenChange={o => !o && onClose()}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>Silme Onayı</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          <strong className="text-foreground">{name}</strong> silinecek. Bu işlem geri alınamaz.
        </p>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            İptal
          </Button>
          <Button variant="destructive" onClick={onConfirm} disabled={isPending}>
            {isPending ? 'Siliniyor...' : 'Sil'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function DataTable({ children }: { children: React.ReactNode }) {
  return (
    <div className="overflow-hidden rounded-xl border border-border bg-white">
      <table className="w-full text-sm">{children}</table>
    </div>
  );
}

export function PageHeader({ title, description }: { title: string; description: string }) {
  return (
    <div className="mb-6">
      <h1 className="text-2xl font-bold text-slate-900">{title}</h1>
      <p className="mt-1 text-slate-600">{description}</p>
    </div>
  );
}
