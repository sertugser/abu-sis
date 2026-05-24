import { useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import { useChangeStudentNumber } from '../../../hooks/useAdminStudents';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogFooter,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogAction,
  AlertDialogCancel,
} from '@/components/ui/alert-dialog';

const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';

export default function StudentNumberPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const changeNumber = useChangeStudentNumber();

  const [newNumber, setNewNumber] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  if (!context) return null;
  const { student } = context;

  const canChange = hasPermission('menu.ogrenci-no-degistirme.change');

  const handleChange = () => {
    setError(null);
    setSuccess(null);
    const trimmed = newNumber.trim();
    if (!trimmed || trimmed === student.studentNumber) {
      setError('Geçerli, farklı bir öğrenci numarası girin.');
      return;
    }
    setConfirmOpen(true);
  };

  const performChange = async () => {
    setError(null);
    setSuccess(null);
    try {
      await changeNumber.mutateAsync({
        studentId: student.id,
        newStudentNumber: newNumber.trim(),
      });
      setSuccess('Öğrenci numarası güncellendi.');
      setNewNumber('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Öğrenci numarası değiştirilemedi.');
    } finally {
      setConfirmOpen(false);
    }
  };

  return (
    <div className="max-w-2xl space-y-6">
      <div>
        <label className={FIELD_LABEL} htmlFor="field-current-number">
          Mevcut Öğrenci No
        </label>
        <Input id="field-current-number" className="mt-1" value={student.studentNumber} readOnly />
      </div>

      {!canChange ? (
        <p className="text-sm text-muted-foreground">Öğrenci no değiştirme yetkiniz yok.</p>
      ) : (
        <>
          <div>
            <label className={FIELD_LABEL} htmlFor="field-new-number">
              Yeni Öğrenci No
            </label>
            <Input
              id="field-new-number"
              className="mt-1"
              value={newNumber}
              onChange={(e) => {
                if (success) setSuccess(null);
                setNewNumber(e.target.value);
              }}
            />
          </div>

          {error && <p className="text-sm text-destructive">{error}</p>}
          {success && <p className="text-sm text-emerald-600">{success}</p>}

          <Button onClick={handleChange} disabled={changeNumber.isPending || confirmOpen}>
            {changeNumber.isPending ? 'Değiştiriliyor...' : 'Değiştir'}
          </Button>

          <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Öğrenci No Değişikliği</AlertDialogTitle>
                <AlertDialogDescription>
                  &apos;{student.studentNumber}&apos; numarasını &apos;{newNumber.trim()}&apos;
                  olarak değiştirmek üzeresiniz. Onaylıyor musunuz?
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Vazgeç</AlertDialogCancel>
                <AlertDialogAction onClick={() => void performChange()}>Onayla</AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </>
      )}
    </div>
  );
}
