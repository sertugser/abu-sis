import { useEffect, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { AlertTriangle, Ban, BookOpen, Loader2, ShieldCheck, ShieldOff, Trash2, UserRoundSearch } from 'lucide-react';
import { useAdvisorConstraints, useAdvisorStudents, useDeleteAdvisorConstraint, useUpdateAdvisorConstraint, useUpsertAdvisorConstraint } from '../hooks/useAdvisorConstraints';
import { useStudentEligibility } from '../hooks/useStudentEligibility';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { getApiErrorMessage } from '../lib/apiError';
import type { AdvisorConstraintType } from '../types/advisorConstraint';

export default function AdvisorCourseConstraintsPage() {
  const { data: advisorStudents, isLoading: studentsLoading, error: studentsError } = useAdvisorStudents();
  const { data: activeTerm } = useActiveTerm();
  const [selectedStudentId, setSelectedStudentId] = useState<number | null>(null);
  const [selectedCourseId, setSelectedCourseId] = useState<number | null>(null);
  const [constraintType, setConstraintType] = useState<AdvisorConstraintType>('Warning');
  const [note, setNote] = useState('');
  const [courseSearch, setCourseSearch] = useState('');
  const [feedback, setFeedback] = useState<string | null>(null);

  useEffect(() => {
    if (!selectedStudentId && advisorStudents?.length) {
      setSelectedStudentId(advisorStudents[0].studentId);
    }
  }, [advisorStudents, selectedStudentId]);

  const constraintsQuery = useAdvisorConstraints(selectedStudentId, activeTerm?.id);
  const eligibilityQuery = useStudentEligibility(selectedStudentId);
  const createConstraint = useUpsertAdvisorConstraint();
  const updateConstraint = useUpdateAdvisorConstraint();
  const deleteConstraint = useDeleteAdvisorConstraint();

  const availableCourses = (eligibilityQuery.data ?? [])
    .filter(course => {
      if (!courseSearch.trim()) return true;
      const q = courseSearch.trim().toLocaleLowerCase('tr');
      return course.courseCode.toLocaleLowerCase('tr').includes(q)
        || course.courseName.toLocaleLowerCase('tr').includes(q);
    })
    .sort((a, b) => {
      if (a.isEligible !== b.isEligible) return a.isEligible ? -1 : 1;
      return a.courseCode.localeCompare(b.courseCode, 'tr');
    });

  const selectedStudent = advisorStudents?.find(student => student.studentId === selectedStudentId) ?? null;

  const handleCreateConstraint = async () => {
    if (!selectedStudentId || !selectedCourseId) return;

    setFeedback(null);
    try {
      const result = await createConstraint.mutateAsync({
        studentId: selectedStudentId,
        courseId: selectedCourseId,
        termId: activeTerm?.id,
        constraintType,
        note: note.trim() || undefined,
      });
      setNote('');
      setFeedback(`${result.courseCode} icin ${result.constraintType === 'Block' ? 'blokaj' : 'uyari'} kaydedildi.`);
    } catch (error) {
      setFeedback(getApiErrorMessage(error, 'Kisit kaydedilemedi.'));
    }
  };

  const handleUpdateItem = async (
    id: number,
    request: { constraintType: AdvisorConstraintType; note?: string; isActive: boolean },
    successMessage: string
  ) => {
    setFeedback(null);
    try {
      await updateConstraint.mutateAsync({ id, request });
      setFeedback(successMessage);
    } catch (error) {
      setFeedback(getApiErrorMessage(error, 'Kisit guncellenemedi.'));
    }
  };

  const handleDeleteItem = async (id: number) => {
    setFeedback(null);
    try {
      await deleteConstraint.mutateAsync({ id });
      setFeedback('Kisit silindi.');
    } catch (error) {
      setFeedback(getApiErrorMessage(error, 'Kisit silinemedi.'));
    }
  };

  const anyMutationPending = createConstraint.isPending || updateConstraint.isPending || deleteConstraint.isPending;

  return (
    <div className="space-y-6">
      <Card className="overflow-hidden border-none shadow-sm">
        <div className="bg-[#003366] px-6 py-6 text-white">
          <div className="flex flex-col gap-2 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <div className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.08em]">
                <ShieldCheck className="h-3.5 w-3.5" />
                Danisman Araci
              </div>
              <h1 className="mt-3 font-heading text-2xl font-extrabold tracking-tight">Ders kisiti yonetimi</h1>
              <p className="mt-1 max-w-3xl text-sm text-white/75">
                Ogrencinin bu donem almasini istemedigin dersleri bloklayabilir veya uyarili gecis kurali koyabilirsin.
              </p>
            </div>
            {activeTerm && (
              <Badge variant="info" className="w-fit border-white/20 bg-white/10 text-white">
                Aktif donem: {activeTerm.name}
              </Badge>
            )}
          </div>
        </div>
      </Card>

      {studentsError && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Ogrenci listesi yuklenemedi</AlertTitle>
          <AlertDescription>{getApiErrorMessage(studentsError, 'Danisman ogrencileri getirilemedi.')}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle>Yeni kisit ekle</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {studentsLoading ? (
              <Skeleton className="h-64 rounded-xl" />
            ) : !advisorStudents?.length ? (
              <Alert>
                <UserRoundSearch className="h-4 w-4" />
                <AlertTitle>Danisman ogrencisi bulunamadi</AlertTitle>
                <AlertDescription>
                  Bu hesapla eslesen aktif ogrenci yok. Farkli bir danisman hesabi ile deneyebilirsin.
                </AlertDescription>
              </Alert>
            ) : (
              <>
                <div className="space-y-2">
                  <p className="text-sm font-medium text-foreground">Ogrenci sec</p>
                  <Select
                    value={selectedStudentId ? String(selectedStudentId) : undefined}
                    onValueChange={value => {
                      setSelectedStudentId(Number(value));
                      setSelectedCourseId(null);
                      setFeedback(null);
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Danisman ogrencisi sec" />
                    </SelectTrigger>
                    <SelectContent>
                      {advisorStudents.map(student => (
                        <SelectItem key={student.studentId} value={String(student.studentId)}>
                          {student.studentNumber} - {student.studentName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {selectedStudent && (
                  <div className="rounded-2xl border border-border bg-muted/20 p-4 text-sm">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <p className="font-semibold text-foreground">{selectedStudent.studentName}</p>
                        <p className="text-muted-foreground">{selectedStudent.studentNumber} • {selectedStudent.programName}</p>
                      </div>
                      <div className="text-right">
                        <p className="font-semibold text-foreground">{selectedStudent.cgpa.toFixed(2)}</p>
                        <p className="text-xs text-muted-foreground">{selectedStudent.completedSemesters}. donem</p>
                      </div>
                    </div>
                  </div>
                )}

                <div className="space-y-2">
                  <p className="text-sm font-medium text-foreground">Ders ara</p>
                  <Input
                    value={courseSearch}
                    onChange={event => setCourseSearch(event.target.value)}
                    placeholder="Kod veya ders adi yaz..."
                  />
                </div>

                <div className="space-y-2">
                  <p className="text-sm font-medium text-foreground">Ders sec</p>
                  <Select
                    value={selectedCourseId ? String(selectedCourseId) : undefined}
                    onValueChange={value => setSelectedCourseId(Number(value))}
                    disabled={eligibilityQuery.isLoading || !availableCourses.length}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={eligibilityQuery.isLoading ? 'Dersler yukleniyor...' : 'Ders sec'} />
                    </SelectTrigger>
                    <SelectContent>
                      {availableCourses.map(course => (
                        <SelectItem key={course.courseId} value={String(course.courseId)}>
                          {course.courseCode} - {course.courseName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {eligibilityQuery.isError && (
                    <p className="text-sm text-destructive">{getApiErrorMessage(eligibilityQuery.error, 'Ders uygunluk listesi getirilemedi.')}</p>
                  )}
                </div>

                <div className="grid gap-4 sm:grid-cols-2">
                  <button
                    type="button"
                    onClick={() => setConstraintType('Warning')}
                    className={`rounded-2xl border px-4 py-3 text-left transition-colors ${
                      constraintType === 'Warning'
                        ? 'border-amber-300 bg-amber-50'
                        : 'border-border bg-background hover:bg-muted/30'
                    }`}
                  >
                    <div className="flex items-center gap-2">
                      <AlertTriangle className="h-4 w-4 text-amber-500" />
                      <span className="font-semibold text-foreground">Warning</span>
                    </div>
                    <p className="mt-1 text-sm text-muted-foreground">Ogrenci kaydi gorebilir ama uyarili devam eder.</p>
                  </button>

                  <button
                    type="button"
                    onClick={() => setConstraintType('Block')}
                    className={`rounded-2xl border px-4 py-3 text-left transition-colors ${
                      constraintType === 'Block'
                        ? 'border-rose-300 bg-rose-50'
                        : 'border-border bg-background hover:bg-muted/30'
                    }`}
                  >
                    <div className="flex items-center gap-2">
                      <Ban className="h-4 w-4 text-rose-500" />
                      <span className="font-semibold text-foreground">Block</span>
                    </div>
                    <p className="mt-1 text-sm text-muted-foreground">Ders planda ve ankette ogrenciye tamamen kapatilir.</p>
                  </button>
                </div>

                <div className="space-y-2">
                  <p className="text-sm font-medium text-foreground">Not</p>
                  <Textarea
                    value={note}
                    onChange={event => setNote(event.target.value)}
                    placeholder="Ornek: Bu donem once alttan derslerini tamamla."
                    rows={4}
                  />
                </div>

                <Button
                  onClick={handleCreateConstraint}
                  disabled={!selectedStudentId || !selectedCourseId || anyMutationPending}
                  className="w-full gap-2 bg-[#003366] hover:bg-[#002244]"
                >
                  {createConstraint.isPending ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin" />
                      Kaydediliyor...
                    </>
                  ) : (
                    <>
                      <ShieldCheck className="h-4 w-4" />
                      Kisiti Kaydet
                    </>
                  )}
                </Button>

                {feedback && (
                  <Alert variant={feedback.includes('kaydedildi') || feedback.includes('silindi') || feedback.includes('guncellendi') ? 'success' : 'destructive'}>
                    <AlertDescription>{feedback}</AlertDescription>
                  </Alert>
                )}
              </>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle>Mevcut kisitlar</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {constraintsQuery.isLoading ? (
              <Skeleton className="h-72 rounded-xl" />
            ) : constraintsQuery.isError ? (
              <Alert variant="destructive">
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>{getApiErrorMessage(constraintsQuery.error, 'Kisit listesi yuklenemedi.')}</AlertDescription>
              </Alert>
            ) : !constraintsQuery.data?.length ? (
              <Alert>
                <ShieldOff className="h-4 w-4" />
                <AlertTitle>Kisit bulunmuyor</AlertTitle>
                <AlertDescription>
                  Secili ogrenci icin aktif donemde tanimli block ya da warning kaydi yok.
                </AlertDescription>
              </Alert>
            ) : (
              constraintsQuery.data.map(item => (
                <div key={item.id} className="rounded-2xl border border-border p-4">
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-mono text-sm font-semibold text-primary">{item.courseCode}</span>
                        <span className="font-medium text-foreground">{item.courseName}</span>
                        <Badge variant={item.constraintType === 'Block' ? 'destructive' : 'warning'}>
                          {item.constraintType}
                        </Badge>
                        <Badge variant={item.isActive ? 'success' : 'secondary'}>
                          {item.isActive ? 'Aktif' : 'Pasif'}
                        </Badge>
                      </div>
                      <p className="text-sm text-muted-foreground">{item.termName}</p>
                      {item.note && <p className="text-sm text-foreground">{item.note}</p>}
                    </div>

                    <div className="flex flex-wrap gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={anyMutationPending}
                        onClick={() => handleUpdateItem(
                          item.id,
                          {
                            constraintType: item.constraintType === 'Block' ? 'Warning' : 'Block',
                            note: item.note ?? undefined,
                            isActive: item.isActive,
                          },
                          'Kisit tipi guncellendi.'
                        )}
                      >
                        {item.constraintType === 'Block' ? 'Warning Yap' : 'Block Yap'}
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={anyMutationPending}
                        onClick={() => handleUpdateItem(
                          item.id,
                          {
                            constraintType: item.constraintType,
                            note: item.note ?? undefined,
                            isActive: !item.isActive,
                          },
                          item.isActive ? 'Kisit pasife alindi.' : 'Kisit tekrar aktive edildi.'
                        )}
                      >
                        {item.isActive ? 'Pasif Yap' : 'Aktif Et'}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={anyMutationPending}
                        onClick={() => handleDeleteItem(item.id)}
                        className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                      >
                        <Trash2 className="mr-1 h-4 w-4" />
                        Sil
                      </Button>
                    </div>
                  </div>
                </div>
              ))
            )}

            {selectedStudent && availableCourses.length > 0 && (
              <div className="rounded-2xl border border-border bg-muted/15 p-4">
                <div className="flex items-center gap-2">
                  <BookOpen className="h-4 w-4 text-primary" />
                  <p className="text-sm font-semibold text-foreground">Uygunluk gorunumu</p>
                </div>
                <div className="mt-3 grid gap-2 md:grid-cols-2">
                  {availableCourses.slice(0, 8).map(course => (
                    <div key={course.courseId} className="rounded-xl border border-border bg-background px-3 py-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-mono text-xs font-semibold text-primary">{course.courseCode}</span>
                        <Badge variant={course.isEligible ? 'success' : 'secondary'}>
                          {course.isEligible ? 'Uygun' : 'Uygun Degil'}
                        </Badge>
                      </div>
                      <p className="mt-1 text-sm text-foreground">{course.courseName}</p>
                      <p className="mt-1 text-xs text-muted-foreground">{course.takeType}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
