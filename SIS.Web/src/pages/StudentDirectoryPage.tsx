import { useState } from 'react';
import { Search, UserPlus, ChevronLeft, ChevronRight } from 'lucide-react';
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
  useAdminStudents,
  useCreateStudent,
  type AdminStudentListItem,
} from '../hooks/useAdminStudents';
import { useAdminDepartments, useAdminPrograms, useAdminAdvisors } from '../hooks/useAdminLookup';
import { STUDENT_STATUS_LABELS } from '../lib/constants';

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'success' | 'warning' | 'outline'> = {
  Active: 'success',
  Passive: 'secondary',
  Frozen: 'warning',
  Suspended: 'destructive',
  Dismissed: 'destructive',
  Graduated: 'default',
};

const CURRENT_YEAR = new Date().getFullYear();

function cgpaColor(cgpa: number) {
  if (cgpa >= 3.0) return 'text-green-600';
  if (cgpa >= 2.0) return 'text-yellow-600';
  return 'text-red-600';
}

export default function StudentDirectoryPage() {
  const [search, setSearch] = useState('');
  const [departmentId, setDepartmentId] = useState<number | undefined>(undefined);
  const [programId, setProgramId] = useState<number | undefined>(undefined);
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [selectedStudent, setSelectedStudent] = useState<AdminStudentListItem | null>(null);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');

  // Create form state
  const [form, setForm] = useState({
    firstName: '',
    lastName: '',
    nationalId: '',
    programId: 0,
    entryYear: CURRENT_YEAR,
    isScholarshipStudent: false,
    password: '',
    advisorId: '' as string | number,
    overrideStudentNumber: '',
    overrideEmail: '',
  });
  const [formError, setFormError] = useState('');

  const { data, isLoading } = useAdminStudents({
    search: search || undefined,
    departmentId,
    programId,
    status: status || undefined,
    page,
    pageSize: 25,
  });

  const { data: departments = [] } = useAdminDepartments();
  const { data: programs = [] } = useAdminPrograms(departmentId);
  const { data: advisors = [] } = useAdminAdvisors();
  const createStudent = useCreateStudent();

  function openCreateModal() {
    setForm({
      firstName: '',
      lastName: '',
      nationalId: '',
      programId: 0,
      entryYear: CURRENT_YEAR,
      isScholarshipStudent: false,
      password: '',
      advisorId: '',
      overrideStudentNumber: '',
      overrideEmail: '',
    });
    setFormError('');
    setCreateModalOpen(true);
  }

  async function handleCreate() {
    if (!form.firstName.trim() || !form.lastName.trim()) {
      setFormError('Ad ve soyad zorunludur.');
      return;
    }
    if (!form.nationalId.trim() || form.nationalId.length !== 11) {
      setFormError('TC kimlik numarası 11 haneli olmalıdır.');
      return;
    }
    if (!form.programId) {
      setFormError('Program seçimi zorunludur.');
      return;
    }
    if (!form.password || form.password.length < 8) {
      setFormError('Şifre en az 8 karakter olmalıdır.');
      return;
    }

    try {
      await createStudent.mutateAsync({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        nationalId: form.nationalId.trim(),
        programId: form.programId,
        entryYear: form.entryYear,
        isScholarshipStudent: form.isScholarshipStudent,
        password: form.password,
        advisorId: form.advisorId ? Number(form.advisorId) : undefined,
        overrideStudentNumber: form.overrideStudentNumber.trim() || undefined,
        overrideEmail: form.overrideEmail.trim() || undefined,
      });
      setCreateModalOpen(false);
      showSuccess('Öğrenci oluşturuldu.');
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Öğrenci oluşturulamadı.');
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
            placeholder="Ad, soyad veya öğrenci numarası..."
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1); }}
            className="pl-9"
          />
        </div>

        <select
          value={departmentId ?? ''}
          onChange={e => {
            setDepartmentId(e.target.value ? Number(e.target.value) : undefined);
            setProgramId(undefined);
            setPage(1);
          }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Bölümler</option>
          {departments.map(d => (
            <option key={d.id} value={d.id}>{d.name}</option>
          ))}
        </select>

        <select
          value={programId ?? ''}
          onChange={e => { setProgramId(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Programlar</option>
          {programs.map(p => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>

        <select
          value={status}
          onChange={e => { setStatus(e.target.value); setPage(1); }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Durumlar</option>
          {Object.entries(STUDENT_STATUS_LABELS).map(([k, v]) => (
            <option key={k} value={k}>{v}</option>
          ))}
        </select>

        <div className="flex items-center gap-2 ml-auto">
          {successMessage && (
            <span className="text-sm font-medium text-green-700">{successMessage}</span>
          )}
          <Button onClick={openCreateModal}>
            <UserPlus className="h-4 w-4 mr-2" />
            Yeni Öğrenci Ekle
          </Button>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white border border-border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Öğrenci No</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad Soyad</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Program</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">GNO</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Dönem</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Danışman</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
              <th className="text-right px-4 py-3 font-semibold text-muted-foreground">Detay</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading ? (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td>
              </tr>
            ) : data?.items.length === 0 ? (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">Öğrenci bulunamadı.</td>
              </tr>
            ) : (
              data?.items.map(student => (
                <tr key={student.id} className="hover:bg-muted/20 transition-colors">
                  <td className="px-4 py-3 font-mono text-xs">{student.studentNumber}</td>
                  <td className="px-4 py-3 font-medium text-foreground">
                    {student.firstName} {student.lastName}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    <div className="text-xs">{student.programName}</div>
                    <div className="text-xs text-muted-foreground/70">{student.departmentName}</div>
                  </td>
                  <td className="px-4 py-3">
                    <span className={`font-semibold ${cgpaColor(student.cgpa)}`}>
                      {student.cgpa.toFixed(2)}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-center text-muted-foreground">{student.completedSemesters}</td>
                  <td className="px-4 py-3 text-muted-foreground text-xs">{student.advisorName ?? '—'}</td>
                  <td className="px-4 py-3">
                    <Badge variant={STATUS_VARIANT[student.status] ?? 'secondary'}>
                      {STUDENT_STATUS_LABELS[student.status] ?? student.status}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelectedStudent(student)}
                    >
                      Görüntüle
                    </Button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>

        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-border">
            <p className="text-sm text-muted-foreground">
              Toplam {data.totalCount} öğrenci — Sayfa {data.page}/{data.totalPages}
            </p>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <Button variant="outline" size="sm" disabled={page >= data.totalPages} onClick={() => setPage(p => p + 1)}>
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Student Detail Modal */}
      <Dialog open={!!selectedStudent} onOpenChange={open => !open && setSelectedStudent(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle className="flex items-center justify-between">
              <span>Öğrenci Detayı</span>
            </DialogTitle>
          </DialogHeader>
          {selectedStudent && (
            <div className="space-y-4 py-2">
              <div className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
                <div>
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">Öğrenci No</p>
                  <p className="font-mono font-semibold mt-0.5">{selectedStudent.studentNumber}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">Durum</p>
                  <div className="mt-0.5">
                    <Badge variant={STATUS_VARIANT[selectedStudent.status] ?? 'secondary'}>
                      {STUDENT_STATUS_LABELS[selectedStudent.status] ?? selectedStudent.status}
                    </Badge>
                  </div>
                </div>
                <div className="col-span-2">
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">Ad Soyad</p>
                  <p className="font-medium mt-0.5">{selectedStudent.firstName} {selectedStudent.lastName}</p>
                </div>
                <div className="col-span-2">
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">E-posta</p>
                  <p className="mt-0.5 text-muted-foreground">{selectedStudent.email}</p>
                </div>
                <div className="col-span-2">
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">Program</p>
                  <p className="mt-0.5">{selectedStudent.programName}</p>
                  <p className="text-xs text-muted-foreground">{selectedStudent.departmentName}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">GNO</p>
                  <p className={`font-semibold mt-0.5 ${cgpaColor(selectedStudent.cgpa)}`}>
                    {selectedStudent.cgpa.toFixed(2)} / 4.00
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">Tamamlanan Dönem</p>
                  <p className="mt-0.5">{selectedStudent.completedSemesters}</p>
                </div>
                <div className="col-span-2">
                  <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">Danışman</p>
                  <p className="mt-0.5">{selectedStudent.advisorName ?? '—'}</p>
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setSelectedStudent(null)}>Kapat</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Create Student Modal */}
      <Dialog open={createModalOpen} onOpenChange={setCreateModalOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Yeni Öğrenci Ekle</DialogTitle>
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
              <label className="text-sm font-medium">TC Kimlik No *</label>
              <Input
                value={form.nationalId}
                onChange={e => setForm(f => ({ ...f, nationalId: e.target.value }))}
                placeholder="11 haneli TC kimlik numarası"
                maxLength={11}
              />
            </div>

            <div className="space-y-1">
              <label className="text-sm font-medium">Program *</label>
              <select
                value={form.programId || ''}
                onChange={e => setForm(f => ({ ...f, programId: Number(e.target.value) }))}
                className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
              >
                <option value="">Program seçin...</option>
                {programs.map(p => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-sm font-medium">Giriş Yılı *</label>
                <Input
                  type="number"
                  value={form.entryYear}
                  onChange={e => setForm(f => ({ ...f, entryYear: Number(e.target.value) }))}
                  min={2000}
                  max={CURRENT_YEAR + 1}
                />
              </div>
              <div className="space-y-1 flex flex-col justify-end">
                <label className="flex items-center gap-2 cursor-pointer pb-2">
                  <input
                    type="checkbox"
                    checked={form.isScholarshipStudent}
                    onChange={e => setForm(f => ({ ...f, isScholarshipStudent: e.target.checked }))}
                    className="h-4 w-4 rounded"
                  />
                  <span className="text-sm font-medium">Burslu Öğrenci</span>
                </label>
              </div>
            </div>

            <div className="space-y-1">
              <label className="text-sm font-medium">Danışman</label>
              <select
                value={form.advisorId}
                onChange={e => setForm(f => ({ ...f, advisorId: e.target.value }))}
                className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
              >
                <option value="">Danışman seçin (opsiyonel)</option>
                {advisors.map(a => (
                  <option key={a.id} value={a.id}>{a.fullName} — {a.departmentName}</option>
                ))}
              </select>
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

            <div className="border-t border-border pt-3 space-y-3">
              <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">Opsiyonel Geçersiz Kılma</p>
              <div className="space-y-1">
                <label className="text-sm font-medium">Öğrenci Numarası Geçersiz Kıl</label>
                <Input
                  value={form.overrideStudentNumber}
                  onChange={e => setForm(f => ({ ...f, overrideStudentNumber: e.target.value }))}
                  placeholder="Boş bırakılırsa otomatik üretilir"
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
            </div>

            {formError && (
              <p className="text-sm text-destructive">{formError}</p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateModalOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createStudent.isPending}>
              {createStudent.isPending ? 'Oluşturuluyor...' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
