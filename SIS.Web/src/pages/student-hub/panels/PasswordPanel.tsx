import { useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import { useResetPassword } from '../../../hooks/useAdminUsers';
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
const MIN_PASSWORD_LENGTH = 8;

export default function PasswordPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const resetPassword = useResetPassword();

  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  if (!context) return null;
  const { student } = context;

  const canReset = hasPermission('menu.kullanici-tanimlama.reset-password');

  const handleReset = () => {
    setError(null);
    setSuccess(null);
    if (newPassword.length < MIN_PASSWORD_LENGTH) {
      setError('Şifre en az 8 karakter olmalıdır.');
      return;
    }
    setConfirmOpen(true);
  };

  const performReset = async () => {
    setError(null);
    setSuccess(null);
    try {
      await resetPassword.mutateAsync({ id: student.userId, newPassword });
      setSuccess('Şifre sıfırlandı.');
      setNewPassword('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Şifre sıfırlanamadı.');
    } finally {
      setConfirmOpen(false);
    }
  };

  return (
    <div className="max-w-2xl space-y-6">
      {!canReset ? (
        <p className="text-sm text-muted-foreground">Şifre sıfırlama yetkiniz yok.</p>
      ) : (
        <>
          <div>
            <label className={FIELD_LABEL} htmlFor="field-new-password">
              Yeni Şifre
            </label>
            <Input
              id="field-new-password"
              type="password"
              className="mt-1"
              value={newPassword}
              onChange={(e) => {
                if (success) setSuccess(null);
                setNewPassword(e.target.value);
              }}
            />
          </div>

          {error && <p className="text-sm text-destructive">{error}</p>}
          {success && <p className="text-sm text-emerald-600">{success}</p>}

          <Button onClick={handleReset} disabled={resetPassword.isPending || confirmOpen}>
            {resetPassword.isPending ? 'Sıfırlanıyor...' : 'Şifreyi Sıfırla'}
          </Button>

          <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Şifre sıfırlama onayı</AlertDialogTitle>
                <AlertDialogDescription>
                  Bu öğrencinin şifresini sıfırlamak üzeresiniz. Onaylıyor musunuz?
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Vazgeç</AlertDialogCancel>
                <AlertDialogAction onClick={() => void performReset()}>Onayla</AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </>
      )}
    </div>
  );
}
