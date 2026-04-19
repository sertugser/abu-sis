import { useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { AlertCircle, BarChart3, Info, Lock } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { hasAnyRole } from '../lib/roles';
import { useInstructorSections, useInstructorTerms } from '../hooks/useInstructorSections';
import {
  useActiveEvaluationPeriod,
  useDepartmentEvaluationSummary,
  useSectionEvaluationResults,
} from '../hooks/useCourseEvaluation';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
} from '../hooks/useAdminAcademicStructure';
import { useAdminTerms } from '../hooks/useAdminTerms';
import ResultChart from '../components/course-evaluation/ResultChart';
import type { QuestionResult, SectionEvaluationResult } from '../types/courseEvaluation';

const MANAGEMENT_ROLES = ['SystemAdministrator', 'Dean', 'DepartmentManager'] as const;
const TEACHING_ROLES = ['Instructor', 'AcademicAdvisor'] as const;

export default function EvaluationResultsPage() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];

  const isManagement = hasAnyRole(roles, MANAGEMENT_ROLES);
  const isTeaching = hasAnyRole(roles, TEACHING_ROLES);

  const [mode, setMode] = useState<'management' | 'instructor'>(
    isManagement ? 'management' : 'instructor'
  );

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-heading text-xl font-bold">Değerlendirme Sonuçları</h1>
        <p className="text-sm text-muted-foreground">
          Öğrenci değerlendirme anket sonuçları (yanıtlar anonimdir).
        </p>
      </div>

      {isManagement && isTeaching ? (
        <Tabs value={mode} onValueChange={(v) => setMode(v as 'management' | 'instructor')}>
          <TabsList>
            <TabsTrigger value="management">Bölüm Özeti</TabsTrigger>
            <TabsTrigger value="instructor">Kendi Derslerim</TabsTrigger>
          </TabsList>
          <TabsContent value="management" className="mt-4">
            <ManagementView />
          </TabsContent>
          <TabsContent value="instructor" className="mt-4">
            <InstructorView />
          </TabsContent>
        </Tabs>
      ) : isManagement ? (
        <ManagementView />
      ) : (
        <InstructorView />
      )}
    </div>
  );
}

// ─── YÖNETİM GÖRÜNÜMÜ (Admin / Dean / Bölüm Başkanı) ─────────────────

function ManagementView() {
  const { data: faculties = [], isLoading: facultiesLoading } = useAdminFaculties();
  const { data: terms = [] } = useAdminTerms();
  const { data: activePeriodResponse } = useActiveEvaluationPeriod();

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [selectedDepartmentId, setSelectedDepartmentId] = useState<number | null>(null);
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);

  const { data: departments = [] } = useAdminDepartmentsByStructure(
    selectedFacultyId ?? undefined
  );

  // Default dönem: önce açık değerlendirme dönemi, sonra aktif akademik dönem
  const defaultTermId = useMemo(() => {
    if (selectedTermId) return selectedTermId;
    if (activePeriodResponse?.isOpen && activePeriodResponse.period) {
      return activePeriodResponse.period.termId;
    }
    return terms.find((t) => t.isActive)?.id ?? null;
  }, [selectedTermId, activePeriodResponse, terms]);

  const { data: summary, isLoading: summaryLoading } = useDepartmentEvaluationSummary(
    selectedDepartmentId,
    defaultTermId ?? undefined
  );

  if (facultiesLoading) {
    return <Skeleton className="h-96 rounded-xl" />;
  }

  if (faculties.length === 0) {
    return (
      <Alert>
        <Info className="h-4 w-4" />
        <AlertDescription>Fakülte kaydı bulunamadı.</AlertDescription>
      </Alert>
    );
  }

  // Dönemleri yeniden-eskiye sırala
  const sortedTerms = [...terms].sort((a, b) => {
    if (a.year !== b.year) return b.year - a.year;
    return a.semester.localeCompare(b.semester);
  });

  return (
    <div className="space-y-5">
      <Card>
        <CardContent className="p-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label className="text-xs font-semibold uppercase text-muted-foreground tracking-wide mb-1 block">
                Dönem
              </label>
              <Select
                value={defaultTermId?.toString() ?? ''}
                onValueChange={(v) => setSelectedTermId(Number(v))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Dönem seçiniz" />
                </SelectTrigger>
                <SelectContent>
                  {sortedTerms.map((t) => (
                    <SelectItem key={t.id} value={t.id.toString()}>
                      {t.name}
                      {t.isActive ? ' (Aktif)' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <label className="text-xs font-semibold uppercase text-muted-foreground tracking-wide mb-1 block">
                Fakülte
              </label>
              <Select
                value={selectedFacultyId?.toString() ?? ''}
                onValueChange={(v) => {
                  setSelectedFacultyId(Number(v));
                  setSelectedDepartmentId(null);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Fakülte seçiniz" />
                </SelectTrigger>
                <SelectContent>
                  {faculties.map((f) => (
                    <SelectItem key={f.id} value={f.id.toString()}>
                      {f.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <label className="text-xs font-semibold uppercase text-muted-foreground tracking-wide mb-1 block">
                Bölüm
              </label>
              <Select
                value={selectedDepartmentId?.toString() ?? ''}
                onValueChange={(v) => setSelectedDepartmentId(Number(v))}
                disabled={!selectedFacultyId || departments.length === 0}
              >
                <SelectTrigger>
                  <SelectValue
                    placeholder={
                      !selectedFacultyId
                        ? 'Önce fakülte seçiniz'
                        : departments.length === 0
                        ? 'Bu fakültede bölüm yok'
                        : 'Bölüm seçiniz'
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {departments.map((d) => (
                    <SelectItem key={d.id} value={d.id.toString()}>
                      {d.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      {!selectedDepartmentId ? (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertDescription>
            Sonuçları görmek için yukarıdan dönem, fakülte ve bölüm seçiniz.
          </AlertDescription>
        </Alert>
      ) : summaryLoading ? (
        <Skeleton className="h-96 rounded-xl" />
      ) : !summary ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Bölüm özeti yüklenemedi.</AlertDescription>
        </Alert>
      ) : (
        <DepartmentSummary
          summary={summary}
          onShowSection={(sectionId) => setSelectedSectionId(sectionId)}
        />
      )}

      <Dialog
        open={selectedSectionId != null}
        onOpenChange={(open) => {
          if (!open) setSelectedSectionId(null);
        }}
      >
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Ders Değerlendirme Detayı</DialogTitle>
          </DialogHeader>
          {selectedSectionId != null && (
            <SectionDetail sectionId={selectedSectionId} />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function DepartmentSummary({
  summary,
  onShowSection,
}: {
  summary: {
    departmentName: string;
    termName: string;
    totalSections: number;
    evaluatedSections: number;
    overallResponseRate: number;
    averageRating: number | null;
    sectionSummaries: Array<{
      courseSectionId: number;
      courseCode: string;
      courseName: string;
      instructorName: string;
      responseCount: number;
      enrolledCount: number;
      responseRate: number;
      averageRating: number | null;
      isVisible: boolean;
    }>;
  };
  onShowSection: (sectionId: number) => void;
}) {
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <StatCard
          label="Bölüm"
          value={summary.departmentName}
          sub={summary.termName}
          accent="border-l-indigo-500"
        />
        <StatCard
          label="Değerlendirilen Ders"
          value={`${summary.evaluatedSections} / ${summary.totalSections}`}
          sub="Katılım sağlanan / Toplam"
          accent="border-l-blue-500"
        />
        <StatCard
          label="Genel Katılım"
          value={`%${summary.overallResponseRate.toFixed(1)}`}
          sub="Yanıt oranı"
          accent="border-l-amber-500"
        />
        <StatCard
          label="Ortalama Puan"
          value={summary.averageRating != null ? summary.averageRating.toFixed(2) : '—'}
          sub="5 üzerinden"
          accent="border-l-emerald-500"
        />
      </div>

      <Card>
        <CardContent className="p-0">
          {summary.sectionSummaries.length === 0 ? (
            <div className="p-6 text-center text-sm text-muted-foreground">
              Bu bölümde değerlendirmeye açık ders bulunmuyor.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ders</TableHead>
                  <TableHead>Öğretim Üyesi</TableHead>
                  <TableHead className="text-center">Katılım</TableHead>
                  <TableHead className="text-center">Ort. Puan</TableHead>
                  <TableHead className="text-right">Detay</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {summary.sectionSummaries.map((s) => (
                  <TableRow key={s.courseSectionId}>
                    <TableCell>
                      <div className="flex flex-col">
                        <span className="font-mono text-sm">{s.courseCode}</span>
                        <span className="text-xs text-muted-foreground">{s.courseName}</span>
                      </div>
                    </TableCell>
                    <TableCell className="text-sm">{s.instructorName}</TableCell>
                    <TableCell className="text-center text-sm">
                      {s.responseCount}/{s.enrolledCount}
                      <span className="text-xs text-muted-foreground ml-1">
                        (%{s.responseRate.toFixed(0)})
                      </span>
                    </TableCell>
                    <TableCell className="text-center">
                      {s.isVisible && s.averageRating != null ? (
                        <Badge variant="secondary" className="font-mono">
                          {s.averageRating.toFixed(2)}
                        </Badge>
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      {s.isVisible ? (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => onShowSection(s.courseSectionId)}
                        >
                          Gör
                        </Button>
                      ) : (
                        <Badge variant="outline" className="text-xs">
                          Yetersiz
                        </Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function SectionDetail({ sectionId }: { sectionId: number }) {
  const { data: results, isLoading } = useSectionEvaluationResults(sectionId);

  if (isLoading) {
    return <Skeleton className="h-96 rounded-xl" />;
  }

  if (!results) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Sonuçlar yüklenemedi.</AlertDescription>
      </Alert>
    );
  }

  return <ResultsView results={results} />;
}

// ─── ÖĞRETİM ÜYESİ GÖRÜNÜMÜ ──────────────────────────────────────────

function InstructorView() {
  const { data: terms = [], isLoading: termsLoading } = useInstructorTerms();
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);

  // Varsayılan dönem: önce aktif dönem (instructor'ın dersi varsa), yoksa en son dönem
  const effectiveTermId = useMemo(() => {
    if (selectedTermId) return selectedTermId;
    const activeTerm = terms.find((t) => t.isActive);
    if (activeTerm) return activeTerm.id;
    return terms[0]?.id ?? null;
  }, [selectedTermId, terms]);

  const { data: sectionsData, isLoading: sectionsLoading } = useInstructorSections(effectiveTermId);
  const sections = sectionsData?.sections ?? [];

  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);
  // Dönem değişince section seçimini sıfırla
  const activeSectionId = selectedSectionId ?? sections[0]?.sectionId ?? null;

  const { data: results, isLoading: resultsLoading } = useSectionEvaluationResults(activeSectionId);

  if (termsLoading) {
    return <Skeleton className="h-96 rounded-xl" />;
  }

  if (terms.length === 0) {
    return (
      <Alert>
        <Info className="h-4 w-4" />
        <AlertDescription>Henüz hiçbir dönemde ders vermemişsiniz.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardContent className="p-4 space-y-4">
          <div>
            <label className="text-xs font-semibold uppercase text-muted-foreground tracking-wide mb-1 block">
              Dönem
            </label>
            <Select
              value={effectiveTermId?.toString() ?? ''}
              onValueChange={(v) => {
                setSelectedTermId(Number(v));
                setSelectedSectionId(null);
              }}
            >
              <SelectTrigger className="md:max-w-sm">
                <SelectValue placeholder="Dönem seçiniz" />
              </SelectTrigger>
              <SelectContent>
                {terms.map((t) => (
                  <SelectItem key={t.id} value={t.id.toString()}>
                    {t.name}
                    {t.isActive ? ' (Aktif)' : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {sectionsLoading ? (
            <Skeleton className="h-10" />
          ) : sections.length === 0 ? (
            <p className="text-sm text-muted-foreground">Bu dönemde ders bulunamadı.</p>
          ) : (
            <div>
              <p className="text-xs font-semibold uppercase text-muted-foreground tracking-wide mb-2">Ders Seç</p>
              <div className="flex flex-wrap gap-2">
                {sections.map((s) => (
                  <button
                    key={s.sectionId}
                    onClick={() => setSelectedSectionId(s.sectionId)}
                    className={`px-3 py-1.5 text-sm rounded-md border transition-colors ${
                      activeSectionId === s.sectionId
                        ? 'bg-primary text-primary-foreground border-primary'
                        : 'bg-card border-border hover:border-primary/50'
                    }`}
                  >
                    <span className="font-mono mr-1.5">{s.courseCode}</span>
                    <span>{s.courseName}</span>
                  </button>
                ))}
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {sections.length === 0 ? null : resultsLoading ? (
        <Skeleton className="h-96 rounded-xl" />
      ) : !results ? (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Sonuçlar yüklenemedi.</AlertDescription>
        </Alert>
      ) : (
        <ResultsView results={results} />
      )}
    </div>
  );
}

// ─── PAYLAŞILAN BİLEŞENLER ───────────────────────────────────────────

function ResultsView({ results }: { results: SectionEvaluationResult }) {
  if (!results.isVisible) {
    return (
      <Alert variant="destructive" className="bg-amber-50 border-amber-200">
        <Lock className="h-4 w-4 text-amber-600" />
        <AlertDescription className="text-amber-900">
          Anonimlik koruması için en az 5 yanıt alınmadan sonuçlar görüntülenemez. Şu ana kadar{' '}
          <strong>{results.totalResponses}</strong> yanıt alındı.
        </AlertDescription>
      </Alert>
    );
  }

  const courseQuestions = results.questionResults.filter((q) => q.category === 'Course');
  const instructorQuestions = results.questionResults.filter((q) => q.category === 'Instructor');

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <StatCard
          label="Toplam Yanıt"
          value={`${results.totalResponses} / ${results.enrolledCount}`}
          sub={`Katılım: %${results.responseRate.toFixed(1)}`}
          accent="border-l-blue-500"
        />
        <StatCard
          label="Genel Ortalama"
          value={results.overallAverage != null ? results.overallAverage.toFixed(2) : '—'}
          sub="5 üzerinden"
          accent="border-l-emerald-500"
        />
        <StatCard
          label="Ders"
          value={results.courseCode}
          sub={results.courseName}
          accent="border-l-indigo-500"
        />
      </div>

      {courseQuestions.length > 0 && (
        <CategorySection title="Ders Hakkında" questions={courseQuestions} total={results.totalResponses} />
      )}
      {instructorQuestions.length > 0 && (
        <CategorySection title="Öğretim Üyesi Hakkında" questions={instructorQuestions} total={results.totalResponses} />
      )}
    </div>
  );
}

function StatCard({
  label,
  value,
  sub,
  accent,
}: {
  label: string;
  value: string;
  sub: string;
  accent: string;
}) {
  return (
    <Card className={`border-l-4 ${accent}`}>
      <CardContent className="p-4">
        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">{label}</p>
        <p className="text-lg font-bold mt-1">{value}</p>
        <p className="text-xs text-muted-foreground">{sub}</p>
      </CardContent>
    </Card>
  );
}

function CategorySection({
  title,
  questions,
  total,
}: {
  title: string;
  questions: QuestionResult[];
  total: number;
}) {
  return (
    <Card>
      <CardContent className="p-5 space-y-4">
        <div className="flex items-center gap-2">
          <BarChart3 className="h-4 w-4 text-primary" />
          <h3 className="font-heading text-sm font-bold uppercase tracking-wide">{title}</h3>
        </div>
        <div className="space-y-5">
          {questions.map((q) => (
            <QuestionResultBlock key={q.questionId} question={q} total={total} />
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

function QuestionResultBlock({ question, total }: { question: QuestionResult; total: number }) {
  if (question.questionType === 'Rating') {
    return (
      <div>
        <div className="flex items-start justify-between gap-4 mb-2">
          <p className="text-sm font-medium flex-1">{question.questionText}</p>
          {question.averageRating != null && (
            <Badge variant="secondary" className="font-mono">
              {question.averageRating.toFixed(2)} / 5
            </Badge>
          )}
        </div>
        {question.ratingDistribution && (
          <ResultChart distribution={question.ratingDistribution} totalResponses={total} />
        )}
      </div>
    );
  }

  return <TextResponses question={question} />;
}

function TextResponses({ question }: { question: QuestionResult }) {
  const responses = useMemo(
    () => question.textResponses?.filter((t) => t.trim().length > 0) ?? [],
    [question.textResponses]
  );

  return (
    <div>
      <p className="text-sm font-medium mb-2">{question.questionText}</p>
      {responses.length === 0 ? (
        <p className="text-xs text-muted-foreground italic">Yanıt yok.</p>
      ) : (
        <div className="space-y-2">
          {responses.map((text, idx) => (
            <div key={idx} className="bg-muted/40 border border-border/40 rounded-md px-3 py-2 text-sm">
              {text}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
