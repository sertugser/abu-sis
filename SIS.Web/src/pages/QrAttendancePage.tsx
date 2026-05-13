import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { AlertCircle, CheckCircle2, Clock, LogIn, QrCode } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useAuth } from '../context/AuthContext';
import { useQrAttendanceSession, useSubmitQrAttendance } from '../hooks/useAttendance';

export default function QrAttendancePage() {
  const { token } = useParams<{ token: string }>();
  const navigate = useNavigate();
  const { isAuthenticated, user } = useAuth();
  const { data, isLoading, isError } = useQrAttendanceSession(token);
  const submitMut = useSubmitQrAttendance(token);
  const [message, setMessage] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState(false);
  const [now, setNow] = useState(() => Date.now());

  const remainingMs = data?.expiresAt ? Math.max(0, new Date(data.expiresAt).getTime() - now) : 0;
  const isFormActive = Boolean(data?.isActive && (!data.expiresAt || remainingMs > 0));

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  const handleLogin = () => {
    const returnTo = token ? `/yoklama/qr/${encodeURIComponent(token)}` : '/';
    navigate(`/login?returnTo=${encodeURIComponent(returnTo)}`);
  };

  const handleSubmit = () => {
    setMessage(null);

    submitMut.mutate(undefined, {
      onSuccess: response => {
        setIsSuccess(true);
        setMessage(response.message);
      },
      onError: error => {
        const apiError = error as { response?: { data?: { message?: string; error?: string } } };
        setIsSuccess(false);
        setMessage(apiError.response?.data?.message ?? apiError.response?.data?.error ?? 'Yoklama kaydi alinamadi.');
      },
    });
  };

  return (
    <div className="min-h-screen bg-slate-50 px-4 py-8">
      <div className="mx-auto flex w-full max-w-md flex-col gap-4">
        <div className="flex items-center gap-3">
          <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-[#003366] text-white">
            <QrCode className="h-5 w-5" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-950">Yoklama Formu</h1>
            <p className="text-sm text-slate-600">Ogrenci girisi gerekli</p>
          </div>
        </div>

        {isLoading && (
          <Card>
            <CardContent className="space-y-3 p-5">
              <Skeleton className="h-6 w-40" />
              <Skeleton className="h-4 w-64" />
              <Skeleton className="h-40 w-full" />
            </CardContent>
          </Card>
        )}

        {(isError || !token) && !isLoading && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>Yoklama formu bulunamadi.</AlertDescription>
          </Alert>
        )}

        {data && (
          <Card>
            <CardContent className="space-y-4 p-5">
              <div className="space-y-2">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant={isFormActive ? 'success' : 'secondary'}>
                    {isFormActive ? 'Aktif' : 'Kapali'}
                  </Badge>
                  {data.expiresAt && (
                    <Badge variant="outline" className="gap-1.5">
                      <Clock className="h-3.5 w-3.5" />
                      {isFormActive ? formatRemaining(remainingMs) : 'Sure doldu'}
                    </Badge>
                  )}
                </div>
                <div>
                  <p className="font-mono text-sm font-semibold text-[#003366]">{data.courseCode}</p>
                  <h2 className="text-lg font-bold text-slate-950">{data.courseName}</h2>
                  <p className="text-sm text-slate-600">Sube {data.sectionNumber} / {data.weekNumber}. Hafta</p>
                </div>
              </div>

              {!isFormActive ? (
                <Alert>
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    {data.expiresAt && remainingMs === 0 ? 'Yoklama formu kapali veya suresi dolmus.' : data.message}
                  </AlertDescription>
                </Alert>
              ) : isSuccess ? (
                <Alert className="border-emerald-200 bg-emerald-50 text-emerald-800">
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertDescription>{message ?? 'Yoklama kaydiniz alindi.'}</AlertDescription>
                </Alert>
              ) : !isAuthenticated ? (
                <div className="space-y-4">
                  <Alert>
                    <LogIn className="h-4 w-4" />
                    <AlertDescription>Yoklamaya katilmak icin ogrenci hesabinizla giris yapin.</AlertDescription>
                  </Alert>
                  <Button type="button" className="w-full gap-2" onClick={handleLogin}>
                    <LogIn className="h-4 w-4" />
                    Giris Yap
                  </Button>
                </div>
              ) : (
                <div className="space-y-4">
                  <Alert>
                    <CheckCircle2 className="h-4 w-4" />
                    <AlertDescription>
                      {user?.name ? `${user.name} olarak yoklamaya katilacaksiniz.` : 'Oturumunuzla yoklamaya katilacaksiniz.'}
                    </AlertDescription>
                  </Alert>

                  {message && (
                    <Alert variant="destructive">
                      <AlertCircle className="h-4 w-4" />
                      <AlertDescription>{message}</AlertDescription>
                    </Alert>
                  )}

                  <Button type="button" className="w-full" onClick={handleSubmit} disabled={submitMut.isPending}>
                    {submitMut.isPending ? 'Gonderiliyor...' : 'Yoklamaya Katil'}
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}

function formatRemaining(ms: number) {
  const seconds = Math.ceil(ms / 1000);
  const minutes = Math.floor(seconds / 60);
  const rest = seconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(rest).padStart(2, '0')}`;
}
