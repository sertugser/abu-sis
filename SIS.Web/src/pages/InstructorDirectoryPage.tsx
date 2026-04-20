import { useState } from 'react';
import { Search, UserPlus } from 'lucide-react';
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
  useAdminInstructors,
  useCreateInstructor,
  type AdminInstructorListItem,
} from '../hooks/useAdminInstructors';
import { useAdminDepartments } from '../hooks/useAdminLookup';

const TITLES = [
  'Prof. Dr.',
  'Doç. Dr.',
  'Dr.',
  'Arş. Gör.',
  'Öğr. Gör.',
  'Emeritus Prof. Dr.',
];

const TITLE_MAX_HOURS: Record<string, number> = {
  'Prof. Dr.': 12,
  'Doç. Dr.': 12,
  'Dr.': 10,
  'Arş. Gör.': 8,
  'Öğr. Gör.': 8,
  'Emeritus Prof. Dr.': 10,
};

export default function InstructorDirectoryPage() {
  const [search, setSearch] = useState('');
  const [departmentId, setDepartmentId] = useState<number | undefined>(undefined);
  const [title, setTitle] = useState('');
  const [isActive, setIsActive] = useState<boolean | undefined>(undefined);
  const [selectedInstructor, setSelectedInstructor] = useState<AdminInstructorListItem | null>(null);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');

  const [form, setForm] = useState({
    firstName: '',
    lastName: '',
    title: 'Dr.',
    departmentId: 0,
    maxWeeklyHours: 10,
    password: '',
    overrideEmail: '',
  });
  const [formError, setFormError] = useState('');

  const { data: instructors = [], isLoading } = useAdminInstructors({
    search: search || undefined,
    departmentId,
    title: title || undefined,
    isActive,
  });

  const { data: departments = [] } = useAdminDepartments();
  const createInstructor = useCreateInstructor();

  function openCreateModal() {
    setForm({
      firstName: '',
      lastName: '',
      title: 'Dr.',
      departmentId: 0,
      maxWeeklyHours: 10,
      password: '',
      overrideEmail: '',
    });
    setFormError('');
    setCreateModalOpen(true);
  }

  function handleTitleChange(t: string) {
    setForm(f => ({ ...f, title: t, maxWeeklyHours: TITLE_MAX_HOURS[t] ?? 10 }));
  }

  async function handleCreate() {
    if (!form.firstName.trim() || !form.lastName.trim()) {
      setFormError('Ad ve soyad zorunludur.');
      return;
    }
    if (!form.departmentId) {
      setFormError('Bölüm seçimi zorunludur.');
      return;
    }
    if (!form.password || form.password.length < 8) {
      setFormError('Şifre en az 8 karakter olmalıdır.');
      return;
    }

    try {
      await createInstructor.mutateAsync({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        title: form.title,
        departmentId: form.departmentId,
        maxWeeklyHours: form.maxWeeklyHours,
        password: form.password,
        overrideEmail: form.overrideEmail.trim() || undefined,
      });
      setCreateModalOpen(false);
      showSuccess('Personel oluşturuldu.');
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Personel oluşturulamadı.');
    }
  }

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  return (
    <div className="space-y-6">
      {/* Filters + Action */}
      <div className="flex flex-wrap gap-3 items-center bg-white border border-border rounded-xl p-4">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Ad, soyad veya e-posta ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>

        <select
          value={departmentId ?? ''}
          onChange={e => setDepartmentId(e.target.value ? Number(e.target.value) : undefined)}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Bölümler</option>
          {departments.map(d => (
            <option key={d.id} value={d.id}>{d.name}</option>
          ))}
        </select>

        <select
          value={title}
          onChange={e => setTitle(e.target.value)}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Unvanlar</option>
          {TITLES.map(t => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>

        <select
          value={isActive === undefined ? '' : String(isActive)}
          onChange={e => setIsActive(e.target.value === '' ? undefined : e.target.value === 'true')}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Durumlar</option>
          <option value="true">Aktif</option>
          <option value="false">Pasif</option>
        </select>

        <div className="flex items-center gap-2 ml-auto">
          {successMessage && (
            <span className="text-sm font-medium text-green-700">{successMessage}</span>
          )}
          <Button onClick={openCreateModal}>
            <UserPlus className="h-4 w-4 mr-2" />
            Yeni Personel Ekle
          </Button>
        </div>
      </div>


      {/* Table */}
      <div className="bg-white border border-border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad Soyad</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Unvan</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Bölüm</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">E-posta</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
              <th className="text-right px-4 py-3 font-semibold text-muted-foreground">Detay</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading ? (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td>
              </tr>
            ) : instructors.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">Personel bulunamadı.</td>
              </tr>
            ) : (
              instructors.map(instructor => (
                <tr key={instructor.id} className="hover:bg-muted/20 transition-colors">
                  <td className="px-4 py-3 font-medium text-foreground">
                    {instructor.firstName} {instructor.lastName}
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant="outline" className="text-xs">{instructor.title}</Badge>
                  </td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">
                    {instructor.departmentName}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">{instructor.email}</td>
                  <td className="px-4 py-3">
                    <Badge variant={instructor.isActive ? 'success' : 'destructive'}>
                      {instructor.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelectedInstructor(instructor)}
                    >
                      Görüntüle
                    </Button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Instructor Detail Modal */}
      <Dialog open={!!selectedInstructor} onOpenChange={open => !open && setSelectedInstructor(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Personel Detayı</DialogTitle>
          </DialogHeader>
          {selectedInstructor && (
            <div className="space-y-4 py-2">
              <div className="flex items-start gap-4">
                <div className="h-12 w-12 rounded-full bg-primary/10 flex items-center justify-center text-primary font-bold text-lg flex-shrink-0">
                  {selectedInstructor.firstName[0]}{selectedInstructor.lastName[0]}
                </div>
                <div>
                  <p className="font-semibold">{selectedInstructor.title} {selectedInstructor.firstName} {selectedInstructor.lastName}</p>
                  <p className="text-sm text-muted-foreground">{selectedInstructor.departmentName}</p>
                  <Badge variant={selectedInstructor.isActive ? 'success' : 'destructive'} className="mt-1">
                    {selectedInstructor.isActive ? 'Aktif' : 'Pasif'}
                  </Badge>
                </div>
              </div>
              <div className="grid grid-cols-1 gap-3 text-sm border-t border-border pt-4">
                <div>
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">E-posta</p>
                  <p className="mt-0.5 text-muted-foreground">{selectedInstructor.email}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">Bölüm Kodu</p>
                  <p className="mt-0.5 font-mono">{selectedInstructor.departmentCode}</p>
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setSelectedInstructor(null)}>Kapat</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Create Instructor Modal */}
      <Dialog open={createModalOpen} onOpenChange={setCreateModalOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Yeni Personel Ekle</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-2 max-h-[60vh] overflow-y-auto pr-1">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-sm font-medium">Ad *</label>
                <Input
                  value={form.firstName}
                  onChange={e => setForm(f => ({ ...f, firstName: e.target.value }))}
                  placeholder="Adı"
                />
              </div>
              <div className="space-y-1">
                <label className="text-sm font-medium">Soyad *</label>
                <Input
                  value={form.lastName}
                  onChange={e => setForm(f => ({ ...f, lastName: e.target.value }))}
                  placeholder="Soyadı"
                />
              </div>
            </div>

            <div className="space-y-1">
              <label className="text-sm font-medium">Unvan *</label>
              <select
                value={form.title}
                onChange={e => handleTitleChange(e.target.value)}
                className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
              >
                {TITLES.map(t => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>

            <div className="space-y-1">
              <label className="text-sm font-medium">Bölüm *</label>
              <select
                value={form.departmentId || ''}
                onChange={e => setForm(f => ({ ...f, departmentId: Number(e.target.value) }))}
                className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
              >
                <option value="">Bölüm seçin...</option>
                {departments.map(d => (
                  <option key={d.id} value={d.id}>{d.name}</option>
                ))}
              </select>
            </div>

            <div className="space-y-1">
              <label className="text-sm font-medium">Haftalık Maks. Ders Saati</label>
              <Input
                type="number"
                value={form.maxWeeklyHours}
                onChange={e => setForm(f => ({ ...f, maxWeeklyHours: Number(e.target.value) }))}
                min={1}
                max={20}
              />
            </div>

            <div className="space-y-1">
              <label className="text-sm font-medium">Şifre *</label>
              <Input
                type="password"
                value={form.password}
                onChange={e => setForm(f => ({ ...f, password: e.target.value }))}
                placeholder="En az 8 karakter"
              />
            </div>

            <div className="space-y-1">
              <label className="text-sm font-medium">E-posta Geçersiz Kıl</label>
              <Input
                value={form.overrideEmail}
                onChange={e => setForm(f => ({ ...f, overrideEmail: e.target.value }))}
                placeholder="Boş bırakılırsa otomatik üretilir"
              />
            </div>

            {formError && (
              <p className="text-sm text-destructive">{formError}</p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateModalOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createInstructor.isPending}>
              {createInstructor.isPending ? 'Oluşturuluyor...' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
