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
import type { ValidationError } from '../types/registration';
import { Link } from 'react-router-dom';
import { Trash2, Send, CheckCircle2, ClipboardList, BookOpen, Loader2 } from 'lucide-react';

export default function RegistrationPlanPage() {
  const { data: registrations, isLoading } = useMyRegistrations();
  const { data: student } = useStudentInfo();
  const dropCourse = useDropCourse();
  const submitRegistration = useSubmitRegistration();
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);
  const [submitSuccess, setSubmitSuccess] = useState(false);

  const planItems = registrations?.filter(r => r.status === 'Draft') || [];
  const totalEcts = planItems.reduce((sum, r) => sum + r.ects, 0);
  const ectsLimit = student?.ectsLimit ?? 30;
  const ectsPercent = Math.min((totalEcts / ectsLimit) * 100, 100);

  const handleRemove = (registrationId: number) => {
    dropCourse.mutate(registrationId);
  };

  const handleSubmit = () => {
    setValidationErrors([]);
    setSubmitSuccess(false);
    const sectionIds = planItems.map(r => r.courseSectionId);
    submitRegistration.mutate(sectionIds, {
      onSuccess: (data) => {
        if (data.validationResult && !data.validationResult.isValid) {
          setValidationErrors(data.validationResult.errors);
        } else if (data.success) {
          setSubmitSuccess(true);
          setValidationErrors([]);
        }
      },
      onError: () => {
        setValidationErrors([{ rule: 'System', message: 'Kayıt sırasında bir hata oluştu.' }]);
      },
    });
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

      {validationErrors.length > 0 && <ValidationErrors errors={validationErrors} />}

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
                  <Button
                    variant="ghost"
                    size="icon"
                    onClick={() => handleRemove(item.registrationId)}
                    disabled={dropCourse.isPending}
                    className="shrink-0 text-muted-foreground hover:text-destructive hover:bg-destructive/10"
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
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
    </div>
  );
}
