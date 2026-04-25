import { useTransition, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import { ArrowRight, ClipboardList, Gauge, Info, Lightbulb, Route, ShieldAlert, Sparkles, Target } from 'lucide-react';
import { useRecommendations } from '../hooks/useRecommendations';
import { getApiErrorMessage } from '../lib/apiError';
import type { CourseRecommendationDto } from '../types/recommendation';

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

export default function SmartPlanningPage() {
  const [pacePreference, setPacePreference] = useState(55);
  const [isPending, startTransition] = useTransition();
  const recommendationsQuery = useRecommendations(pacePreference);

  // useMemo ile stable referans (?? [] her render'da yeni array yaratırdı)
  const recommendations = useMemo(
    () => recommendationsQuery.data?.recommendations ?? [],
    [recommendationsQuery.data],
  );
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

  const paceProfile = getPaceProfile(pacePreference);
  const compulsoryCount = recommendations.filter(item => item.isCompulsory).length;

  return (
    <div className="space-y-6 pb-6">
      <Card className="overflow-hidden border-none shadow-sm">
        <div className="bg-[linear-gradient(135deg,#0a2348_0%,#123c76_58%,#0b2850_100%)] px-6 py-6 text-white">
          <div className="grid gap-6 xl:grid-cols-[minmax(0,1.4fr)_minmax(320px,0.9fr)] xl:items-end">
            <div className="space-y-5">
              <div className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-xs font-semibold tracking-[0.08em] uppercase">
                <Sparkles className="h-3.5 w-3.5" />
                Akilli Planlama
              </div>
              <div>
                <h1 className="font-heading text-3xl font-extrabold tracking-tight">Akilli ders onerileri</h1>
                <p className="mt-2 max-w-3xl text-sm leading-6 text-white/78">
                  Sistem uygun oldugun dersleri onceliklendirir, danisman uyarilarini gosterir ve dersleri karar
                  vermeni kolaylastiracak sekilde gruplar. Karari verdiginde ders kaydi ekranindan secimini yapabilirsin.
                </p>
              </div>

              <div className="grid gap-3 md:grid-cols-3">
                <HeroGuidePill
                  icon={<Target className="h-4 w-4" />}
                  title="1. Oneriyi tara"
                  description="Skor ve gecikme etkisine bakarak ilk adaylarini kisalt."
                />
                <HeroGuidePill
                  icon={<Lightbulb className="h-4 w-4" />}
                  title="2. Gerekceleri oku"
                  description="Kritik yol, zorluk ve danisman uyarilari karar vermeni kolaylastirir."
                />
                <HeroGuidePill
                  icon={<ArrowRight className="h-4 w-4" />}
                  title="3. Ders kaydina git"
                  description="Uygun gordugun dersleri ders kayit ekraninda tek akista sec."
                />
              </div>
            </div>

            <div className="rounded-3xl border border-white/15 bg-white/10 p-5 backdrop-blur">
              <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-white/65">Siradaki adim</p>
              <h2 className="mt-2 text-xl font-semibold text-white">
                Onerileri kullanarak bu donem yukunu sekillendir
              </h2>
              <p className="mt-2 text-sm leading-6 text-white/72">
                Listedeki dersleri skor ve gecikme etkisine gore inceleyip ders kayit ekraninda planini olustur.
              </p>

              <div className="mt-5">
                <Button asChild className="w-full gap-2 bg-white text-[#0b2850] hover:bg-white/90">
                  <Link to="/ders-kayit">
                    <ArrowRight className="h-4 w-4" />
                    Ders kayit ekranina git
                  </Link>
                </Button>
              </div>

              <div className="mt-5 grid gap-3 sm:grid-cols-3">
                <SummaryBadge label="Oneri" value={`${recommendations.length} ders`} />
                <SummaryBadge label="Zorunlu" value={`${compulsoryCount} ders`} />
                <SummaryBadge label="Tempo" value={paceProfile.shortLabel} />
              </div>
            </div>
          </div>
        </div>
      </Card>

      <Card className="overflow-hidden shadow-sm">
        <CardContent className="grid gap-6 p-6 lg:grid-cols-[minmax(0,1.15fr)_minmax(300px,0.85fr)]">
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="info">Adim 1</Badge>
            </div>

            <div>
              <h2 className="font-heading text-xl font-semibold tracking-tight text-foreground">
                Bu donem icin onceliklendirilmis dersler
              </h2>
              <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
                Skorlar karar vermeni kolaylastirmak icin var. Kartlardaki nedenleri oku, kritik yol gecikmesini gor
                ve uygun dersleri ders kayit ekraninda planla.
              </p>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <InlineMetric label="Toplam oneri" value={`${recommendations.length}`} helper="listelendi" />
              <InlineMetric label="Zorunlu aday" value={`${compulsoryCount}`} helper="oncelikli" />
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
                  <RecommendationCard key={item.courseId} recommendation={item} />
                ))}
              </div>
            </section>
          ))}
        </div>
      )}
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
                Uygun gordugun dersleri ders kayit ekraninda secime donusturebilirsin.
              </p>
              <div className="mt-3">
                <Button asChild variant="outline" size="sm">
                  <Link to="/ders-kayit">Ders kaydina git</Link>
                </Button>
              </div>
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
