import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import {
  AlertCircle,
  BookOpen,
  CheckCircle2,
  Layers,
  Users,
  User as UserIcon,
  XCircle,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import type {
  BulkOpenCoursesResultDto,
  OpenedCourseSummaryDto,
} from '../../types/courseOpening';

interface BulkOpenResultDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  result?: BulkOpenCoursesResultDto | null;
  /** Backend'e ulaşmadan oluşan hata (network/5xx). Var ise success path ezilir. */
  exceptionMessage?: string | null;
}

/**
 * Toplu ders açma sonrası kullanıcıya gösterilen profesyonel sonuç ekranı.
 * Hero header + renkli stat kartları + accent bar'lı course kartları (Yön A).
 *
 * Tüm içerik backend'den gelen `BulkOpenCoursesResultDto`'dan render edilir.
 * Hardcoded içerik yok.
 */
export function BulkOpenResultDialog({
  open,
  onOpenChange,
  result,
  exceptionMessage,
}: BulkOpenResultDialogProps) {
  // Exception path — DTO yok, sadece mesaj
  if (exceptionMessage) {
    return (
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="sm:max-w-lg p-0 overflow-hidden">
          <DialogTitle className="sr-only">Toplu ders açma başarısız</DialogTitle>
          <FailureHero />
          <div className="px-6 py-5 space-y-4">
            <div className="rounded-lg border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-destructive leading-relaxed">
              {exceptionMessage}
            </div>
            <div className="flex justify-end">
              <Button onClick={() => onOpenChange(false)}>Kapat</Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    );
  }

  if (!result) return null;

  const opened = result.totalCoursesOpened ?? 0;
  const sectionsCreated = result.totalSectionsCreated ?? 0;
  const totalCapacity = (result.openedCourses ?? []).reduce(
    (sum, c) => sum + (c.totalCapacity ?? 0),
    0,
  );
  const errors = result.errors ?? [];
  const hasErrors = errors.length > 0;
  const allFailed = opened === 0 && hasErrors;
  const partialSuccess = opened > 0 && hasErrors;

  const heroVariant: HeroVariant = allFailed
    ? 'failure'
    : partialSuccess
      ? 'partial'
      : 'success';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl p-0 overflow-hidden max-h-[88vh] flex flex-col gap-0">
        <DialogTitle className="sr-only">
          {heroVariant === 'failure'
            ? 'Hiçbir ders açılamadı'
            : heroVariant === 'partial'
              ? 'Açma kısmen tamamlandı'
              : 'Dersler başarıyla açıldı'}
        </DialogTitle>

        {/* HERO */}
        <Hero variant={heroVariant} openedCount={opened} errorCount={errors.length} />

        {/* CONTENT */}
        <div className="flex-1 min-h-0 overflow-y-auto">
          <div className="px-6 py-5 space-y-5">
            {/* Stat kartları — sadece başarı/kısmen durumda */}
            {opened > 0 && (
              <div className="grid grid-cols-3 gap-3">
                <StatCard
                  variant="blue"
                  icon={<BookOpen className="h-4 w-4" />}
                  label="Açılan ders"
                  value={opened}
                />
                <StatCard
                  variant="violet"
                  icon={<Layers className="h-4 w-4" />}
                  label="Oluşturulan şube"
                  value={sectionsCreated}
                />
                <StatCard
                  variant="emerald"
                  icon={<Users className="h-4 w-4" />}
                  label="Toplam kontenjan"
                  value={totalCapacity}
                />
              </div>
            )}

            {/* Açılan dersler */}
            {opened > 0 && (
              <section className="space-y-2">
                <SectionLabel>Açılan dersler</SectionLabel>
                <ul className="space-y-2">
                  {(result.openedCourses ?? []).map((course, idx) => (
                    <CourseCard key={course.courseId} course={course} index={idx} />
                  ))}
                </ul>
              </section>
            )}

            {/* Hatalar */}
            {hasErrors && (
              <section className="space-y-2">
                <SectionLabel tone="destructive">{errors.length} hata</SectionLabel>
                <ul className="space-y-1.5">
                  {errors.map((err, idx) => (
                    <li
                      key={idx}
                      className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm leading-snug"
                    >
                      <XCircle className="h-4 w-4 mt-0.5 shrink-0 text-destructive" />
                      <span className="text-destructive">{err}</span>
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </div>
        </div>

        {/* FOOTER */}
        <div className="border-t bg-muted/20 px-6 py-3 flex justify-end">
          <Button onClick={() => onOpenChange(false)}>Kapat</Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Hero
// ─────────────────────────────────────────────────────────────────────────────

type HeroVariant = 'success' | 'partial' | 'failure';

const HERO_STYLES: Record<HeroVariant, { gradient: string; iconRing: string }> = {
  success: {
    gradient: 'from-emerald-500 via-emerald-500 to-teal-600',
    iconRing: 'bg-white/15 ring-white/30',
  },
  partial: {
    gradient: 'from-amber-500 via-amber-500 to-orange-600',
    iconRing: 'bg-white/15 ring-white/30',
  },
  failure: {
    gradient: 'from-rose-500 via-red-500 to-red-600',
    iconRing: 'bg-white/15 ring-white/30',
  },
};

function Hero({
  variant,
  openedCount,
  errorCount,
}: {
  variant: HeroVariant;
  openedCount: number;
  errorCount: number;
}) {
  const { gradient, iconRing } = HERO_STYLES[variant];
  const Icon = variant === 'success' ? CheckCircle2 : AlertCircle;
  const title =
    variant === 'success'
      ? 'Dersler başarıyla açıldı'
      : variant === 'partial'
        ? 'Açma kısmen tamamlandı'
        : 'Hiçbir ders açılamadı';
  const subtitle =
    variant === 'success'
      ? 'Tüm seçili dersler aktif olarak yayında.'
      : variant === 'partial'
        ? `${openedCount} ders açıldı, ${errorCount} ders açılamadı.`
        : 'Toplu işlem geri alındı, hiçbir değişiklik yapılmadı.';

  return (
    <div
      className={cn(
        'relative overflow-hidden bg-gradient-to-br px-6 py-5 text-white',
        gradient,
      )}
    >
      {/* Subtle decorative blobs */}
      <div className="pointer-events-none absolute -top-12 -right-12 h-40 w-40 rounded-full bg-white/10 blur-2xl" />
      <div className="pointer-events-none absolute -bottom-16 -left-16 h-44 w-44 rounded-full bg-white/5 blur-2xl" />

      <div className="relative flex items-center gap-3.5">
        <div
          className={cn(
            'flex h-11 w-11 items-center justify-center rounded-xl ring-1 backdrop-blur-sm',
            iconRing,
          )}
        >
          <Icon className="h-5 w-5" />
        </div>
        <div className="min-w-0">
          <h2 className="text-base font-semibold leading-tight">{title}</h2>
          <p className="text-xs text-white/85 mt-0.5">{subtitle}</p>
        </div>
      </div>
    </div>
  );
}

function FailureHero() {
  return <Hero variant="failure" openedCount={0} errorCount={1} />;
}

// ─────────────────────────────────────────────────────────────────────────────
// Stat Card
// ─────────────────────────────────────────────────────────────────────────────

type StatVariant = 'blue' | 'violet' | 'emerald';

const STAT_STYLES: Record<
  StatVariant,
  { bg: string; border: string; iconBg: string; iconText: string; label: string; value: string }
> = {
  blue: {
    bg: 'bg-gradient-to-br from-blue-50 to-sky-50',
    border: 'border-blue-100',
    iconBg: 'bg-blue-100',
    iconText: 'text-blue-600',
    label: 'text-blue-700/80',
    value: 'text-blue-900',
  },
  violet: {
    bg: 'bg-gradient-to-br from-violet-50 to-purple-50',
    border: 'border-violet-100',
    iconBg: 'bg-violet-100',
    iconText: 'text-violet-600',
    label: 'text-violet-700/80',
    value: 'text-violet-900',
  },
  emerald: {
    bg: 'bg-gradient-to-br from-emerald-50 to-teal-50',
    border: 'border-emerald-100',
    iconBg: 'bg-emerald-100',
    iconText: 'text-emerald-600',
    label: 'text-emerald-700/80',
    value: 'text-emerald-900',
  },
};

function StatCard({
  variant,
  icon,
  label,
  value,
}: {
  variant: StatVariant;
  icon: React.ReactNode;
  label: string;
  value: number;
}) {
  const styles = STAT_STYLES[variant];
  return (
    <div
      className={cn(
        'relative rounded-xl border px-4 py-3 transition-shadow hover:shadow-sm',
        styles.bg,
        styles.border,
      )}
    >
      <div className="flex items-start justify-between">
        <div className="min-w-0">
          <div className={cn('text-2xl font-bold tabular-nums leading-none', styles.value)}>
            {value}
          </div>
          <div className={cn('text-[11px] font-medium mt-1.5 uppercase tracking-wide', styles.label)}>
            {label}
          </div>
        </div>
        <div className={cn('flex h-8 w-8 items-center justify-center rounded-lg', styles.iconBg, styles.iconText)}>
          {icon}
        </div>
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Course Card (accent bar + chips)
// ─────────────────────────────────────────────────────────────────────────────

function CourseCard({ course, index }: { course: OpenedCourseSummaryDto; index: number }) {
  return (
    <li
      className={cn(
        'group relative overflow-hidden rounded-lg border border-emerald-100 bg-white pl-4 pr-3.5 py-2.5',
        'transition-all hover:-translate-y-px hover:shadow-md hover:border-emerald-200',
      )}
      style={{ animationDelay: `${Math.min(index, 6) * 40}ms` }}
    >
      {/* Sol accent bar */}
      <span
        aria-hidden
        className="absolute left-0 top-0 bottom-0 w-1 bg-gradient-to-b from-emerald-400 to-teal-500"
      />

      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className="flex items-baseline gap-2">
            <span className="font-mono text-sm font-bold text-emerald-900 tracking-tight">
              {course.courseCode}
            </span>
            <span className="text-sm text-foreground/90 truncate">{course.courseName}</span>
          </div>

          <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1.5 text-xs">
            {/* Şube sayısı + numaraları chip olarak */}
            <span className="inline-flex items-center gap-1.5 text-muted-foreground">
              <Layers className="h-3 w-3" />
              <strong className="text-foreground">{course.sectionCount}</strong> şube
            </span>
            {course.sectionNumbers.length > 0 && (
              <div className="flex items-center gap-1">
                {course.sectionNumbers.map((n) => (
                  <span
                    key={n}
                    className="inline-flex h-5 min-w-[22px] items-center justify-center rounded-md bg-emerald-100 px-1.5 text-[10px] font-semibold text-emerald-800 tabular-nums"
                  >
                    {n}
                  </span>
                ))}
              </div>
            )}

            <span className="inline-flex items-center gap-1.5 text-muted-foreground">
              <Users className="h-3 w-3" />
              <strong className="text-foreground tabular-nums">{course.totalCapacity}</strong> kontenjan
            </span>

            {course.instructorName && (
              <span className="inline-flex items-center gap-1.5 text-muted-foreground">
                <UserIcon className="h-3 w-3" />
                <span className="text-foreground">{course.instructorName}</span>
              </span>
            )}
          </div>
        </div>

        {/* Pulse'lu yeşil checkmark */}
        <div className="relative shrink-0">
          <span className="absolute inset-0 rounded-full bg-emerald-400/30 animate-ping" />
          <span className="relative flex h-7 w-7 items-center justify-center rounded-full bg-emerald-500 text-white">
            <CheckCircle2 className="h-4 w-4" />
          </span>
        </div>
      </div>
    </li>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Section Label
// ─────────────────────────────────────────────────────────────────────────────

function SectionLabel({
  children,
  tone = 'default',
}: {
  children: React.ReactNode;
  tone?: 'default' | 'destructive';
}) {
  return (
    <div className="flex items-center gap-2">
      <span
        className={cn(
          'h-px flex-1',
          tone === 'destructive' ? 'bg-destructive/20' : 'bg-border',
        )}
      />
      <span
        className={cn(
          'text-[11px] font-semibold uppercase tracking-wider',
          tone === 'destructive' ? 'text-destructive' : 'text-muted-foreground',
        )}
      >
        {children}
      </span>
      <span
        className={cn(
          'h-px flex-1',
          tone === 'destructive' ? 'bg-destructive/20' : 'bg-border',
        )}
      />
    </div>
  );
}
