import { useMemo, useState, type ReactNode } from 'react';
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
import { AlertCircle, BarChart3, Download, Info, Lock } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { hasAnyRole } from '../lib/roles';
import { downloadBlob } from '../lib/download';
import { useInstructorSections, useInstructorTerms } from '../hooks/useInstructorSections';
import {
  useActiveEvaluationPeriod,
  useDepartmentEvaluationSummary,
  useExportDepartmentEvaluationResults,
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
  const { user } = useAuth();
  const isAdmin = hasAnyRole(user?.roles ?? [], ['SystemAdministrator']);

  const { data: faculties = [], isLoading: facultiesLoading } = useAdminFaculties();
  const { data: terms = [] } = useAdminTerms();
  const { data: activePeriodResponse } = useActiveEvaluationPeriod();

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [selectedDepartmentId, setSelectedDepartmentId] = useState<number | null>(null);
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);

  const exportMutation = useExportDepartmentEvaluationResults();

  function handleExport() {
    if (!selectedDepartmentId) return;
    exportMutation.mutate(
      { departmentId: selectedDepartmentId, termId: defaultTermId },
      {
        onSuccess: (blob) => {
          downloadBlob({ blob }, 'degerlendirme-sonuclari.xlsx');
        },
        onError: () => {
          alert('Excel dışa aktarımı başarısız oldu. Lütfen tekrar deneyin.');
        },
      }
    );
  }

  const { data: departments = [] } = useAdminDepartmentsByStructure(
    selectedFacultyId ?? undefined
  );

  // Default dönem: önce açık değerlendirme dönemi, sonra aktif akademik dönem
  const defaultTermId = (() => {
    if (selectedTermId) return selectedTermId;
    if (activePeriodResponse?.isOpen && activePeriodResponse.period) {
      return activePeriodResponse.period.termId;
    }
    return terms.find((t) => t.isActive)?.id ?? null;
  })();

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
    return a.termType.localeCompare(b.termType);
  });

  return (
    <div className="space-y-4">
      <div className="overflow-hidden rounded-xl border border-border bg-white shadow-sm">
        {/* Filter toolbar — 3 dropdowns + Excel button inline */}
        <div className="flex flex-wrap items-end gap-3 px-4 py-3 border-b border-slate-100 bg-slate-50/30">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3 flex-1 min-w-0">
            <FilterField label="Dönem">
              <Select
                value={defaultTermId?.toString() ?? ''}
                onValueChange={(v) => setSelectedTermId(Number(v))}
              >
                <SelectTrigger className="bg-white">
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
            </FilterField>

            <FilterField label="Fakülte">
              <Select
                value={selectedFacultyId?.toString() ?? ''}
                onValueChange={(v) => {
                  setSelectedFacultyId(Number(v));
                  setSelectedDepartmentId(null);
                }}
              >
                <SelectTrigger className="bg-white">
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
            </FilterField>

            <FilterField label="Bölüm">
              <Select
                value={selectedDepartmentId?.toString() ?? ''}
                onValueChange={(v) => setSelectedDepartmentId(Number(v))}
                disabled={!selectedFacultyId || departments.length === 0}
              >
                <SelectTrigger className="bg-white">
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
            </FilterField>
          </div>

          {isAdmin && selectedDepartmentId && summary && (
            <Button
              size="sm"
              variant="outline"
              onClick={handleExport}
              disabled={exportMutation.isPending}
              className="gap-1.5"
            >
              <Download className="h-4 w-4" />
              {exportMutation.isPending ? 'Hazırlanıyor...' : 'Excel\'e Aktar'}
            </Button>
          )}
        </div>

        {!selectedDepartmentId ? (
          <div className="px-6 py-10 text-center text-sm text-muted-foreground">
            <Info className="h-6 w-6 mx-auto mb-2 text-muted-foreground/40" />
            Sonuçları görmek için yukarıdan dönem, fakülte ve bölüm seçiniz.
          </div>
        ) : summaryLoading ? (
          <div className="p-5">
            <Skeleton className="h-72 rounded-lg" />
          </div>
        ) : !summary ? (
          <div className="p-5">
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>Bölüm özeti yüklenemedi.</AlertDescription>
            </Alert>
          </div>
        ) : (
          <DepartmentSummary
            summary={summary}
            onShowSection={(sectionId) => setSelectedSectionId(sectionId)}
          />
        )}
      </div>

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
    <>
      {/* Stats strip — 4 cells with vertical dividers, no individual card borders */}
      <div className="grid grid-cols-2 md:grid-cols-4 divide-x divide-slate-100 border-b border-slate-100">
        <StatStripCell
          label="Bölüm"
          value={summary.departmentName}
          sub={summary.termName}
          accent="bg-indigo-500"
        />
        <StatStripCell
          label="Değerlendirilen Ders"
          value={`${summary.evaluatedSections} / ${summary.totalSections}`}
          sub="Katılım / Toplam"
          accent="bg-blue-500"
        />
        <StatStripCell
          label="Genel Katılım"
          value={`%${summary.overallResponseRate.toFixed(1)}`}
          sub="Yanıt oranı"
          accent="bg-amber-500"
        />
        <StatStripCell
          label="Ortalama Puan"
          value={summary.averageRating != null ? summary.averageRating.toFixed(2) : '—'}
          sub="5 üzerinden"
          accent="bg-emerald-500"
        />
      </div>

      {summary.sectionSummaries.length === 0 ? (
        <div className="p-6 text-center text-sm text-muted-foreground">
          Bu bölümde değerlendirmeye açık ders bulunmuyor.
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="pl-5">Ders</TableHead>
              <TableHead>Öğretim Üyesi</TableHead>
              <TableHead className="text-center">Katılım</TableHead>
              <TableHead className="text-center">Ort. Puan</TableHead>
              <TableHead className="text-right pr-5">Detay</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {summary.sectionSummaries.map((s) => (
              <TableRow key={s.courseSectionId}>
                <TableCell className="pl-5">
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
                <TableCell className="text-right pr-5">
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
    </>
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
    <div className="overflow-hidden rounded-xl border border-border bg-white shadow-sm">
      {/* Toolbar — dönem + ders seçimi tek başlık altında */}
      <div className="px-4 py-3 border-b border-slate-100 bg-slate-50/30 space-y-3">
        <div className="flex flex-wrap items-end gap-3">
          <FilterField label="Dönem" className="min-w-[240px]">
            <Select
              value={effectiveTermId?.toString() ?? ''}
              onValueChange={(v) => {
                setSelectedTermId(Number(v));
                setSelectedSectionId(null);
              }}
            >
              <SelectTrigger className="bg-white">
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
          </FilterField>
        </div>

        {sectionsLoading ? (
          <Skeleton className="h-9" />
        ) : sections.length === 0 ? (
          <p className="text-sm text-muted-foreground">Bu dönemde ders bulunamadı.</p>
        ) : (
          <div>
            <p className="text-xs font-semibold uppercase text-muted-foreground tracking-wide mb-2">Ders</p>
            <div className="flex flex-wrap gap-2">
              {sections.map((s) => (
                <button
                  key={s.sectionId}
                  onClick={() => setSelectedSectionId(s.sectionId)}
                  className={`px-3 py-1.5 text-sm rounded-md border transition-colors ${
                    activeSectionId === s.sectionId
                      ? 'bg-primary text-primary-foreground border-primary'
                      : 'bg-white border-border hover:border-primary/50'
                  }`}
                >
                  <span className="font-mono mr-1.5">{s.courseCode}</span>
                  <span>{s.courseName}</span>
                </button>
              ))}
            </div>
          </div>
        )}
      </div>

      {sections.length === 0 ? null : resultsLoading ? (
        <div className="p-5">
          <Skeleton className="h-72 rounded-lg" />
        </div>
      ) : !results ? (
        <div className="p-5">
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>Sonuçlar yüklenemedi.</AlertDescription>
          </Alert>
        </div>
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
      <div className="p-5">
        <Alert variant="destructive" className="bg-amber-50 border-amber-200">
          <Lock className="h-4 w-4 text-amber-600" />
          <AlertDescription className="text-amber-900">
            Anonimlik koruması için en az 5 yanıt alınmadan sonuçlar görüntülenemez. Şu ana kadar{' '}
            <strong>{results.totalResponses}</strong> yanıt alındı.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const courseQuestions = results.questionResults.filter((q) => q.category === 'Course');
  const instructorQuestions = results.questionResults.filter((q) => q.category === 'Instructor');

  return (
    <>
      <div className="grid grid-cols-1 md:grid-cols-3 divide-x divide-slate-100 border-b border-slate-100">
        <StatStripCell
          label="Toplam Yanıt"
          value={`${results.totalResponses} / ${results.enrolledCount}`}
          sub={`Katılım: %${results.responseRate.toFixed(1)}`}
          accent="bg-blue-500"
        />
        <StatStripCell
          label="Genel Ortalama"
          value={results.overallAverage != null ? results.overallAverage.toFixed(2) : '—'}
          sub="5 üzerinden"
          accent="bg-emerald-500"
        />
        <StatStripCell
          label="Ders"
          value={results.courseCode}
          sub={results.courseName}
          accent="bg-indigo-500"
        />
      </div>

      <div className="p-5 space-y-4">
        {courseQuestions.length > 0 && (
          <CategorySection title="Ders Hakkında" questions={courseQuestions} total={results.totalResponses} />
        )}
        {instructorQuestions.length > 0 && (
          <CategorySection title="Öğretim Üyesi Hakkında" questions={instructorQuestions} total={results.totalResponses} />
        )}
      </div>
    </>
  );
}

function FilterField({
  label,
  children,
  className,
}: {
  label: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={className}>
      <label className="text-xs font-semibold uppercase text-muted-foreground tracking-wide mb-1 block">
        {label}
      </label>
      {children}
    </div>
  );
}

function StatStripCell({
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
    <div className="px-4 py-3 flex items-start gap-2.5">
      <div className={`mt-1 h-9 w-1 rounded-full ${accent} shrink-0`} />
      <div className="min-w-0 flex-1">
        <p className="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider truncate">{label}</p>
        <p className="text-base font-bold leading-tight mt-0.5 truncate" title={value}>{value}</p>
        <p className="text-xs text-muted-foreground truncate" title={sub}>{sub}</p>
      </div>
    </div>
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
