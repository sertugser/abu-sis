import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { useCreateStudent } from '../../hooks/useAdminStudents';
import { useAdminPrograms, useAdminAdvisors } from '../../hooks/useAdminLookup';

const CURRENT_YEAR = new Date().getFullYear();

const EMPTY_FORM = {
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
};

interface CreateStudentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Bölüm seçimi — program listesini sınırlamak için (opsiyonel). */
  departmentId?: number;
  onCreated: (message: string) => void;
}

export default function CreateStudentDialog({
  open,
  onOpenChange,
  departmentId,
  onCreated,
}: CreateStudentDialogProps) {
  const [form, setForm] = useState(EMPTY_FORM);
  const [formError, setFormError] = useState('');

  const { data: programs = [] } = useAdminPrograms(departmentId);
  const { data: advisors = [] } = useAdminAdvisors();
  const createStudent = useCreateStudent();

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
      onOpenChange(false);
      onCreated('Öğrenci oluşturuldu.');
    } catch (err: unknown) {
      setFormError(err instanceof Error ? err.message : 'Öğrenci oluşturulamadı.');
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
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
          <Button variant="outline" onClick={() => onOpenChange(false)}>İptal</Button>
          <Button onClick={handleCreate} disabled={createStudent.isPending}>
            {createStudent.isPending ? 'Oluşturuluyor...' : 'Oluştur'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
