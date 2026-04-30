import { useMemo, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, CheckCircle2, KeyRound, Loader2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { changePassword } from '../api/auth.api';
import { useAuth } from '../context/AuthContext';

function formatPasswordExpiry(value: string | null) {
  if (!value)
    return null;

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime()))
    return null;

  return parsed.toLocaleString('tr-TR');
}

export default function PasswordChangePage() {
  const navigate = useNavigate();
  const { clearPasswordRotationRequirement, passwordExpiresAt, requiresPasswordChange } = useAuth();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const passwordExpiryText = useMemo(
    () => formatPasswordExpiry(passwordExpiresAt),
    [passwordExpiresAt],
  );

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setSuccess(null);

    if (newPassword.length < 8) {
      setError('Yeni şifre en az 8 karakter olmalıdır.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setError('Yeni şifre ve tekrar alanı eşleşmiyor.');
      return;
    }

    if (currentPassword === newPassword) {
      setError('Yeni şifre mevcut şifre ile aynı olmamalıdır.');
      return;
    }

    setIsSubmitting(true);
    try {
      await changePassword({ currentPassword, newPassword });
      clearPasswordRotationRequirement();
      setSuccess('Şifreniz başarıyla güncellendi.');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      navigate('/', { replace: true });
    } catch (requestError: unknown) {
      const fallback = 'Şifre değiştirilirken bir hata oluştu.';
      const apiError = typeof requestError === 'object' && requestError !== null
        ? (requestError as { response?: { data?: { error?: string } } })
        : null;
      setError(apiError?.response?.data?.error ?? fallback);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="container mx-auto max-w-2xl p-6">
      <div className="mb-6">
        <h1 className="font-heading text-3xl font-bold tracking-tight">Şifre Değiştir</h1>
        <p className="mt-2 text-muted-foreground">
          Hesap güvenliğini korumak için mevcut şifrenizi yenileyin.
        </p>
      </div>

      {requiresPasswordChange && (
        <Alert className="mb-5 border-amber-200 bg-amber-50 text-amber-900">
          <KeyRound className="h-4 w-4" />
          <AlertTitle>Şifre yenileme zorunlu</AlertTitle>
          <AlertDescription>
            {passwordExpiryText
              ? `Şifre geçerlilik süreniz ${passwordExpiryText} tarihinde doldu. Devam etmek için şifrenizi güncelleyin.`
              : 'Devam etmek için şifrenizi güncelleyin.'}
          </AlertDescription>
        </Alert>
      )}

      {error && (
        <Alert variant="destructive" className="mb-5">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Şifre güncellenemedi</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {success && (
        <Alert className="mb-5 border-emerald-200 bg-emerald-50 text-emerald-900">
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Şifre güncellendi</AlertTitle>
          <AlertDescription>{success}</AlertDescription>
        </Alert>
      )}

      <form
        onSubmit={handleSubmit}
        className="space-y-5 rounded-lg border border-border bg-card p-6 shadow-sm"
      >
        <div className="space-y-2">
          <Label htmlFor="current-password">Mevcut Şifre</Label>
          <Input
            id="current-password"
            type="password"
            value={currentPassword}
            onChange={(event) => setCurrentPassword(event.target.value)}
            autoComplete="current-password"
            required
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="new-password">Yeni Şifre</Label>
          <Input
            id="new-password"
            type="password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            autoComplete="new-password"
            required
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="confirm-password">Yeni Şifre Tekrar</Label>
          <Input
            id="confirm-password"
            type="password"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            autoComplete="new-password"
            required
          />
        </div>

        <div className="flex justify-end">
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Güncelleniyor...
              </>
            ) : (
              'Şifreyi Güncelle'
            )}
          </Button>
        </div>
      </form>
    </div>
  );
}
