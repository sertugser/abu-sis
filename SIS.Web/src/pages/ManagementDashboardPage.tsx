import { useState, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { ROUTES } from '@/lib/constants';
import { useAuth } from '../context/AuthContext';
import { useActiveTerm } from '../hooks/useActiveTerm';
import {
  useAcademicOperationsContext,
  useAcademicPlanningSummary,
  useDepartmentAcademicLevels,
  useDepartmentClassLists,
  useGradeLockReadiness,
} from '../hooks/useAcademicOperations';
import { getAcademicOperationsErrorMessage, type GradeLockReadiness } from '../api/academicOperations.api';
import {
  departmentManagerMenuGroups,
  administrativeStaffMenuGroups,
  deanMenuGroups,
  systemAdminMenuGroups,
} from '../components/layout/SidebarMenuGroups';
import type { DepartmentForecastSummary } from '../types/academicOperations';
import type { ScheduleValidationResult } from '../types/scheduling';
import {
  AlertCircle,
  AlertTriangle,
  ArrowRight,
  CalendarRange,
  Check,
  CheckCircle2,
  ClipboardList,
  GraduationCap,
  Layers,
  Network,
  Pencil,
  RotateCcw,
  TrendingUp,
  Users,
  XCircle,
} from 'lucide-react';

/* ─────────────────────────── Main Page ─────────────────────────── */

export default function ManagementDashboardPage() {
  const { user } = useAuth();
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();

  const context = contextQuery.data ?? null;
  const termId = activeTerm?.id;

  // For roles that can view all departments (SystemAdmin, AdministrativeStaff),
  // let the user pick which department's stats to display.
  const canViewAll = context?.canViewAllDepartments ?? false;
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);

  // Resolve the effective department id
  const departmentId = canViewAll
    ? (selectedDeptId ?? context?.accessibleDepartments[0]?.departmentId ?? null)
    : (context?.defaultDepartmentId ?? null);

  const planningQuery = useAcademicPlanningSummary(departmentId, termId, !!departmentId && !!termId);
  const classListQuery = useDepartmentClassLists(departmentId, termId, !!departmentId && !!termId);
  const gradeQuery = useGradeLockReadiness(departmentId, termId, !!departmentId && !!termId);
  const levelsQuery = useDepartmentAcademicLevels(departmentId, !!departmentId);

  const forecast = context ? (planningQuery.data?.forecastSummary ?? null) : null;
  const validation = context ? planningQuery.data?.scheduleValidation : undefined;
  const classList = context ? classListQuery.data : undefined;
  const gradeLock = context ? gradeQuery.data : undefined;

  const activeDept = context?.accessibleDepartments.find(d => d.departmentId === departmentId);
  const departmentName = canViewAll ? '' : (activeDept?.departmentName ?? '');

  return (
    <div className="space-y-6">
      {/* ── Hero Banner — renders immediately from JWT ── */}
      <div className="bg-gradient-to-r from-primary via-primary-light to-accent rounded-2xl overflow-hidden px-6 py-5 sm:px-8 sm:py-6">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div>
            <h1 className="font-heading text-xl font-bold text-white">
              Hoş geldiniz, {user?.name ?? ''}
            </h1>
            <p className="text-sm text-white/80">
              {context?.roleLabel ?? ''}
              {departmentName ? ` · ${departmentName}` : ''}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
          </div>
        </div>
        {context != null && canViewAll && context.accessibleDepartments.length > 0 && (
          <div className="mt-3">
            <select
              value={departmentId ?? ''}
              onChange={e => setSelectedDeptId(e.target.value ? Number(e.target.value) : null)}
              className="rounded-lg bg-white/15 border border-white/30 text-white text-sm px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-white/40"
            >
              {context.accessibleDepartments.map(dept => (
                <option key={dept.departmentId} value={dept.departmentId} className="text-foreground bg-background">
                  {dept.departmentCode} — {dept.departmentName}
                </option>
              ))}
            </select>
          </div>
        )}
        {context != null && !canViewAll && context.accessibleDepartments.length > 1 && (
          <div className="flex flex-wrap gap-1.5 mt-3">
            {context.accessibleDepartments.map(dept => (
              <span
                key={dept.departmentId}
                className={`rounded-md px-2.5 py-1 text-xs text-white font-medium ${
                  dept.departmentId === departmentId ? 'bg-white/25' : 'bg-white/10'
                }`}
              >
                {dept.departmentCode}
              </span>
            ))}
          </div>
        )}
      </div>

      {/* ── Context loading / error / no-department states ── */}
      {contextQuery.isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {[1, 2, 3, 4, 5].map(i => (
            <Skeleton key={i} className="h-40 rounded-xl" />
          ))}
        </div>
      ) : contextQuery.isError || !context ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Yönetim görünümü açılamadı</AlertTitle>
          <AlertDescription>
            {getAcademicOperationsErrorMessage(contextQuery.error, 'Veriler yüklenemedi.')}
          </AlertDescription>
        </Alert>
      ) : departmentId === null && !canViewAll ? (
        <Alert variant="warning">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Bölüm atamanız bulunamadı</AlertTitle>
          <AlertDescription>
            Lütfen sistem yöneticisi ile iletişime geçin.
          </AlertDescription>
        </Alert>
      ) : (
        <div className="space-y-6">
          {/* ── Status Cards — problems first ── */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 stagger-children">
            <CapacityStatusCard
              forecast={forecast}
              isLoading={planningQuery.isLoading}
              isError={planningQuery.isError}
            />
            <ScheduleHealthCard
              validation={validation}
              isLoading={planningQuery.isLoading}
              isError={planningQuery.isError}
            />
            <GradeEntryCard
              gradeLock={gradeLock}
              isLoading={gradeQuery.isLoading}
              isError={gradeQuery.isError}
            />
          </div>

          {/* ── Overview Stats ── */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <CompactStatCard
              icon={Users}
              iconBg="bg-primary/8 text-primary"
              value={
                classListQuery.isLoading || levelsQuery.isLoading
                  ? undefined
                  : `${classList?.totalStudents ?? 0} / ${levelsQuery.data?.totalStudents ?? 0}`
              }
              label="Derse Kayıtlı / Toplam Öğrenci"
              isError={classListQuery.isError && levelsQuery.isError}
            />
            <CompactStatCard
              icon={Layers}
              iconBg="bg-accent/10 text-accent"
              value={classListQuery.isLoading ? undefined : (classList?.totalSections ?? 0)}
              label="Aktif Şube"
              isError={classListQuery.isError}
            />
          </div>

          {/* ── Quick Access ── */}
          <QuickAccessSection />
        </div>
      )}
    </div>
  );
}

/* ────────────────── Capacity Status Card ────────────────── */

function CapacityStatusCard({
  forecast,
  isLoading,
  isError,
}: {
  forecast: DepartmentForecastSummary | null;
  isLoading: boolean;
  isError: boolean;
}) {
  const delta = forecast?.capacitySummary?.sectionDelta ?? 0;
  const isBalanced = delta === 0;
  const isDeficient = delta > 0;
  const coursesNeedingMore = forecast?.capacitySummary?.coursesNeedingMoreSections ?? 0;

  const borderColor = !forecast
    ? 'border-l-border'
    : isDeficient
      ? 'border-l-[var(--color-destructive)]'
      : isBalanced
        ? 'border-l-[var(--color-success)]'
        : 'border-l-[var(--color-warning)]';

  return (
    <Card className={`border-l-4 ${borderColor}`}>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center gap-2">
          <TrendingUp className="h-4.5 w-4.5 text-primary" />
          Kapasite Durumu
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? (
          <Skeleton className="h-16 w-full" />
        ) : isError ? (
          <p className="text-sm text-muted-foreground">Veri yüklenemedi</p>
        ) : !forecast ? (
          <p className="text-sm text-muted-foreground">Tahmin verisi henüz oluşturulmadı</p>
        ) : isBalanced ? (
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-5 w-5 text-success" />
            <p className="text-sm font-medium text-success">Kapasite dengeli</p>
          </div>
        ) : isDeficient ? (
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <XCircle className="h-5 w-5 text-destructive" />
              <div>
                <p className="font-heading text-2xl font-bold text-destructive">{delta}</p>
                <p className="text-sm text-muted-foreground">şube eksik</p>
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              {coursesNeedingMore} ders ek şube bekliyor
            </p>
          </div>
        ) : (
          <div className="flex items-center gap-2">
            <AlertTriangle className="h-5 w-5 text-warning" />
            <div>
              <p className="font-heading text-2xl font-bold text-warning">{Math.abs(delta)}</p>
              <p className="text-sm text-muted-foreground">fazla kapasite</p>
            </div>
          </div>
        )}

        {forecast && (
          <Link
            to={ROUTES.COURSE_APPROVAL}
            className="inline-flex items-center gap-1.5 text-xs font-medium text-primary hover:underline"
          >
            Ders Onay İşlemleri <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        )}
      </CardContent>
    </Card>
  );
}

/* ────────────────── Compact Stat Card ────────────────── */

function CompactStatCard({
  icon: Icon,
  iconBg,
  value,
  label,
  isError,
}: {
  icon: React.ComponentType<{ className?: string }>;
  iconBg: string;
  value: number | string | undefined;
  label: string;
  isError?: boolean;
}) {
  return (
    <Card>
      <CardContent className="p-5">
        <div className="flex items-center gap-3">
          <div className={`flex h-10 w-10 items-center justify-center rounded-xl ${iconBg}`}>
            <Icon className="h-5 w-5" />
          </div>
          <div>
            {isError ? (
              <p className="text-sm text-muted-foreground">Veri yüklenemedi</p>
            ) : value === undefined ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <p className="font-heading text-3xl font-bold text-foreground">{value}</p>
            )}
            <p className="text-sm text-muted-foreground">{label}</p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

/* ────────────────── Schedule Health Card ────────────────── */

function ScheduleHealthCard({
  validation,
  isLoading,
  isError,
}: {
  validation: ScheduleValidationResult | undefined;
  isLoading: boolean;
  isError: boolean;
}) {
  const conflictCount = validation?.conflicts?.length ?? 0;
  const hasConflicts = conflictCount > 0;
  const borderColor = hasConflicts
    ? 'border-l-[var(--color-destructive)]'
    : 'border-l-[var(--color-success)]';

  return (
    <Card className={`border-l-4 ${borderColor}`}>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center gap-2">
          <CalendarRange className="h-4.5 w-4.5 text-primary" />
          Program Sağlığı
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? (
          <Skeleton className="h-16 w-full" />
        ) : isError ? (
          <p className="text-sm text-muted-foreground">Veri yüklenemedi</p>
        ) : !hasConflicts ? (
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-5 w-5 text-success" />
            <p className="text-sm font-medium text-success">Çakışma tespit edilmedi</p>
          </div>
        ) : (
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <XCircle className="h-5 w-5 text-destructive" />
              <div>
                <p className="font-heading text-2xl font-bold text-destructive">{conflictCount}</p>
                <p className="text-sm text-muted-foreground">çakışma</p>
              </div>
            </div>
            <div className="space-y-1">
              {validation?.conflicts.slice(0, 2).map((c, i) => (
                <p key={i} className="text-xs text-muted-foreground leading-relaxed">
                  {c.message}
                </p>
              ))}
              {conflictCount > 2 && (
                <Link
                  to={ROUTES.SCHEDULE_CREATION}
                  className="text-xs text-primary hover:underline"
                >
                  ve {conflictCount - 2} daha...
                </Link>
              )}
            </div>
          </div>
        )}

        <Link
          to={ROUTES.SCHEDULE_CREATION}
          className="inline-flex items-center gap-1.5 text-xs font-medium text-primary hover:underline"
        >
          Ders Programı Oluşturma <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </CardContent>
    </Card>
  );
}

/* ────────────────── Grade Entry Card ────────────────── */

function GradeEntryCard({
  gradeLock,
  isLoading,
  isError,
}: {
  gradeLock: GradeLockReadiness | undefined;
  isLoading: boolean;
  isError: boolean;
}) {
  const allGradesEntered = gradeLock?.allGradesEntered ?? false;
  const hasActiveRegistrations = gradeLock?.hasActiveRegistrations ?? false;
  const missingGradeCount = gradeLock?.missingGradeCount ?? 0;
  const totalSections = gradeLock?.totalSections ?? 0;

  const hasData = gradeLock != null;

  // 3 durum: kayıt yok (muted), tüm notlar girilmiş (success), eksik not var (destructive)
  const borderColor = !hasData || isError
    ? 'border-l-muted'
    : !hasActiveRegistrations
      ? 'border-l-[var(--color-warning)]'
      : allGradesEntered
        ? 'border-l-[var(--color-success)]'
        : 'border-l-[var(--color-destructive)]';

  return (
    <Card className={`border-l-4 ${borderColor}`}>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center gap-2">
          <GraduationCap className="h-4.5 w-4.5 text-primary" />
          Not Girişi
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? (
          <Skeleton className="h-16 w-full" />
        ) : isError || !hasData ? (
          <p className="text-sm text-muted-foreground">Veri yüklenemedi</p>
        ) : totalSections === 0 ? (
          <div className="flex items-center gap-2">
            <AlertCircle className="h-5 w-5 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">Bu bölümde aktif şube bulunamadı</p>
          </div>
        ) : !hasActiveRegistrations ? (
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <AlertCircle className="h-5 w-5 text-warning" />
              <p className="text-sm font-medium text-warning">Henüz aktif ders kaydı yok</p>
            </div>
            <p className="text-xs text-muted-foreground">
              {totalSections} şube açık, ancak onaylanmış kayıt bulunmuyor
            </p>
          </div>
        ) : allGradesEntered ? (
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-5 w-5 text-success" />
            <p className="text-sm font-medium text-success">Tüm notlar girilmiş</p>
          </div>
        ) : (
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <XCircle className="h-5 w-5 text-destructive" />
              <div>
                <p className="font-heading text-2xl font-bold text-destructive">{missingGradeCount}</p>
                <p className="text-sm text-muted-foreground">eksik not kaydı</p>
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              {totalSections} şube analiz edildi
            </p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/* ────────────────── Quick Access Section ────────────────── */

const QUICK_LINKS_STORAGE_KEY = 'sis-dashboard-quick-links';

const DEFAULT_QUICK_LINKS = [
  ROUTES.COURSE_APPROVAL,
  ROUTES.CLASS_LISTS,
  ROUTES.SCHEDULE_CREATION,
  ROUTES.TERM_CLOSING,
];

/** Icon overrides for well-known routes */
const ROUTE_ICONS: Record<string, React.ComponentType<{ className?: string }>> = {
  [ROUTES.COURSE_APPROVAL]: Network,
  [ROUTES.CLASS_LISTS]: ClipboardList,
  [ROUTES.SCHEDULE_CREATION]: CalendarRange,
  [ROUTES.TERM_CLOSING]: GraduationCap,
  [ROUTES.INSTRUCTOR_SECTIONS]: Layers,
};

interface QuickLinkOption {
  to: string;
  title: string;
  icon: React.ComponentType<{ className?: string }>;
  group: string;
}

function buildAllLinkOptions(): QuickLinkOption[] {
  const allGroups = [
    ...systemAdminMenuGroups,
    ...deanMenuGroups,
    ...departmentManagerMenuGroups,
    ...administrativeStaffMenuGroups,
  ];
  const seen = new Set<string>();
  return allGroups.flatMap(g =>
    g.children
      .filter(c => c.to !== ROUTES.DASHBOARD && !seen.has(c.to) && seen.add(c.to))
      .map(c => ({
        to: c.to,
        title: c.label,
        icon: ROUTE_ICONS[c.to] ?? (g.icon as React.ComponentType<{ className?: string }>),
        group: g.label,
      })),
  );
}

function getStoredQuickLinks(): string[] {
  try {
    const stored = localStorage.getItem(QUICK_LINKS_STORAGE_KEY);
    if (stored) {
      const parsed = JSON.parse(stored) as unknown;
      if (Array.isArray(parsed) && parsed.every(x => typeof x === 'string')) return parsed;
    }
  } catch { /* ignore */ }
  return DEFAULT_QUICK_LINKS;
}

function QuickAccessSection() {
  const [selectedRoutes, setSelectedRoutes] = useState(getStoredQuickLinks);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [draft, setDraft] = useState<string[]>([]);

  const allOptions = buildAllLinkOptions();

  const openEditor = useCallback(() => {
    setDraft([...selectedRoutes]);
    setDialogOpen(true);
  }, [selectedRoutes]);

  const toggleRoute = useCallback((route: string) => {
    setDraft(prev =>
      prev.includes(route) ? prev.filter(r => r !== route) : [...prev, route],
    );
  }, []);

  const saveChanges = useCallback(() => {
    localStorage.setItem(QUICK_LINKS_STORAGE_KEY, JSON.stringify(draft));
    setSelectedRoutes(draft);
    setDialogOpen(false);
  }, [draft]);

  const resetDefaults = useCallback(() => {
    setDraft([...DEFAULT_QUICK_LINKS]);
  }, []);

  // Build displayed links from selected routes, preserving order
  const displayedLinks = selectedRoutes
    .map(route => allOptions.find(o => o.to === route))
    .filter((o): o is QuickLinkOption => o != null);

  // Group options by category for the editor dialog
  const groupedOptions = allOptions.reduce<Record<string, QuickLinkOption[]>>((acc, opt) => {
    (acc[opt.group] ??= []).push(opt);
    return acc;
  }, {});

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium text-muted-foreground">Hızlı Erişim</p>
        <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
          <DialogTrigger asChild>
            <Button variant="ghost" size="sm" className="h-7 gap-1.5 text-xs text-muted-foreground" onClick={openEditor}>
              <Pencil className="h-3.5 w-3.5" />
              Düzenle
            </Button>
          </DialogTrigger>
          <DialogContent className="max-w-xl max-h-[80vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Hızlı Erişimi Düzenle</DialogTitle>
              <DialogDescription>
                Ana sayfada görmek istediğiniz kısayolları seçin.
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-4">
              {Object.entries(groupedOptions).map(([group, options]) => (
                <div key={group}>
                  <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                    {group}
                  </p>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
                    {options.map(opt => {
                      const isSelected = draft.includes(opt.to);
                      return (
                        <button
                          key={opt.to}
                          type="button"
                          onClick={() => toggleRoute(opt.to)}
                          className={`flex items-center gap-2.5 rounded-lg px-3 py-2 text-left text-sm transition-colors cursor-pointer ${
                            isSelected
                              ? 'bg-primary/10 text-primary font-medium'
                              : 'hover:bg-muted/50 text-foreground'
                          }`}
                        >
                          <div className={`flex h-5 w-5 shrink-0 items-center justify-center rounded border transition-colors ${
                            isSelected
                              ? 'bg-primary border-primary text-white'
                              : 'border-border'
                          }`}>
                            {isSelected && <Check className="h-3 w-3" />}
                          </div>
                          {opt.title}
                        </button>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>

            <div className="flex items-center justify-between pt-2 border-t">
              <Button variant="ghost" size="sm" className="gap-1.5 text-xs" onClick={resetDefaults}>
                <RotateCcw className="h-3.5 w-3.5" />
                Varsayılana Dön
              </Button>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" onClick={() => setDialogOpen(false)}>
                  İptal
                </Button>
                <Button size="sm" onClick={saveChanges}>
                  Kaydet ({draft.length})
                </Button>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      </div>

      {displayedLinks.length === 0 ? (
        <p className="text-sm text-muted-foreground py-4 text-center">
          Henüz kısayol eklenmedi. <button type="button" onClick={openEditor} className="text-primary hover:underline cursor-pointer">Düzenle</button> butonuyla ekleyebilirsiniz.
        </p>
      ) : (
        <div className="flex flex-wrap gap-3">
          {displayedLinks.map(link => (
            <QuickLinkCard key={link.to} icon={link.icon} title={link.title} to={link.to} />
          ))}
        </div>
      )}
    </div>
  );
}

function QuickLinkCard({
  icon: Icon,
  title,
  to,
}: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  to: string;
}) {
  return (
    <Link
      to={to}
      className="flex flex-1 min-w-[180px] items-center gap-3 rounded-xl border border-border/70 p-4 transition-colors hover:bg-muted/50 hover:border-primary/30 group"
    >
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary/8 text-primary group-hover:bg-primary/15 transition-colors">
        <Icon className="h-5 w-5" />
      </div>
      <p className="font-semibold text-foreground text-sm">{title}</p>
      <ArrowRight className="h-4 w-4 text-muted-foreground ml-auto shrink-0 opacity-0 group-hover:opacity-100 transition-opacity" />
    </Link>
  );
}
