import { useState, type FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import { AlertCircle, CheckCircle2, Clock, QrCode } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { useQrAttendanceSession, useSubmitQrAttendance } from '../hooks/useAttendance';

export default function QrAttendancePage() {
  const { token } = useParams<{ token: string }>();
  const { data, isLoading, isError } = useQrAttendanceSession(token);
  const submitMut = useSubmitQrAttendance(token);
  const [studentNumber, setStudentNumber] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState(false);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setMessage(null);

    submitMut.mutate(
      { studentNumber, firstName, lastName },
      {
        onSuccess: response => {
          setIsSuccess(true);
          setMessage(response.message);
        },
        onError: error => {
          const apiError = error as { response?: { data?: { message?: string; error?: string } } };
          setIsSuccess(false);
          setMessage(apiError.response?.data?.message ?? apiError.response?.data?.error ?? 'Yoklama kaydı alınamadı.');
        },
      }
    );
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
            <p className="text-sm text-slate-600">Öğrenci girişi</p>
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
            <AlertDescription>Yoklama formu bulunamadı.</AlertDescription>
          </Alert>
        )}

        {data && (
          <Card>
            <CardContent className="space-y-4 p-5">
              <div className="space-y-2">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant={data.isActive ? 'success' : 'secondary'}>
                    {data.isActive ? 'Aktif' : 'Kapalı'}
                  </Badge>
                  {data.expiresAt && (
                    <Badge variant="outline" className="gap-1.5">
                      <Clock className="h-3.5 w-3.5" />
                      {new Date(data.expiresAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}
                    </Badge>
                  )}
                </div>
                <div>
                  <p className="font-mono text-sm font-semibold text-[#003366]">{data.courseCode}</p>
                  <h2 className="text-lg font-bold text-slate-950">{data.courseName}</h2>
                  <p className="text-sm text-slate-600">Şube {data.sectionNumber} / {data.weekNumber}. Hafta</p>
                </div>
              </div>

              {!data.isActive ? (
                <Alert>
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>{data.message}</AlertDescription>
                </Alert>
              ) : isSuccess ? (
                <Alert className="border-emerald-200 bg-emerald-50 text-emerald-800">
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertDescription>{message ?? 'Yoklama kaydınız alındı.'}</AlertDescription>
                </Alert>
              ) : (
                <form onSubmit={handleSubmit} className="space-y-4">
                  <div className="space-y-2">
                    <Label>Okul Numarası</Label>
                    <Input
                      value={studentNumber}
                      onChange={event => setStudentNumber(event.target.value)}
                      required
                      autoComplete="off"
                      inputMode="numeric"
                    />
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <div className="space-y-2">
                      <Label>Ad</Label>
                      <Input
                        value={firstName}
                        onChange={event => setFirstName(event.target.value)}
                        required
                        autoComplete="given-name"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label>Soyad</Label>
                      <Input
                        value={lastName}
                        onChange={event => setLastName(event.target.value)}
                        required
                        autoComplete="family-name"
                      />
                    </div>
                  </div>

                  {message && (
                    <Alert variant="destructive">
                      <AlertCircle className="h-4 w-4" />
                      <AlertDescription>{message}</AlertDescription>
                    </Alert>
                  )}

                  <Button type="submit" className="w-full" disabled={submitMut.isPending}>
                    {submitMut.isPending ? 'Gönderiliyor...' : 'Yoklamaya Katıl'}
                  </Button>
                </form>
              )}
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}
