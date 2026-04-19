import { useDeferredValue, useEffect, useMemo, useState, useTransition } from 'react';
import { Link } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';
import { ArrowRight, CheckCircle2, ClipboardList, Clock3, Gauge, Info, Lightbulb, ListChecks, Loader2, Lock, Route, Search, ShieldAlert, Sparkles, Target } from 'lucide-react';
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

const PACE_PRESETS = [
  {
    label: 'Guvenli',
    value: 25,
    helper: 'Daha az risk, daha hafif yuk',
  },
  {
    label: 'Dengeli',
    value: 55,
    helper: 'Guven ve hiz arasinda denge',
  },
  {
    label: 'Hizli',
    value: 80,
    helper: 'Daha agresif ilerleme',
  },
] as const;

type OptionalCourseFilter = 'All' | 'Compulsory' | 'Elective' | 'BehindSchedule';
type SmartPlanningTab = 'recommendations' | 'survey';

export default function SmartPlanningPage() {
  const [activeTab, setActiveTab] = useState<SmartPlanningTab>('recommendations');
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
  const [reviewedCourseIds, setReviewedCourseIds] = useState<number[]>([]);
  const [courseQuery, setCourseQuery] = useState('');
  const [courseFilter, setCourseFilter] = useState<OptionalCourseFilter>('All');
  const [surveySaved, setSurveySaved] = useState(false);
  const [surveyFeedback, setSurveyFeedback] = useState<string | null>(null);
  const [focusedCourseId, setFocusedCourseId] = useState<number | null>(null);
  const deferredCourseQuery = useDeferredValue(courseQuery);

  useEffect(() => {
    if (!surveyData) {
      setSelectedIntentions({});
      setPreferredDays([]);
      setPreferredTimeRanges([]);
      setReviewedCourseIds([]);
      setCourseQuery('');
      setCourseFilter('All');
      return;
    }

    const availableCourseIds = new Set(surveyData.availableCourses.map(course => course.courseId));
    const optionalCourseIds = new Set(
      surveyData.availableCourses
        .filter(course => !course.isLocked)
        .map(course => course.courseId)
    );
    const nextIntentions = surveyData.responses.reduce<Record<number, CourseIntentionLevel>>((acc, item) => {
      if (availableCourseIds.has(item.courseId)) {
        acc[item.courseId] = item.intention;
      }
      return acc;
    }, {});

    surveyData.availableCourses.forEach(course => {
      if (course.isLocked) {
        nextIntentions[course.courseId] = course.lockedIntention ?? 'Certain';
      }
    });

    setSelectedIntentions(nextIntentions);
    setPreferredDays(splitPreferenceValue(surveyData.preference.preferredDays));
    setPreferredTimeRanges(splitPreferenceValue(surveyData.preference.preferredTimeRanges));
    setReviewedCourseIds(
      surveyData.responses
        .map(item => item.courseId)
        .filter(courseId => optionalCourseIds.has(courseId))
    );
    setCourseQuery('');
    setCourseFilter('All');
  }, [surveyData]);

  const surveyCourses = useMemo(() => {
    if (!surveyData) return [];
    return [...surveyData.availableCourses].sort((a, b) => {
      const aPriority = a.isLocked ? 0 : a.isCompulsory ? 1 : 2;
      const bPriority = b.isLocked ? 0 : b.isCompulsory ? 1 : 2;
      if (aPriority !== bPriority) return aPriority - bPriority;
      const takeStatusDiff = getTakeStatusPriority(a.takeStatus) - getTakeStatusPriority(b.takeStatus);
      if (takeStatusDiff !== 0) return takeStatusDiff;
      return a.courseCode.localeCompare(b.courseCode, 'tr');
    });
  }, [surveyData]);
  const surveyCourseMap = useMemo(
    () => new Map(surveyCourses.map(course => [course.courseId, course])),
    [surveyCourses]
  );
  const actionableRecommendationCount = useMemo(
    () => recommendations.filter(recommendation => surveyCourseMap.has(recommendation.courseId)).length,
    [recommendations, surveyCourseMap]
  );

  const lockedSurveyCourses = useMemo(
    () => surveyCourses.filter(course => course.isLocked),
    [surveyCourses]
  );
  const optionalSurveyCourses = useMemo(
    () => surveyCourses.filter(course => !course.isLocked),
    [surveyCourses]
  );

  const lockedCount = lockedSurveyCourses.length;
  const manualCertainCount = optionalSurveyCourses.filter(
    course => selectedIntentions[course.courseId] === 'Certain'
  ).length;
  const certainCount = lockedCount + manualCertainCount;
  const maybeCount = optionalSurveyCourses.filter(
    course => selectedIntentions[course.courseId] === 'Maybe'
  ).length;
  const selectedCount = certainCount + maybeCount;
  const reviewedCourseIdSet = useMemo(() => new Set(reviewedCourseIds), [reviewedCourseIds]);
  const reviewedOptionalCount = optionalSurveyCourses.filter(course => reviewedCourseIdSet.has(course.courseId)).length;
  const reviewProgress = optionalSurveyCourses.length === 0
    ? 100
    : Math.round((reviewedOptionalCount / optionalSurveyCourses.length) * 100);
  const remainingOptionalCount = Math.max(optionalSurveyCourses.length - reviewedOptionalCount, 0);
  const filteredOptionalSurveyCourses = useMemo(() => {
    const normalizedQuery = deferredCourseQuery.trim().toLocaleLowerCase('tr');

    return optionalSurveyCourses.filter(course => {
      const matchesFilter = (() => {
        switch (courseFilter) {
          case 'Compulsory':
            return course.isCompulsory;
          case 'Elective':
            return !course.isCompulsory;
          case 'BehindSchedule':
            return course.takeStatus === 'Gecikmis';
          default:
            return true;
        }
      })();

      if (!matchesFilter) return false;
      if (!normalizedQuery) return true;

      return `${course.courseCode} ${course.courseName}`
        .toLocaleLowerCase('tr')
        .includes(normalizedQuery);
    });
  }, [courseFilter, deferredCourseQuery, optionalSurveyCourses]);
  const nextSuggestedCourse = useMemo(
    () => optionalSurveyCourses.find(course => !reviewedCourseIdSet.has(course.courseId)) ?? optionalSurveyCourses[0],
    [optionalSurveyCourses, reviewedCourseIdSet]
  );

  useEffect(() => {
    if (focusedCourseId === null || activeTab !== 'survey' || typeof window === 'undefined') return;

    const frame = window.requestAnimationFrame(() => {
      const target = document.getElementById(`survey-course-${focusedCourseId}`);
      target?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });

    return () => window.cancelAnimationFrame(frame);
  }, [activeTab, focusedCourseId, filteredOptionalSurveyCourses.length, surveyCourses.length]);

  useEffect(() => {
    if (focusedCourseId === null || typeof window === 'undefined') return;

    const timeout = window.setTimeout(() => setFocusedCourseId(null), 2200);
    return () => window.clearTimeout(timeout);
  }, [focusedCourseId]);
  const initialIntentionsKey = useMemo(() => {
    if (!surveyData) return '';

    const entries = surveyData.availableCourses.flatMap(course => {
      const response = surveyData.responses.find(item => item.courseId === course.courseId);
      const intention = course.isLocked
        ? course.lockedIntention ?? 'Certain'
        : response?.intention;

      return intention ? [`${course.courseId}:${intention}`] : [];
    });

    return entries.sort().join('|');
  }, [surveyData]);
  const currentIntentionsKey = useMemo(() => {
    if (!surveyData) return '';

    const entries = surveyData.availableCourses.flatMap(course => {
      const intention = course.isLocked
        ? course.lockedIntention ?? 'Certain'
        : selectedIntentions[course.courseId];

      return intention ? [`${course.courseId}:${intention}`] : [];
    });

    return entries.sort().join('|');
  }, [selectedIntentions, surveyData]);
  const initialDaysKey = useMemo(
    () => buildPreferenceKey(splitPreferenceValue(surveyData?.preference.preferredDays)),
    [surveyData]
  );
  const initialTimeRangesKey = useMemo(
    () => buildPreferenceKey(splitPreferenceValue(surveyData?.preference.preferredTimeRanges)),
    [surveyData]
  );
  const hasUnsavedChanges = !!surveyData && (
    initialIntentionsKey !== currentIntentionsKey
    || initialDaysKey !== buildPreferenceKey(preferredDays)
    || initialTimeRangesKey !== buildPreferenceKey(preferredTimeRanges)
  );
  const needsLockedSubmission = !!surveyData && lockedSurveyCourses.some(
    course => !surveyData.responses.some(response => response.courseId === course.courseId)
  );
  const heroTitle = activeTab === 'survey'
    ? 'Niyet anketi'
    : 'Ders onerileri ve niyet anketi';
  const heroDescription = activeTab === 'survey'
    ? 'Bu donem icin zorunlu derslerini gor, secilebilir derslerde niyetini belirt ve program tercihlerini tek akista tamamla.'
    : 'Sistem uygun oldugun dersleri onceliklendirir, danisman uyarilarini gosterir ve bu donem niyet ettigin dersleri tek yerden toplar.';
  const paceProfile = getPaceProfile(pacePreference);

  const handleRecommendationAction = (courseId: number, intention?: CourseIntentionLevel) => {
    const targetCourse = surveyCourseMap.get(courseId);

    if (targetCourse && !targetCourse.isLocked && intention) {
      setSelectedIntentions(prev => ({
        ...prev,
        [courseId]: intention,
      }));
      setReviewedCourseIds(prev => (
        prev.includes(courseId)
          ? prev
          : [...prev, courseId]
      ));
    }

    setFocusedCourseId(courseId);
    setActiveTab('survey');
  };

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
    <div className="space-y-6 pb-24 xl:pb-0">
      <Card className="overflow-hidden border-none shadow-sm">
        <div className="bg-[linear-gradient(135deg,#0a2348_0%,#123c76_58%,#0b2850_100%)] px-6 py-6 text-white">
          <div className="grid gap-6 xl:grid-cols-[minmax(0,1.4fr)_minmax(320px,0.9fr)] xl:items-end">
            <div className="space-y-5">
              <div className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-xs font-semibold tracking-[0.08em] uppercase">
                <Sparkles className="h-3.5 w-3.5" />
                {activeTab === 'survey' ? 'Niyet Akisi' : 'Akilli Planlama'}
              </div>
              <div>
                <h1 className="font-heading text-3xl font-extrabold tracking-tight">{heroTitle}</h1>
                <p className="mt-2 max-w-3xl text-sm leading-6 text-white/78">
                  {heroDescription}
                </p>
              </div>

              <div className="grid gap-3 md:grid-cols-3">
                <HeroGuidePill
                  icon={<Target className="h-4 w-4" />}
                  title="1. Oneriyi tara"
                  description="Skor ve gecikme etkisine bakarak ilk adaylarini kisalt."
                />
                <HeroGuidePill
                  icon={<ListChecks className="h-4 w-4" />}
                  title="2. Niyetini belirle"
                  description="Uygun dersleri anket tarafinda kesin ya da belki olarak isaretle."
                />
                <HeroGuidePill
                  icon={<CheckCircle2 className="h-4 w-4" />}
                  title="3. Tek seferde kaydet"
                  description="Program tercihlerini ekleyip secimlerini anket kapanmadan gonder."
                />
              </div>
            </div>

            <div className="rounded-3xl border border-white/15 bg-white/10 p-5 backdrop-blur">
              <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-white/65">Siradaki adim</p>
              <h2 className="mt-2 text-xl font-semibold text-white">
                {activeTab === 'survey'
                  ? (hasUnsavedChanges || needsLockedSubmission ? 'Secimlerini sisteme kaydet' : 'Kararini guclendirmek icin onerilere don')
                  : (surveyData ? 'Uygun gordugun dersleri ankete tasimaya basla' : 'Onerileri kullanarak bu donem yukunu sekillendir')}
              </h2>
              <p className="mt-2 text-sm leading-6 text-white/72">
                {activeTab === 'survey'
                  ? (hasUnsavedChanges || needsLockedSubmission
                    ? 'Zorunlu derslerin kayda gecmesi ve yaptigin secimlerin korunmasi icin bu adimi tamamla.'
                    : 'Anket kayitli gorunuyor. Istersen onerilere geri donup kararini tekrar karsilastirabilirsin.')
                  : (surveyData
                    ? `${actionableRecommendationCount} oneriyi anket tarafinda hizli aksiyonla dogrudan secime cevirebilirsin.`
                    : 'Su anda acik anket bulunmuyor; yine de dersleri oncelik sirasina gore rahatca karsilastirabilirsin.')}
              </p>

              <div className="mt-5">
                {activeTab === 'survey' ? (
                  <Button
                    onClick={hasUnsavedChanges || needsLockedSubmission ? handleSaveSurvey : () => setActiveTab('recommendations')}
                    disabled={saveSurvey.isPending}
                    className="w-full gap-2 bg-white text-[#0b2850] hover:bg-white/90"
                  >
                    {hasUnsavedChanges || needsLockedSubmission ? (
                      <>
                        {saveSurvey.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />}
                        {saveSurvey.isPending ? 'Kaydediliyor...' : 'Secimleri kaydet'}
                      </>
                    ) : (
                      <>
                        <ArrowRight className="h-4 w-4" />
                        Onerilere geri don
                      </>
                    )}
                  </Button>
                ) : surveyData ? (
                  <Button
                    onClick={() => setActiveTab('survey')}
                    className="w-full gap-2 bg-white text-[#0b2850] hover:bg-white/90"
                  >
                    <ArrowRight className="h-4 w-4" />
                    Niyet anketine gec
                  </Button>
                ) : (
                  <Button asChild className="w-full gap-2 bg-white text-[#0b2850] hover:bg-white/90">
                    <Link to="/ders-kayit">
                      <ArrowRight className="h-4 w-4" />
                      Ders kayit ekranina git
                    </Link>
                  </Button>
                )}
              </div>

              <div className="mt-5 grid gap-3 sm:grid-cols-3">
                {activeTab === 'survey' ? (
                  <>
                    <SummaryBadge label="Zorunlu" value={`${lockedCount} ders`} />
                    <SummaryBadge label="Secilebilir" value={`${optionalSurveyCourses.length} ders`} />
                    <SummaryBadge label="Incelenen" value={`${reviewedOptionalCount}/${optionalSurveyCourses.length || 0}`} />
                  </>
                ) : (
                  <>
                    <SummaryBadge label="Oneri" value={`${recommendations.length} ders`} />
                    <SummaryBadge label="Kesin" value={`${certainCount} secim`} />
                    <SummaryBadge label="Belki" value={`${maybeCount} secim`} />
                  </>
                )}
              </div>
            </div>
          </div>
        </div>
      </Card>

      <Tabs value={activeTab} onValueChange={value => setActiveTab(value as SmartPlanningTab)} className="space-y-4">
        <TabsList className="grid w-full grid-cols-2">
          <TabsTrigger value="recommendations">Akilli Ders Onerileri</TabsTrigger>
          <TabsTrigger value="survey">Niyet Anketi</TabsTrigger>
        </TabsList>

        <TabsContent value="recommendations" className="space-y-4">
          <Card className="overflow-hidden shadow-sm">
            <CardContent className="grid gap-6 p-6 lg:grid-cols-[minmax(0,1.15fr)_minmax(300px,0.85fr)]">
              <div className="space-y-4">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant="info">Adim 1</Badge>
                  {surveyData && (
                    <Badge variant="outline">{actionableRecommendationCount} ders ankette hizli secilebilir</Badge>
                  )}
                </div>

                <div>
                  <h2 className="font-heading text-xl font-semibold tracking-tight text-foreground">
                    Bu donem icin onceliklendirilmis dersler
                  </h2>
                  <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
                    Skorlar karar vermeni kolaylastirmak icin var. Kartlardaki nedenleri oku, kritik yol gecikmesini gor
                    ve uygun dersleri tek tikla anket tarafina tasimaya basla.
                  </p>
                </div>

                <div className="grid gap-3 sm:grid-cols-3">
                  <InlineMetric label="Toplam oneri" value={`${recommendations.length}`} helper="listelendi" />
                  <InlineMetric label="Ankete uygun" value={`${actionableRecommendationCount}`} helper="hizli aksiyon var" />
                  <InlineMetric label="Tempo modu" value={paceProfile.label} helper={paceProfile.shortLabel} />
                </div>
              </div>

              <div className="rounded-3xl border border-slate-200 bg-slate-50/80 p-5">
                <div className="flex items-center gap-2 text-sm font-semibold text-foreground">
                  <Gauge className="h-4 w-4 text-[#003366]" />
                  Tempo modu: {paceProfile.label}
                </div>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  {paceProfile.description}
                </p>

                <div className="mt-4 flex flex-wrap gap-2">
                  {PACE_PRESETS.map(option => (
                    <button
                      key={option.label}
                      type="button"
                      onClick={() => startTransition(() => setPacePreference(option.value))}
                      className={cn(
                        'rounded-full border px-3 py-1.5 text-sm font-medium transition-colors',
                        Math.abs(pacePreference - option.value) <= 10
                          ? 'border-[#003366] bg-[#003366] text-white'
                          : 'border-border bg-white text-foreground hover:bg-muted/40'
                      )}
                    >
                      {option.label}
                    </button>
                  ))}
                </div>

                <div className="mt-5 rounded-2xl border border-white/80 bg-white p-4 shadow-sm">
                  <div className="flex items-center justify-between text-sm">
                    <span className="font-medium text-foreground">Detayli ayar</span>
                    <span className="text-muted-foreground">
                      {pacePreference}/100 {isPending && 'guncelleniyor...'}
                    </span>
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
            </CardContent>
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
                      <RecommendationCard
                        key={item.courseId}
                        recommendation={item}
                        surveyCourse={surveyCourseMap.get(item.courseId)}
                        currentIntention={selectedIntentions[item.courseId]}
                        onQuickAction={handleRecommendationAction}
                      />
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
              <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
                <div className="space-y-5">
                  <Card className="overflow-hidden border-none shadow-sm">
                    <div className="bg-[#003366] px-6 py-5 text-white">
                      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                        <div className="space-y-3">
                          <div className="flex flex-wrap items-center gap-2">
                            <Badge className="bg-white/10 text-white" variant="outline">Niyet Anketi</Badge>
                            {lockedCount > 0 && (
                              <Badge className="border-white/15 bg-red-500/90 text-white" variant="outline">
                                {lockedCount} zorunlu alttan ders
                              </Badge>
                            )}
                          </div>
                          <div>
                            <h2 className="text-xl font-semibold tracking-tight">{surveyData.survey.title}</h2>
                            <p className="mt-1 max-w-3xl text-sm text-white/75">
                              {surveyData.survey.termName} icin ders niyetini kurumsal ve net bir akisla tamamla.
                              Sistem, FF, NA ve U notu aldigin dersleri otomatik olarak zorunlu tutar.
                            </p>
                          </div>
                        </div>
                        <div className="rounded-2xl border border-white/15 bg-white/10 px-4 py-3 text-sm">
                          <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-white/65">Kapanis</p>
                          <p className="mt-1 font-semibold text-white">{formatDateTime(surveyData.survey.closeAtUtc)}</p>
                        </div>
                      </div>
                    </div>

                    <CardContent className="grid gap-4 p-6 xl:grid-cols-[minmax(0,1fr)_320px]">
                      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                        <SurveyStatCard label="Zorunlu Ders" value={`${lockedCount}`} helper="Sistem kilitli" />
                        <SurveyStatCard label="Secilebilir Ders" value={`${optionalSurveyCourses.length}`} helper="Karar bekliyor" />
                        <SurveyStatCard label="Incelenen" value={`${reviewedOptionalCount}/${optionalSurveyCourses.length || 0}`} helper="Opsiyonel dersler" />
                        <SurveyStatCard label="Belki" value={`${maybeCount}`} helper="Ihtimalli secim" />
                      </div>

                      <div className="rounded-2xl border border-border bg-slate-50/80 p-4">
                        <p className="text-sm font-semibold text-foreground">Bu ekranda ne yapacaksin?</p>
                        <div className="mt-3 space-y-3">
                          <ChecklistItem
                            checked={lockedCount === 0 || !needsLockedSubmission}
                            title={lockedCount > 0 ? 'Zorunlu dersler kayda hazir' : 'Zorunlu ders adimi tamam'}
                            description={lockedCount > 0
                              ? `${lockedCount} ders sistem tarafindan kesin alinacak olarak eklendi.`
                              : 'Bu donem sistem tarafindan kilitlenen ders bulunmuyor.'}
                          />
                          <ChecklistItem
                            checked={remainingOptionalCount === 0}
                            title="Opsiyonel dersleri gozden gecir"
                            description={remainingOptionalCount > 0
                              ? `${remainingOptionalCount} ders daha inceleyip kararini netlestirebilirsin.`
                              : 'Tum opsiyonel dersleri en az bir kez degerlendirdin.'}
                          />
                          <ChecklistItem
                            checked={!hasUnsavedChanges && !needsLockedSubmission}
                            title="Kaydetme adimini tamamla"
                            description={hasUnsavedChanges || needsLockedSubmission
                              ? 'Secimlerin sisteme islenmesi icin kaydet butonuna basman gerekiyor.'
                              : 'Tum secimlerin kayitli gorunuyor.'}
                          />
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  {surveyCourses.length === 0 ? (
                    <Alert>
                      <ShieldAlert className="h-4 w-4" />
                      <AlertDescription>
                        Ankete uygun ders bulunamadi. Uygunluk veya danisman kisitlari nedeniyle liste bos olabilir.
                      </AlertDescription>
                    </Alert>
                  ) : (
                    <>
                      {lockedSurveyCourses.length > 0 && (
                        <Card className="border-red-200/70 bg-red-50/30">
                          <CardHeader className="pb-3">
                            <SurveySectionHeader
                              step="1"
                              title="Sistem tarafindan eklenen zorunlu dersler"
                              description="Bu dersler onceki not durumun nedeniyle otomatik olarak kesin alinacak sekilde kilitlendi."
                              aside={<Badge variant="destructive">{lockedCount} kilitli ders</Badge>}
                            />
                          </CardHeader>
                          <CardContent className="space-y-3">
                            {lockedSurveyCourses.map(course => (
                              <LockedCourseCard key={course.courseId} course={course} />
                            ))}
                          </CardContent>
                        </Card>
                      )}

                      <Card>
                        <CardHeader className="pb-4">
                          <SurveySectionHeader
                            step="2"
                            title="Secim yapabilecegin dersler"
                            description="Sadece bu listedeki derslerde niyetini belirtebilirsin. Arama ve filtreleri kullanarak listeyi sadeleştirebilirsin."
                            aside={<Badge variant="secondary">{optionalSurveyCourses.length} secilebilir ders</Badge>}
                          />
                        </CardHeader>
                        <CardContent className="space-y-4">
                          {optionalSurveyCourses.length === 0 ? (
                            <Alert>
                              <CheckCircle2 className="h-4 w-4" />
                              <AlertDescription>
                                Bu anket icin secilebilir ek ders bulunmuyor. Sadece zorunlu alttan derslerin kayda alinacak.
                              </AlertDescription>
                            </Alert>
                          ) : (
                            <>
                              <div className="rounded-2xl border border-border bg-slate-50/70 p-4">
                                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                                  <div className="relative w-full lg:max-w-sm">
                                    <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                                    <Input
                                      value={courseQuery}
                                      onChange={event => setCourseQuery(event.target.value)}
                                      placeholder="Ders kodu veya adi ara"
                                      className="pl-9"
                                    />
                                  </div>
                                  <div className="flex flex-wrap gap-2">
                                    <FilterChip
                                      active={courseFilter === 'All'}
                                      label="Tumu"
                                      onClick={() => setCourseFilter('All')}
                                    />
                                    <FilterChip
                                      active={courseFilter === 'Compulsory'}
                                      label="Zorunlu"
                                      onClick={() => setCourseFilter('Compulsory')}
                                    />
                                    <FilterChip
                                      active={courseFilter === 'Elective'}
                                      label="Secmeli"
                                      onClick={() => setCourseFilter('Elective')}
                                    />
                                    <FilterChip
                                      active={courseFilter === 'BehindSchedule'}
                                      label="Gecikmis"
                                      onClick={() => setCourseFilter('BehindSchedule')}
                                    />
                                  </div>
                                </div>
                              </div>

                              <div className="flex flex-col gap-4 rounded-2xl border border-dashed border-slate-300 bg-white p-4 md:flex-row md:items-center md:justify-between">
                                <div>
                                  <p className="text-sm font-semibold text-foreground">Karar akisini kaybetme</p>
                                  <p className="mt-1 text-sm text-muted-foreground">
                                    {nextSuggestedCourse
                                      ? `Siradaki odak: ${nextSuggestedCourse.courseCode} ${nextSuggestedCourse.courseName}.`
                                      : 'Tum secilebilir dersleri inceledin; artik sadece son kontrol ve kaydetme kaldi.'}
                                  </p>
                                </div>
                                <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center">
                                  <div className="min-w-[170px]">
                                    <Progress value={reviewProgress} className="h-2" />
                                    <p className="mt-2 text-xs text-muted-foreground">
                                      {reviewProgress}% tamamlandi
                                    </p>
                                  </div>
                                  {nextSuggestedCourse && (
                                    <Button
                                      variant="outline"
                                      size="sm"
                                      onClick={() => {
                                        setCourseFilter('All');
                                        setCourseQuery('');
                                        setFocusedCourseId(nextSuggestedCourse.courseId);
                                      }}
                                    >
                                      Siradaki dersi ac
                                    </Button>
                                  )}
                                </div>
                              </div>

                              {filteredOptionalSurveyCourses.length === 0 ? (
                                <Alert>
                                  <Info className="h-4 w-4" />
                                  <AlertDescription>
                                    Secili filtre veya arama ile eslesen ders bulunamadi. Filtreleri temizleyip tekrar deneyebilirsin.
                                  </AlertDescription>
                                </Alert>
                              ) : (
                                filteredOptionalSurveyCourses.map(course => (
                                  <SelectableCourseCard
                                    key={course.courseId}
                                    course={course}
                                    intention={selectedIntentions[course.courseId]}
                                    reviewed={reviewedCourseIdSet.has(course.courseId)}
                                    emphasized={focusedCourseId === course.courseId}
                                    onSelect={(intention) => {
                                      setSelectedIntentions(prev => ({
                                        ...prev,
                                        [course.courseId]: intention,
                                      }));
                                      setReviewedCourseIds(prev => (
                                        prev.includes(course.courseId)
                                          ? prev
                                          : [...prev, course.courseId]
                                      ));
                                    }}
                                  />
                                ))
                              )}
                            </>
                          )}
                        </CardContent>
                      </Card>

                      <Card>
                        <CardHeader className="pb-4">
                          <SurveySectionHeader
                            step="3"
                            title="Opsiyonel program tercihleri"
                            description="Bu alan kararını değil, ders programinin nasil sekillenmesini istedigini anlatir."
                          />
                        </CardHeader>
                        <CardContent className="grid gap-4 md:grid-cols-2">
                          <PreferenceGroup
                            title="Gun tercihleri"
                            description="Programin yogunlasmasini istedigin gunleri sec."
                            options={DAY_OPTIONS}
                            selectedValues={preferredDays}
                            onToggle={(value) => setPreferredDays(prev => toggleArrayValue(prev, value))}
                          />

                          <PreferenceGroup
                            title="Saat tercihleri"
                            description="Sabah, ogle ya da aksam agirlikli tercihlerini isaretle."
                            options={TIME_OPTIONS}
                            selectedValues={preferredTimeRanges}
                            onToggle={(value) => setPreferredTimeRanges(prev => toggleArrayValue(prev, value))}
                          />
                        </CardContent>
                      </Card>
                    </>
                  )}
                </div>

                <div className="space-y-4 xl:sticky xl:top-24">
                  <Card className="border-border bg-white shadow-sm">
                    <CardHeader className="pb-3">
                      <CardTitle className="text-base">Karar Paneli</CardTitle>
                      <p className="text-sm text-muted-foreground">
                        Kaydetmeden once asagidaki kontrol listesini hizlica tamamla.
                      </p>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="space-y-3 rounded-2xl border border-border bg-slate-50/80 p-4">
                        <ChecklistItem
                          checked={lockedCount === 0 || !needsLockedSubmission}
                          title="Zorunlu dersler kayda hazir"
                          description={needsLockedSubmission
                            ? 'Sistemin ekledigi zorunlu derslerin de kayda gecmesi icin anketi en az bir kez gonder.'
                            : 'Zorunlu dersler bu gonderimde hesaba katilacak.'}
                        />
                        <ChecklistItem
                          checked={remainingOptionalCount === 0}
                          title="Opsiyonel derslerde karar verildi"
                          description={remainingOptionalCount > 0
                            ? `${remainingOptionalCount} ders daha incelemeyi dusunebilirsin.`
                            : 'Tum opsiyonel dersleri en az bir kez degerlendirdin.'}
                        />
                        <ChecklistItem
                          checked={!hasUnsavedChanges && !needsLockedSubmission}
                          title="Kaydetme ihtiyaci kalmadi"
                          description={hasUnsavedChanges || needsLockedSubmission
                            ? 'Guncel secimlerin henuz sunucuya kaydedilmedi.'
                            : 'Bu panelde gorulen secimler kayitli durumda.'}
                        />
                      </div>

                      <div className="space-y-2">
                        <div className="flex items-center justify-between text-sm">
                          <span className="text-muted-foreground">Opsiyonel ders inceleme durumu</span>
                          <span className="font-medium text-foreground">{reviewedOptionalCount}/{optionalSurveyCourses.length || 0}</span>
                        </div>
                        <Progress value={reviewProgress} className="h-2" />
                      </div>

                      <div className="space-y-3 text-sm">
                        <SummaryRow label="Toplam niyet" value={`${selectedCount} ders`} />
                        <SummaryRow label="Zorunlu kilitli" value={`${lockedCount} ders`} />
                        <SummaryRow label="Bu donem al" value={`${manualCertainCount} ders`} />
                        <SummaryRow label="Yedekte tut" value={`${maybeCount} ders`} />
                        <SummaryRow label="Gun tercihi" value={preferredDays.join(', ') || 'Secilmedi'} />
                        <SummaryRow label="Saat tercihi" value={preferredTimeRanges.join(', ') || 'Secilmedi'} />
                      </div>

                      <Separator />

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
                            {needsLockedSubmission ? 'Anketi Gonder' : 'Degisiklikleri Kaydet'}
                          </>
                        )}
                      </Button>

                      <p className="text-xs text-muted-foreground">
                        Kaydetme isleminden sonra anket kapanana kadar secimlerini yeniden duzenleyebilirsin.
                      </p>

                      {surveySaved && !hasUnsavedChanges && !needsLockedSubmission && (
                        <p className="text-xs font-medium text-emerald-700">
                          Secimlerin basariyla kaydedildi.
                        </p>
                      )}
                      {!surveySaved && surveyFeedback && (
                        <p className="text-xs font-medium text-destructive">
                          {surveyFeedback}
                        </p>
                      )}
                    </CardContent>
                  </Card>
                </div>
              </div>

              <div className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-white/95 px-4 py-3 backdrop-blur xl:hidden">
                <div className="mx-auto flex max-w-5xl items-center gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-semibold text-foreground">
                      {needsLockedSubmission
                        ? 'Anket gonderimi bekleniyor'
                        : hasUnsavedChanges
                          ? 'Kaydedilmemis degisiklik var'
                          : 'Secimler kayitli'}
                    </p>
                    <p className="truncate text-xs text-muted-foreground">
                      {selectedCount} ders niyeti • {lockedCount} zorunlu • {maybeCount} belki
                    </p>
                  </div>
                  <Button
                    onClick={handleSaveSurvey}
                    disabled={saveSurvey.isPending}
                    className="shrink-0 gap-2 bg-[#003366] px-4 hover:bg-[#002244]"
                  >
                    {saveSurvey.isPending ? (
                      <>
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Kaydediliyor
                      </>
                    ) : (
                      <>
                        <CheckCircle2 className="h-4 w-4" />
                        {needsLockedSubmission ? 'Gonder' : 'Kaydet'}
                      </>
                    )}
                  </Button>
                </div>
              </div>
            </>
          ) : null}
        </TabsContent>
      </Tabs>
    </div>
  );
}

function RecommendationCard({
  recommendation,
  surveyCourse,
  currentIntention,
  onQuickAction,
}: {
  recommendation: CourseRecommendationDto;
  surveyCourse?: {
    courseId: number;
    isLocked: boolean;
    lockedIntention?: CourseIntentionLevel | null;
  };
  currentIntention?: CourseIntentionLevel;
  onQuickAction: (courseId: number, intention?: CourseIntentionLevel) => void;
}) {
  const activeIntention = surveyCourse?.isLocked
    ? surveyCourse.lockedIntention ?? 'Certain'
    : currentIntention;
  const quickActionMessage = surveyCourse
    ? surveyCourse.isLocked
      ? 'Bu ders ankette zaten kesin alinacak olarak kilitli. Ayrintiyi gormek icin anket tarafina gec.'
      : activeIntention === 'Certain'
        ? 'Bu ders anketinde bu donem al olarak isaretli. Istersen belkiye cekebilirsin.'
        : activeIntention === 'Maybe'
          ? 'Bu ders anketinde yedekte tutuluyor. Eminsen bu donem al durumuna gecebilirsin.'
          : 'Bu dersi uygun bulursan anket tarafina tek tikla aktarabilirsin.'
    : 'Bu ders aktif ankette secilebilir listede gorunmuyor; yine de katalog ve ders kaydinda kontrol etmeye deger.';

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
              <p className="mt-1 text-xs font-semibold text-[#003366]">
                {getRecommendationStrengthLabel(recommendation.priorityScore)}
              </p>
            </div>
          </div>
        </div>

        <div className="grid gap-5 px-5 py-4 lg:grid-cols-[1.1fr_0.9fr]">
          <div className="space-y-4">
            <div className="rounded-2xl border border-slate-200 bg-slate-50/70 p-4">
              <p className="text-sm font-semibold text-foreground">
                {getRecommendationNarrative(recommendation)}
              </p>
              <p className="mt-1 text-sm leading-6 text-muted-foreground">
                {getRecommendationSecondaryNarrative(recommendation)}
              </p>
            </div>

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
            <div className="rounded-xl bg-white px-3 py-3 ring-1 ring-border">
              <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">Hizli aksiyon</p>
              <p className="mt-1 text-sm leading-6 text-muted-foreground">
                {quickActionMessage}
              </p>

              {surveyCourse ? (
                <div className="mt-3 flex flex-wrap gap-2">
                  {surveyCourse.isLocked ? (
                    <Button variant="outline" size="sm" onClick={() => onQuickAction(recommendation.courseId)}>
                      Ankette gor
                    </Button>
                  ) : (
                    <>
                      <Button
                        size="sm"
                        className="bg-[#003366] hover:bg-[#002244]"
                        onClick={() => onQuickAction(recommendation.courseId, 'Certain')}
                      >
                        Bu donem al
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => onQuickAction(recommendation.courseId, 'Maybe')}
                      >
                        Yedekte tut
                      </Button>
                    </>
                  )}
                </div>
              ) : (
                <div className="mt-3">
                  <Button asChild variant="outline" size="sm">
                    <Link to="/ders-kayit">Ders kaydina git</Link>
                  </Button>
                </div>
              )}
            </div>

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

function SurveySectionHeader({
  step,
  title,
  description,
  aside,
}: {
  step: string;
  title: string;
  description: string;
  aside?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
      <div className="flex items-start gap-3">
        <div className="mt-0.5 inline-flex h-7 w-7 items-center justify-center rounded-full bg-[#003366] text-xs font-semibold text-white">
          {step}
        </div>
        <div>
          <CardTitle className="text-base">{title}</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">{description}</p>
        </div>
      </div>
      {aside}
    </div>
  );
}

function SurveyStatCard({ label, value, helper }: { label: string; value: string; helper: string }) {
  return (
    <div className="rounded-2xl border border-border bg-slate-50/70 p-4">
      <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">{label}</p>
      <p className="mt-2 text-2xl font-semibold tracking-tight text-foreground">{value}</p>
      <p className="mt-1 text-xs text-muted-foreground">{helper}</p>
    </div>
  );
}

function HeroGuidePill({
  icon,
  title,
  description,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
}) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/10 p-4">
      <div className="flex items-center gap-2 text-sm font-semibold text-white">
        <span className="inline-flex h-7 w-7 items-center justify-center rounded-full bg-white/10 text-white">
          {icon}
        </span>
        {title}
      </div>
      <p className="mt-2 text-sm leading-6 text-white/70">{description}</p>
    </div>
  );
}

function InlineMetric({ label, value, helper }: { label: string; value: string; helper: string }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">{label}</p>
      <p className="mt-2 text-2xl font-semibold tracking-tight text-foreground">{value}</p>
      <p className="mt-1 text-xs text-muted-foreground">{helper}</p>
    </div>
  );
}

function ChecklistItem({
  checked,
  title,
  description,
}: {
  checked: boolean;
  title: string;
  description: string;
}) {
  return (
    <div className="flex items-start gap-3">
      <div className={cn(
        'mt-0.5 inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full border',
        checked
          ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
          : 'border-amber-200 bg-amber-50 text-amber-700'
      )}>
        {checked ? <CheckCircle2 className="h-4 w-4" /> : <Clock3 className="h-4 w-4" />}
      </div>
      <div>
        <p className="text-sm font-semibold text-foreground">{title}</p>
        <p className="mt-1 text-sm leading-6 text-muted-foreground">{description}</p>
      </div>
    </div>
  );
}

function FilterChip({
  active,
  label,
  onClick,
}: {
  active: boolean;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'rounded-full border px-3 py-1.5 text-sm font-medium transition-colors',
        active
          ? 'border-[#003366] bg-[#003366] text-white'
          : 'border-border bg-white text-muted-foreground hover:bg-muted/40'
      )}
    >
      {label}
    </button>
  );
}

function LockedCourseCard({
  course,
}: {
  course: {
    courseId: number;
    courseCode: string;
    courseName: string;
    ects: number;
    isCompulsory: boolean;
    takeStatus: string;
    previousGrade?: string | null;
    isBlockedByAdvisor: boolean;
    lockReason?: string | null;
  };
}) {
  return (
    <div className="rounded-2xl border border-red-200 bg-white p-4 shadow-sm">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-mono text-sm font-semibold text-primary">{course.courseCode}</span>
            <span className="font-medium text-foreground">{course.courseName}</span>
            <Badge variant={course.isCompulsory ? 'accent' : 'secondary'}>
              {course.isCompulsory ? 'Zorunlu' : 'Secmeli'}
            </Badge>
            <Badge variant="destructive">
              {course.takeStatus}
              {course.previousGrade ? ` (${course.previousGrade})` : ''}
            </Badge>
            <Badge variant="info">Sistem kilidi</Badge>
            {course.isBlockedByAdvisor && (
              <Badge variant="warning">Danisman notu var</Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            {course.ects} AKTS
            {course.lockReason ? ` • ${course.lockReason}` : ''}
          </p>
        </div>

        <div className="inline-flex items-center gap-2 rounded-full border border-red-200 bg-red-50 px-4 py-2 text-sm font-semibold text-red-700">
          <Lock className="h-4 w-4" />
          Kesin alinacak
        </div>
      </div>
    </div>
  );
}

function SelectableCourseCard({
  course,
  intention,
  reviewed,
  emphasized,
  onSelect,
}: {
  course: {
    courseId: number;
    courseCode: string;
    courseName: string;
    ects: number;
    suggestedSemester: number;
    isCompulsory: boolean;
    takeStatus: string;
    previousGrade?: string | null;
  };
  intention?: CourseIntentionLevel;
  reviewed: boolean;
  emphasized: boolean;
  onSelect: (value: CourseIntentionLevel | undefined) => void;
}) {
  return (
    <div
      id={`survey-course-${course.courseId}`}
      className={cn(
        'rounded-2xl border bg-white p-4 shadow-sm transition-all',
        emphasized
          ? 'border-[#003366]/35 ring-2 ring-[#003366]/15 shadow-md'
          : 'border-border'
      )}
    >
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_360px] lg:items-center">
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-mono text-sm font-semibold text-primary">{course.courseCode}</span>
            <span className="font-medium text-foreground">{course.courseName}</span>
            <Badge variant={course.isCompulsory ? 'accent' : 'secondary'}>
              {course.isCompulsory ? 'Zorunlu' : 'Secmeli'}
            </Badge>
            {course.takeStatus !== 'Normal' && (
              <Badge variant={course.takeStatus === 'Gecikmis' ? 'warning' : 'info'}>
                {course.takeStatus}
              </Badge>
            )}
            {course.previousGrade && (
              <Badge variant="outline">Onceki not: {course.previousGrade}</Badge>
            )}
          </div>
          <p className="text-sm leading-6 text-slate-700">
            {getSelectableCourseHint(course)}
          </p>
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
            <span>{course.ects} AKTS</span>
            <span>{formatSelectableCourseMeta(course.takeStatus, course.suggestedSemester)}</span>
            {reviewed && (
              <span className="inline-flex items-center gap-1 font-medium text-emerald-700">
                <CheckCircle2 className="h-3.5 w-3.5" />
                Gozden gecirildi
              </span>
            )}
            {!reviewed && !intention && (
              <span className="font-medium text-amber-700">
                Henuz karar verilmedi
              </span>
            )}
          </div>
        </div>

        <div className="w-full rounded-2xl border border-border bg-slate-50/80 p-3 lg:justify-self-end">
          <p className="px-1 pb-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
            Bu ders icin niyetin
          </p>
          <div className="grid gap-2 md:grid-cols-3">
            <SelectionButton
              active={!intention}
              tone="neutral"
              label="Simdilik alma"
              description="Bu donem niyet verme"
              onClick={() => onSelect(undefined)}
            />
            <SelectionButton
              active={intention === 'Certain'}
              tone="certain"
              label="Bu donem al"
              description="Plana dahil et"
              onClick={() => onSelect('Certain')}
            />
            <SelectionButton
              active={intention === 'Maybe'}
              tone="maybe"
              label="Yedekte tut"
              description="Kosullar uygunsa ekle"
              onClick={() => onSelect('Maybe')}
            />
          </div>
        </div>
      </div>
    </div>
  );
}

function SelectionButton({
  active,
  tone,
  label,
  description,
  onClick,
}: {
  active: boolean;
  tone: 'neutral' | 'certain' | 'maybe';
  label: string;
  description: string;
  onClick: () => void;
}) {
  const toneClasses = {
    neutral: active
      ? 'border-slate-300 bg-slate-100 text-slate-900'
      : 'border-border bg-background text-muted-foreground hover:bg-muted/40',
    certain: active
      ? 'border-[#003366] bg-[#003366] text-white'
      : 'border-border bg-background text-foreground hover:bg-[#003366]/5',
    maybe: active
      ? 'border-amber-300 bg-amber-100 text-amber-900'
      : 'border-border bg-background text-foreground hover:bg-amber-50',
  } as const;

  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'inline-flex w-full flex-col items-start rounded-xl border px-3 py-3 text-left transition-colors',
        toneClasses[tone]
      )}
    >
      <span className="text-sm font-semibold">{label}</span>
      <span className={cn('mt-1 text-xs leading-5', active ? 'text-current/80' : 'text-muted-foreground')}>
        {description}
      </span>
    </button>
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

function buildPreferenceKey(values: string[]) {
  return [...values]
    .sort((a, b) => a.localeCompare(b, 'tr'))
    .join('|');
}

function formatSelectableCourseMeta(takeStatus: string, suggestedSemester: number) {
  return takeStatus === 'Normal'
    ? `${suggestedSemester}. Donem`
    : takeStatus;
}

function getTakeStatusPriority(takeStatus: string) {
  switch (takeStatus) {
    case 'Alttan':
      return 0;
    case 'Gecikmis':
      return 1;
    case 'Normal':
      return 2;
    default:
      return 3;
  }
}

function getPaceProfile(value: number) {
  if (value <= 35) {
    return {
      label: 'Guvenli tempo',
      shortLabel: 'riski azalt',
      description: 'Sistem daha yonetilebilir ders karmalarini one cikarir; zorlayici kombinasyonlari daha temkinli onerir.',
    };
  }

  if (value <= 70) {
    return {
      label: 'Dengeli tempo',
      shortLabel: 'dengeyi koru',
      description: 'Mezuniyet hizini korurken ders yukunu kontrol altinda tutmaya calisan en dengeli mod.',
    };
  }

  return {
    label: 'Hizli tempo',
    shortLabel: 'agresif ilerle',
    description: 'Sistem kritik zincirleri ve daha fazla dersi one cikarir; yuk artisina ragmen hiz kazanmayi hedefler.',
  };
}

function getRecommendationStrengthLabel(score: number) {
  if (score >= 90) return 'Hemen degerlendir';
  if (score >= 80) return 'Guclu aday';
  if (score >= 65) return 'Plana uygun';
  return 'Esnek aday';
}

function getRecommendationNarrative(recommendation: CourseRecommendationDto) {
  if (recommendation.criticalPath.estimatedDelaySemesters >= 2) {
    return `Bu dersi ertelemek yaklasik ${recommendation.criticalPath.estimatedDelaySemesters} donemlik gecikme yaratabilir.`;
  }

  if (recommendation.isCompulsory) {
    return 'Bu ders ust donem akisini korudugu icin bu donem icin one cikiyor.';
  }

  if (recommendation.previousGrade) {
    return `Bu dersi daha once ${recommendation.previousGrade} ile tamamladigin ya da denedigin icin tekrar planlaman faydali olabilir.`;
  }

  if (recommendation.difficulty.score <= 40) {
    return 'Bu ders gorece yonetilebilir gorundugu icin donem planina rahat eklenebilir.';
  }

  return 'Bu ders tempo ve uygunluk hesaplarina gore bu donem icin mantikli bir aday gorunuyor.';
}

function getRecommendationSecondaryNarrative(recommendation: CourseRecommendationDto) {
  if (recommendation.advisorConstraintNote) {
    return recommendation.advisorConstraintNote;
  }

  if (recommendation.criticalPath.dependentCourses.length > 0) {
    return `${recommendation.criticalPath.dependentCourses.length} bagli ders bu karardan etkilenecek; zinciri acik tutmak icin dikkate deger.`;
  }

  return recommendation.reasons[0] ?? 'Guncel uygunluk, not ve ilerleme verileri bu dersi destekliyor.';
}

function getSelectableCourseHint(course: {
  isCompulsory: boolean;
  takeStatus: string;
  previousGrade?: string | null;
}) {
  if (course.takeStatus === 'Gecikmis') {
    return 'Bu ders geciktigi icin zincirde sikisma yaratabilir; uygunsa bu donem one almak daha saglikli olur.';
  }

  if (course.takeStatus === 'Alttan') {
    return 'Bu ders onceki donemlerden tasiniyor; mezuniyet yolunu rahatlatmak icin erken kapatmak iyi bir tercih olabilir.';
  }

  if (course.previousGrade) {
    return `Daha once ${course.previousGrade} notu aldigin bu dersi yeniden planlarken yukunu ve motivasyonunu birlikte dusun.`;
  }

  if (course.isCompulsory) {
    return 'Mufredat akisini korumak icin bu dersi bu donem almak genelde daha guvenli bir secimdir.';
  }

  return 'Bu ders uygunluk acisindan secilebilir bir aday; yukune gore kesin ya da yedek karari verebilirsin.';
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
