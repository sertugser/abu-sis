import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, KeyRound, Loader2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth } from '../context/AuthContext';

export default function PasswordChangePage() {
  const navigate = useNavigate();
  const { changePassword, passwordChangeRequired } = useAuth();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setSuccess(null);

    if (newPassword.length < 8) {
      setError('Yeni şifre en az 8 karakter olmalıdır.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setError('Şifre tekrarı eşleşmiyor.');
      return;
    }

    setIsSaving(true);
    try {
      const responseError = await changePassword(currentPassword, newPassword);
      if (responseError) {
        setError(responseError);
        return;
      }

      setSuccess('Şifreniz güncellendi.');
      navigate('/', { replace: true });
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="container mx-auto max-w-2xl p-6">
      <div className="mb-6">
        <h1 className="font-heading text-3xl font-bold tracking-tight">Şifre Değiştir</h1>
        <p className="mt-2 text-muted-foreground">
          {passwordChangeRequired
            ? 'Sistem ayarı nedeniyle şifrenizin süresi doldu. Devam etmek için yeni bir şifre belirleyin.'
            : 'Mevcut şifrenizi güvenli bir yeni şifre ile güncelleyin.'}
        </p>
      </div>

      <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <div className="mb-6 flex items-center gap-3">
          <div className="rounded-full bg-primary/10 p-3 text-primary">
            <KeyRound className="h-5 w-5" />
          </div>
          <div>
            <p className="font-medium text-foreground">Şifre güncelleme formu</p>
            <p className="text-sm text-muted-foreground">Değişiklik backend tarafından anında uygulanır.</p>
          </div>
        </div>

        {passwordChangeRequired && (
          <Alert className="mb-4 border-amber-200 bg-amber-50 text-amber-900">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              Şifre değişmeden diğer sistem ekranlarına erişim kapalıdır.
            </AlertDescription>
          </Alert>
        )}

        {error && (
          <Alert variant="destructive" className="mb-4">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {success && (
          <Alert className="mb-4">
            <AlertDescription>{success}</AlertDescription>
          </Alert>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="current-password">Mevcut Şifre</Label>
            <Input
              id="current-password"
              type="password"
              value={currentPassword}
              onChange={event => setCurrentPassword(event.target.value)}
              autoComplete="current-password"
              required
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="new-password">Yeni Şifre</Label>
            <Input
              id="new-password"
              type="password"
              value={newPassword}
              onChange={event => setNewPassword(event.target.value)}
              autoComplete="new-password"
              required
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="confirm-password">Yeni Şifre Tekrar</Label>
            <Input
              id="confirm-password"
              type="password"
              value={confirmPassword}
              onChange={event => setConfirmPassword(event.target.value)}
              autoComplete="new-password"
              required
            />
          </div>

          <div className="flex justify-end">
            <Button type="submit" disabled={isSaving}>
              {isSaving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              {isSaving ? 'Kaydediliyor...' : 'Şifreyi Güncelle'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
