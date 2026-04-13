import { useEffect, useMemo, useState, useTransition } from 'react';
import { Link } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Separator } from '@/components/ui/separator';
import { CheckCircle2, ClipboardList, Clock3, Info, Lightbulb, Loader2, Route, ShieldAlert, Sparkles } from 'lucide-react';
import { useActiveCourseIntentionSurvey, useSaveCourseIntentions } from '../hooks/useCourseIntention';
import { useRecommendations } from '../hooks/useRecommendations';
import { getApiErrorMessage } from '../lib/apiError';
import type { CourseIntentionLevel } from '../types/courseIntention';
import type { CourseRecommendationDto } from '../types/recommendation';

const DAY_OPTIONS = ['Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma'] as const;
const TIME_OPTIONS = ['Sabah', 'Öğle', 'Öğleden Sonra', 'Akşam'] as const;

const CATEGORY_ORDER: Record<string, number> = {
  'Zorunlu Al': 0,
  Onerilen: 1,
  Alabilirsin: 2,
};

export default function SmartPlanningPage() {
  const [pacePreference, setPacePreference] = useState(55);
  const [isPending, startTransition] = useTransition();
  const recommendationsQuery = useRecommendations(pacePreference);
  const surveyQuery = useActiveCourseIntentionSurvey();

  const recommendations = recommendationsQuery.data?.recommendations ?? [];
  const groupedRecommendations = useMemo(() => {
    return [...recommendations]
      .sort((a, b) => {
        const categoryDiff = (CATEGORY_ORDER[a.category] ?? 99) - (CATEGORY_ORDER[b.category] ?? 99);
        if (categoryDiff !== 0) return categoryDiff;
        return b.priorityScore - a.priorityScore;
      })
      .reduce<Record<string, CourseRecommendationDto[]>>((acc, item) => {
        acc[item.category] = [...(acc[item.category] ?? []), item];
        return acc;
      }, {});
  }, [recommendations]);

  const saveSurvey = useSaveCourseIntentions();
  const surveyData = surveyQuery.data;
  const [selectedIntentions, setSelectedIntentions] = useState<Record<number, CourseIntentionLevel | undefined>>({});
  const [preferredDays, setPreferredDays] = useState<string[]>([]);
  const [preferredTimeRanges, setPreferredTimeRanges] = useState<string[]>([]);
  const [surveySaved, setSurveySaved] = useState(false);
  const [surveyFeedback, setSurveyFeedback] = useState<string | null>(null);

  useEffect(() => {
    if (!surveyData) {
      setSelectedIntentions({});
      setPreferredDays([]);
      setPreferredTimeRanges([]);
      return;
    }

    const nextIntentions = surveyData.responses.reduce<Record<number, CourseIntentionLevel>>((acc, item) => {
      acc[item.courseId] = item.intention;
      return acc;
    }, {});

    setSelectedIntentions(nextIntentions);
    setPreferredDays(splitPreferenceValue(surveyData.preference.preferredDays));
    setPreferredTimeRanges(splitPreferenceValue(surveyData.preference.preferredTimeRanges));
  }, [surveyData]);

  const surveyCourses = useMemo(() => {
    if (!surveyData) return [];
    return [...surveyData.availableCourses].sort((a, b) => {
      const aPriority = a.takeStatus === 'Alttan' ? 0 : a.isCompulsory ? 1 : 2;
      const bPriority = b.takeStatus === 'Alttan' ? 0 : b.isCompulsory ? 1 : 2;
      if (aPriority !== bPriority) return aPriority - bPriority;
      return a.courseCode.localeCompare(b.courseCode, 'tr');
    });
  }, [surveyData]);

  const selectedCount = Object.values(selectedIntentions).filter(Boolean).length;
  const certainCount = Object.values(selectedIntentions).filter(value => value === 'Certain').length;
  const maybeCount = Object.values(selectedIntentions).filter(value => value === 'Maybe').length;

  const handleSaveSurvey = async () => {
    if (!surveyData) return;

    setSurveySaved(false);
    setSurveyFeedback(null);
    const intentions = Object.entries(selectedIntentions)
      .filter(([, value]) => value)
      .map(([courseId, intention]) => ({
        courseId: Number(courseId),
        intention: intention as CourseIntentionLevel,
      }));

    try {
      await saveSurvey.mutateAsync({
        surveyId: surveyData.survey.id,
        intentions,
        preference: {
          preferredDays: preferredDays.join(','),
          preferredTimeRanges: preferredTimeRanges.join(','),
        },
      });

      setSurveySaved(true);
      setSurveyFeedback('Secimlerin kaydedildi. Anket kapanana kadar tekrar duzenleyebilirsin.');
    } catch (error) {
      setSurveyFeedback(getApiErrorMessage(error, 'Anket secimleri kaydedilemedi.'));
    }
  };

  return (
    <div className="space-y-6">
      <Card className="overflow-hidden border-none shadow-sm">
        <div className="bg-[#003366] px-6 py-6 text-white">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div className="space-y-2">
              <div className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-xs font-semibold tracking-[0.08em] uppercase">
                <Sparkles className="h-3.5 w-3.5" />
                Akilli Planlama
              </div>
              <div>
                <h1 className="font-heading text-2xl font-extrabold tracking-tight">Ders onerileri ve niyet anketi</h1>
                <p className="mt-1 max-w-3xl text-sm text-white/75">
                  Sistem uygun oldugun dersleri onceliklendirir, danisman uyarilarini gosterir ve bu donem niyet ettigin dersleri tek yerden toplar.
                </p>
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <SummaryBadge label="Oneri" value={`${recommendations.length} ders`} />
              <SummaryBadge label="Kesin" value={`${certainCount} secim`} />
              <SummaryBadge label="Belki" value={`${maybeCount} secim`} />
            </div>
          </div>
        </div>
      </Card>

      <Tabs defaultValue="recommendations" className="space-y-4">
        <TabsList className="grid w-full grid-cols-2">
          <TabsTrigger value="recommendations">Akilli Ders Onerileri</TabsTrigger>
          <TabsTrigger value="survey">Niyet Anketi</TabsTrigger>
        </TabsList>

        <TabsContent value="recommendations" className="space-y-4">
          <Card>
            <CardHeader className="pb-4">
              <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                <div>
                  <CardTitle>Bu donem icin onceliklendirilmis dersler</CardTitle>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Tempo tercihini degistirerek daha guvenli ya da daha hizli bir plan gorebilirsin.
                  </p>
                </div>

                <div className="min-w-[260px] rounded-xl border border-border bg-muted/20 px-4 py-3">
                  <div className="flex items-center justify-between text-sm">
                    <span className="font-medium text-foreground">Tempo tercihi</span>
                    <span className="text-muted-foreground">{pacePreference}/100 {isPending && 'guncelleniyor...'}</span>
                  </div>
                  <input
                    type="range"
                    min={0}
                    max={100}
                    step={5}
                    value={pacePreference}
                    onChange={event => {
                      const nextValue = Number(event.target.value);
                      startTransition(() => setPacePreference(nextValue));
                    }}
                    className="mt-3 h-2 w-full cursor-pointer accent-[#003366]"
                  />
                  <div className="mt-2 flex justify-between text-[11px] text-muted-foreground">
                    <span>Daha guvenli</span>
                    <span>Daha hizli</span>
                  </div>
                </div>
              </div>
            </CardHeader>
          </Card>

          {recommendationsQuery.isLoading ? (
            <div className="space-y-4">
              {[1, 2, 3].map(item => <Skeleton key={item} className="h-44 rounded-xl" />)}
            </div>
          ) : recommendationsQuery.isError ? (
            <Alert variant="destructive">
              <Info className="h-4 w-4" />
              <AlertTitle>Oneriler yuklenemedi</AlertTitle>
              <AlertDescription>
                {getApiErrorMessage(recommendationsQuery.error, 'Oneri listesi simdilik getirilemedi.')}
              </AlertDescription>
            </Alert>
          ) : recommendations.length === 0 ? (
            <Alert>
              <ClipboardList className="h-4 w-4" />
              <AlertTitle>Oneri bulunamadi</AlertTitle>
              <AlertDescription>
                Su anda uygun gorunen ders bulunmuyor. <Link to="/ders-kayit" className="font-semibold underline underline-offset-4">Ders kayit ekranina</Link> donup katalogu kontrol edebilirsin.
              </AlertDescription>
            </Alert>
          ) : (
            <div className="space-y-5">
              {Object.entries(groupedRecommendations).map(([category, items]) => (
                <section key={category} className="space-y-3">
                  <div className="flex items-center gap-2">
                    <Badge variant={category === 'Zorunlu Al' ? 'destructive' : category === 'Onerilen' ? 'warning' : 'secondary'}>
                      {category}
                    </Badge>
                    <span className="text-sm text-muted-foreground">{items.length} ders</span>
                  </div>

                  <div className="grid gap-4 xl:grid-cols-2">
                    {items.map(item => (
                      <RecommendationCard key={item.courseId} recommendation={item} />
                    ))}
                  </div>
                </section>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="survey" className="space-y-4">
          {surveyQuery.isLoading ? (
            <div className="space-y-4">
              <Skeleton className="h-32 rounded-xl" />
              <Skeleton className="h-72 rounded-xl" />
            </div>
          ) : surveyQuery.isError ? (
            <Alert>
              <Clock3 className="h-4 w-4" />
              <AlertTitle>Acik anket bulunamadi</AlertTitle>
              <AlertDescription>
                {getApiErrorMessage(surveyQuery.error, 'Su anda acik niyet anketi bulunmuyor. Idari birim anket actiginda burada otomatik gorunecek.')}
              </AlertDescription>
            </Alert>
          ) : surveyData ? (
            <>
              <Card>
                <CardHeader className="pb-3">
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                    <div>
                      <CardTitle>{surveyData.survey.title}</CardTitle>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {surveyData.survey.termName} icin niyet ettigin dersleri sec. Block kisitli dersler burada gosterilmez.
                      </p>
                    </div>
                    <Badge variant="info">
                      Acik kalis: {formatDateTime(surveyData.survey.closeAtUtc)}
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
                  <div className="space-y-4">
                    <PreferenceGroup
                      title="Gun tercihleri"
                      description="Yogunlasmasini istedigin gunleri sec."
                      options={DAY_OPTIONS}
                      selectedValues={preferredDays}
                      onToggle={(value) => setPreferredDays(prev => toggleArrayValue(prev, value))}
                    />

                    <PreferenceGroup
                      title="Saat tercihleri"
                      description="Sabah ya da ogleden sonra agirlikli istedigin araliklari sec."
                      options={TIME_OPTIONS}
                      selectedValues={preferredTimeRanges}
                      onToggle={(value) => setPreferredTimeRanges(prev => toggleArrayValue(prev, value))}
                    />
                  </div>

                  <div className="rounded-2xl border border-border bg-muted/20 p-4">
                    <p className="text-sm font-semibold text-foreground">Anket ozeti</p>
                    <div className="mt-4 space-y-3 text-sm">
                      <SummaryRow label="Toplam secili ders" value={`${selectedCount}`} />
                      <SummaryRow label="Kesin alacagim" value={`${certainCount}`} />
                      <SummaryRow label="Belki alacagim" value={`${maybeCount}`} />
                      <SummaryRow label="Gun tercihi" value={preferredDays.join(', ') || 'Secilmedi'} />
                      <SummaryRow label="Saat tercihi" value={preferredTimeRanges.join(', ') || 'Secilmedi'} />
                    </div>
                    <Separator className="my-4" />
                    <Button
                      onClick={handleSaveSurvey}
                      disabled={saveSurvey.isPending}
                      className="w-full gap-2 bg-[#003366] hover:bg-[#002244]"
                    >
                      {saveSurvey.isPending ? (
                        <>
                          <Loader2 className="h-4 w-4 animate-spin" />
                          Kaydediliyor...
                        </>
                      ) : (
                        <>
                          <CheckCircle2 className="h-4 w-4" />
                          Anketi Kaydet
                        </>
                      )}
                    </Button>
                    {surveySaved && (
                      <p className="mt-3 text-xs font-medium text-emerald-700">
                        Secimlerin kaydedildi. Dilersen anket kapanana kadar tekrar duzenleyebilirsin.
                      </p>
                    )}
                    {!surveySaved && surveyFeedback && (
                      <p className="mt-3 text-xs font-medium text-destructive">
                        {surveyFeedback}
                      </p>
                    )}
                  </div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="pb-3">
                  <CardTitle>Bu donem dusundugun dersler</CardTitle>
                </CardHeader>
                <CardContent className="space-y-3">
                  {surveyCourses.length === 0 ? (
                    <Alert>
                      <ShieldAlert className="h-4 w-4" />
                      <AlertDescription>
                        Ankete uygun ders bulunamadi. Uygunluk veya danisman kisitlari nedeniyle liste bos olabilir.
                      </AlertDescription>
                    </Alert>
                  ) : (
                    surveyCourses.map(course => (
                      <div key={course.courseId} className="rounded-2xl border border-border p-4">
                        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                          <div className="space-y-2">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="font-mono text-sm font-semibold text-primary">{course.courseCode}</span>
                              <span className="font-medium text-foreground">{course.courseName}</span>
                              <Badge variant={course.isCompulsory ? 'accent' : 'secondary'}>
                                {course.isCompulsory ? 'Zorunlu' : 'Secmeli'}
                              </Badge>
                              <Badge variant={course.takeStatus === 'Alttan' ? 'destructive' : 'outline'}>
                                {course.takeStatus === 'Alttan' ? `Alttan ${course.previousGrade ? `(${course.previousGrade})` : ''}` : `${course.suggestedSemester}. Donem`}
                              </Badge>
                            </div>
                            <p className="text-sm text-muted-foreground">
                              {course.ects} AKTS
                              {course.previousGrade ? ` • Onceki not: ${course.previousGrade}` : ''}
                            </p>
                          </div>

                          <div className="flex flex-wrap gap-2">
                            <IntentButton
                              active={selectedIntentions[course.courseId] === 'Certain'}
                              label="Kesin alacagim"
                              onClick={() => setSelectedIntentions(prev => ({
                                ...prev,
                                [course.courseId]: prev[course.courseId] === 'Certain' ? undefined : 'Certain',
                              }))}
                            />
                            <IntentButton
                              active={selectedIntentions[course.courseId] === 'Maybe'}
                              label="Belki alacagim"
                              onClick={() => setSelectedIntentions(prev => ({
                                ...prev,
                                [course.courseId]: prev[course.courseId] === 'Maybe' ? undefined : 'Maybe',
                              }))}
                            />
                          </div>
                        </div>
                      </div>
                    ))
                  )}
                </CardContent>
              </Card>
            </>
          ) : null}
        </TabsContent>
      </Tabs>
    </div>
  );
}

function RecommendationCard({ recommendation }: { recommendation: CourseRecommendationDto }) {
  return (
    <Card className="overflow-hidden border-border shadow-sm">
      <CardContent className="p-0">
        <div className="border-b border-border bg-muted/20 px-5 py-4">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div className="space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-mono text-sm font-semibold text-primary">{recommendation.courseCode}</span>
                <span className="font-semibold text-foreground">{recommendation.courseName}</span>
                <Badge variant={recommendation.isCompulsory ? 'accent' : 'secondary'}>
                  {recommendation.isCompulsory ? 'Zorunlu' : 'Secmeli'}
                </Badge>
                {recommendation.advisorConstraintType === 'Warning' && (
                  <Badge variant="warning">Danisman Uyarisi</Badge>
                )}
              </div>
              <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                <span>{recommendation.ects} AKTS</span>
                <span>{recommendation.suggestedSemester}. Donem</span>
                <span>{recommendation.takeType}</span>
              </div>
            </div>

            <div className="min-w-[140px] rounded-xl bg-white px-3 py-2 text-right shadow-sm ring-1 ring-border">
              <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">Oncelik skoru</p>
              <p className="mt-1 text-2xl font-extrabold text-foreground">{recommendation.priorityScore.toFixed(0)}</p>
            </div>
          </div>
        </div>

        <div className="grid gap-5 px-5 py-4 lg:grid-cols-[1.1fr_0.9fr]">
          <div className="space-y-3">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">Neden oneriliyor?</p>
              <div className="mt-2 flex flex-col gap-2">
                {recommendation.reasons.map(reason => (
                  <div key={reason} className="inline-flex items-start gap-2 rounded-xl bg-muted/30 px-3 py-2 text-sm text-foreground">
                    <Lightbulb className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" />
                    <span>{reason}</span>
                  </div>
                ))}
                {recommendation.advisorConstraintNote && (
                  <Alert variant="warning">
                    <ShieldAlert className="h-4 w-4" />
                    <AlertDescription>{recommendation.advisorConstraintNote}</AlertDescription>
                  </Alert>
                )}
              </div>
            </div>
          </div>

          <div className="space-y-4 rounded-2xl border border-border bg-muted/10 p-4">
            <MetricRow label="Zorluk skoru" value={`${recommendation.difficulty.score.toFixed(0)}/100`} progress={recommendation.difficulty.score} />
            <MetricRow label="Basarisizlik etkisi" value={recommendation.difficulty.failureRateComponent.toFixed(1)} progress={recommendation.difficulty.failureRateComponent} />
            <MetricRow label="Not dagilimi etkisi" value={recommendation.difficulty.gradeDistributionComponent.toFixed(1)} progress={recommendation.difficulty.gradeDistributionComponent} />
            <MetricRow label="Tekrar alma zorlugu" value={recommendation.difficulty.retakeDifficultyComponent.toFixed(1)} progress={recommendation.difficulty.retakeDifficultyComponent} />
            <MetricRow label="Kritik yol gecikmesi" value={`${recommendation.criticalPath.estimatedDelaySemesters} donem`} progress={Math.min(recommendation.criticalPath.estimatedDelaySemesters * 33, 100)} />

            {recommendation.criticalPath.dependentCourses.length > 0 && (
              <div className="rounded-xl bg-white px-3 py-3 ring-1 ring-border">
                <div className="flex items-center gap-2">
                  <Route className="h-4 w-4 text-primary" />
                  <p className="text-sm font-medium text-foreground">Bagli dersler</p>
                </div>
                <p className="mt-2 text-sm text-muted-foreground">
                  {recommendation.criticalPath.dependentCourses.join(', ')}
                </p>
              </div>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function PreferenceGroup({
  title,
  description,
  options,
  selectedValues,
  onToggle,
}: {
  title: string;
  description: string;
  options: readonly string[];
  selectedValues: string[];
  onToggle: (value: string) => void;
}) {
  return (
    <div className="rounded-2xl border border-border p-4">
      <p className="text-sm font-semibold text-foreground">{title}</p>
      <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      <div className="mt-3 flex flex-wrap gap-2">
        {options.map(option => {
          const active = selectedValues.includes(option);
          return (
            <button
              key={option}
              type="button"
              onClick={() => onToggle(option)}
              className={`rounded-full border px-3 py-1.5 text-sm transition-colors ${
                active
                  ? 'border-[#003366] bg-[#003366] text-white'
                  : 'border-border bg-background text-foreground hover:bg-muted/40'
              }`}
            >
              {option}
            </button>
          );
        })}
      </div>
    </div>
  );
}

function IntentButton({ active, label, onClick }: { active: boolean; label: string; onClick: () => void }) {
  return (
    <Button
      type="button"
      variant={active ? 'default' : 'outline'}
      onClick={onClick}
      className={active ? 'bg-[#003366] hover:bg-[#002244]' : ''}
    >
      {label}
    </Button>
  );
}

function MetricRow({ label, value, progress }: { label: string; value: string; progress: number }) {
  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between text-sm">
        <span className="text-muted-foreground">{label}</span>
        <span className="font-semibold text-foreground">{value}</span>
      </div>
      <Progress value={Math.max(0, Math.min(progress, 100))} className="h-2" />
    </div>
  );
}

function SummaryBadge({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-white/15 bg-white/10 px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-white/65">{label}</p>
      <p className="mt-1 text-base font-semibold text-white">{value}</p>
    </div>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-3">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right font-medium text-foreground">{value}</span>
    </div>
  );
}

function splitPreferenceValue(value?: string | null) {
  if (!value) return [];
  return value
    .split(',')
    .map(item => item.trim())
    .filter(Boolean);
}

function toggleArrayValue(values: string[], value: string) {
  return values.includes(value)
    ? values.filter(item => item !== value)
    : [...values, value];
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
