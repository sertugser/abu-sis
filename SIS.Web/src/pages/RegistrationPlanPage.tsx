import { useState } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import ValidationErrors from '../components/registration/ValidationErrors';
import RegistrationStatusBadge from '../components/registration/RegistrationStatusBadge';
import { useMyRegistrations } from '../hooks/useMyRegistrations';
import { useDropCourse } from '../hooks/useDropCourse';
import { useSubmitRegistration } from '../hooks/useSubmitRegistration';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useStudentPoolStatus } from '../hooks/useStudentPoolStatus';
import { ApiError } from '../lib/apiError';
import type { ValidationError } from '../types/registration';
import { Link } from 'react-router-dom';
import { AlertCircle, Trash2, Send, CheckCircle2, ClipboardList, BookOpen, Loader2 } from 'lucide-react';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';

export default function RegistrationPlanPage() {
  const { data: registrations, isLoading } = useMyRegistrations();
  const { data: student } = useStudentInfo();
  const { data: poolStatus = [] } = useStudentPoolStatus(student?.id ?? null);
  const dropCourse = useDropCourse();
  const submitRegistration = useSubmitRegistration();
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);
  const [validationWarnings, setValidationWarnings] = useState<ValidationError[]>([]);
  const [submitSuccess, setSubmitSuccess] = useState(false);
  // Faz C C3: yumuşak warning gate — eksik slot varsa onay dialog'u
  const [emptySlotsConfirmOpen, setEmptySlotsConfirmOpen] = useState(false);

  const planItems = registrations?.filter(r => r.status === 'Draft' || r.status === 'Rejected') || [];
  const rejectedItems = registrations?.filter(r => r.status === 'Rejected') || [];
  const advisorNote = rejectedItems.length > 0 ? rejectedItems[0]?.advisorNote : undefined;
  const totalEcts = planItems.reduce((sum, r) => sum + r.ects, 0);
  // Backend EctsLimit non-nullable; loading durumunda 0 göster.
  const ectsLimit = student?.ectsLimit ?? 0;
  const ectsPercent = ectsLimit > 0 ? Math.min((totalEcts / ectsLimit) * 100, 100) : 0;

  // Eksik (boş) slot'lar — fulfillment durumuna göre
  const emptySlots = poolStatus.filter(p => !p.isFulfilled);

  const handleRemove = (registrationId: number) => {
    setValidationErrors([]);
    setValidationWarnings([]);
    dropCourse.mutate(registrationId, {
      onError: (error) => {
        if (error instanceof ApiError && error.validationErrors.length > 0) {
          setValidationErrors(error.validationErrors);
          setValidationWarnings(error.validationWarnings);
        } else {
          setValidationErrors([{ rule: 'System', message: error instanceof Error ? error.message : 'Ders kaldirilamadi.' }]);
        }
      },
    });
  };

  // Asıl submit logic — dialog onayından sonra veya direkt çağrılır
  const performSubmit = () => {
    setValidationErrors([]);
    setValidationWarnings([]);
    setSubmitSuccess(false);
    const sectionIds = planItems.map(r => r.courseSectionId);
    submitRegistration.mutate(sectionIds, {
      onSuccess: (data) => {
        if (data.success) {
          setSubmitSuccess(true);
          setValidationErrors([]);
          setValidationWarnings(data.validationResult?.warnings ?? []);
        }
      },
      onError: (error) => {
        if (error instanceof ApiError && error.validationErrors.length > 0) {
          setValidationErrors(error.validationErrors);
          setValidationWarnings(error.validationWarnings);
        } else {
          setValidationErrors([{ rule: 'System', message: error instanceof Error ? error.message : 'Kayit sirasinda bir hata olustu.' }]);
        }
      },
    });
  };

  const handleSubmit = () => {
    // Karar 4=b: yumuşak gate — eksik slot varsa onay dialogu, yoksa direkt submit.
    // Karar #1: bloke YOK — danışman süzer; sadece bilgilendirme.
    if (emptySlots.length > 0) {
      setEmptySlotsConfirmOpen(true);
      return;
    }
    performSubmit();
  };

  const handleConfirmSubmitDespiteEmptySlots = () => {
    setEmptySlotsConfirmOpen(false);
    performSubmit();
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        {[1,2,3].map(i => <Skeleton key={i} className="h-16 rounded-xl" />)}
        <Skeleton className="h-32 rounded-xl" />
      </div>
    );
  }

  return (
    <div className="space-y-6 ">
      {submitSuccess && (
        <Alert variant="success">
          <CheckCircle2 className="h-4 w-4" />
          <AlertDescription>
            Kayıt başarıyla tamamlandı!{' '}
            <Link to="/ders-kayit" className="underline font-semibold hover:no-underline">Ders Kayıt sayfasına dönün</Link>
          </AlertDescription>
        </Alert>
      )}

      {rejectedItems.length > 0 && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            <strong>Kayıt planınız danışmanınız tarafından reddedildi.</strong>
            {advisorNote && (
              <p className="mt-1 text-sm">Danışman notu: {advisorNote}</p>
            )}
            <p className="mt-1 text-sm">Planınızı düzenleyip tekrar gönderebilirsiniz.</p>
          </AlertDescription>
        </Alert>
      )}

      {(validationErrors.length > 0 || validationWarnings.length > 0) && (
        <ValidationErrors errors={validationErrors} warnings={validationWarnings} />
      )}

      {planItems.length > 0 ? (
        <>
          {/* Plan items */}
          <div className="bg-card rounded-xl border border-border overflow-hidden">
            <div className="divide-y divide-border">
              {planItems.map(item => (
                <div key={item.registrationId} className="flex items-center justify-between p-4 hover:bg-muted/30 transition-colors">
                  <div className="flex items-center gap-4 flex-1 min-w-0">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-mono text-sm font-semibold text-primary">{item.courseCode}</span>
                        <span className="text-foreground font-medium">{item.courseName}</span>
                      </div>
                    </div>
                    <Badge variant="default" className="shrink-0">{item.ects} ECTS</Badge>
                    <RegistrationStatusBadge status={item.status} />
                  </div>
                  {(item.status === 'Draft' || item.status === 'Rejected') && (
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => handleRemove(item.registrationId)}
                      disabled={dropCourse.isPending}
                      className="shrink-0 text-muted-foreground hover:text-destructive hover:bg-destructive/10"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              ))}
            </div>
          </div>

          {/* Summary */}
          <Card>
            <CardContent className="p-6">
            <div className="flex items-center justify-between mb-4">
              <div>
                <p className="text-sm text-muted-foreground">{planItems.length} ders secildi</p>
                <div className="flex items-baseline gap-1.5 mt-1">
                  <span className="font-heading text-3xl font-extrabold text-foreground">{totalEcts}</span>
                  <span className="text-muted-foreground font-medium">/ {ectsLimit} ECTS</span>
                </div>
              </div>
              <Button
                size="lg"
                onClick={handleSubmit}
                disabled={submitRegistration.isPending || planItems.length === 0}
                className="gap-2 rounded-xl"
              >
                {submitRegistration.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Gönderiliyor...
                  </>
                ) : (
                  <>
                    <Send className="h-4 w-4" />
                    Kaydı Onayla
                  </>
                )}
              </Button>
            </div>
            <Progress value={ectsPercent} indicatorClassName={ectsPercent > 90 ? 'bg-destructive' : 'bg-primary'} className="h-2.5" />
          </CardContent>
          </Card>
        </>
      ) : (
        <div className="text-center py-16">
          <ClipboardList className="h-12 w-12 text-muted-foreground/40 mx-auto mb-5" />
          <h3 className="font-heading text-lg font-semibold text-foreground">Planınız boş</h3>
          <p className="text-muted-foreground mt-1 mb-6">Ders kataloğundan ders ekleyerek başlayabilirsiniz.</p>
          <Button asChild className="gap-2 rounded-xl">
            <Link to="/ders-kayit">
              <BookOpen className="h-4 w-4" />
              Ders Kayıt Sayfasına Git
            </Link>
          </Button>
        </div>
      )}

      {/* ── Eksik slot onay dialog (Faz C C3) ── */}
      <AlertDialog open={emptySlotsConfirmOpen} onOpenChange={setEmptySlotsConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Eksik müfredat slotları var</AlertDialogTitle>
            <AlertDialogDescription className="space-y-2">
              <span className="block">
                Aşağıdaki seçmeli grup slotları için yeterli ders seçmediniz. Yine de
                kaydı gönderebilirsiniz; eksiklikler danışmanınızla görüşülerek
                değerlendirilecektir.
              </span>
              <span className="block max-h-40 overflow-y-auto rounded-md bg-muted/40 p-2 text-xs space-y-0.5">
                {emptySlots.map(p => (
                  <span key={p.poolId} className="block">
                    <span className="font-mono font-semibold">{p.poolCode}</span>
                    {p.slotName && <span className="text-violet-700"> / {p.slotName}</span>}
                    <span className="text-muted-foreground">
                      {' — '}
                      {p.requiredCourseCount > 0 && `${p.completedCourseCount}/${p.requiredCourseCount} ders`}
                      {p.requiredCourseCount > 0 && p.requiredEcts > 0 && ', '}
                      {p.requiredEcts > 0 && `${p.earnedEcts}/${p.requiredEcts} AKTS`}
                    </span>
                  </span>
                ))}
              </span>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmSubmitDespiteEmptySlots}>
              Yine de gönder
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
