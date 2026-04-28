import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import {
  AlertCircle,
  AlertTriangle,
  ArrowRight,
  CalendarDays,
  CheckCircle2,
  Clock,
  GraduationCap,
  BookUser,
  Users,
  XCircle,
  Layers,
  LayoutGrid,
  BookOpen,
  Building2,
  ClipboardList,
  Pencil,
  Check,
  EyeOff,
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useAdminUsers } from '../hooks/useAdminUsers';
import { useAdminStudents } from '../hooks/useAdminStudents';
import { useAdminInstructors } from '../hooks/useAdminInstructors';
import { useAdminTerms } from '../hooks/useAdminTerms';
import { useActiveStudentCalendar, useAdminCalendarTerms } from '../hooks/useAcademicCalendar';
import { ROUTES } from '../lib/constants';
import DashboardAnnouncementWidget from '../components/announcements/DashboardAnnouncementWidget';

// ─── Helpers ────────────────────────────────────────────────────

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
}

function daysUntil(iso: string) {
  return Math.ceil((new Date(iso).getTime() - Date.now()) / 86400000);
}

function isWithinRange(start: string, end: string): boolean {
  const now = Date.now();
  return now >= new Date(start).getTime() && now <= new Date(end).getTime();
}

function isUpcoming(start: string): boolean {
  return Date.now() < new Date(start).getTime();
}

// ─── Quick Link Definitions ──────────────────────────────────────

const ALL_QUICK_LINKS = [
  { id: 'users',      icon: Users,        title: 'Kullanıcı Listesi', to: ROUTES.USER_MANAGEMENT },
  { id: 'students',   icon: GraduationCap,title: 'Öğrenci Rehberi',  to: ROUTES.STUDENT_DIRECTORY },
  { id: 'staff',      icon: BookUser,      title: 'Personel Listesi', to: ROUTES.INSTRUCTOR_DIRECTORY },
  { id: 'terms',      icon: CalendarDays,  title: 'Dönem Yönetimi',   to: ROUTES.TERM_MANAGEMENT },
  { id: 'structure',  icon: Layers,        title: 'Fakülte Tanımları', to: ROUTES.FACULTY_DEFINITIONS },
  { id: 'catalog',    icon: LayoutGrid,    title: 'Ders Kataloğu',    to: ROUTES.COURSE_CATALOG_ADMIN },
  { id: 'opening',    icon: BookOpen,      title: 'Ders Aç (Müfredat)', to: ROUTES.COURSE_OPENING },
  { id: 'classroom',  icon: Building2,     title: 'Derslik Doluluk',   to: ROUTES.CLASSROOM_DASHBOARD },
  { id: 'classlist',  icon: ClipboardList, title: 'Sınıf Listeleri',  to: ROUTES.CLASS_LISTS },
] as const;

type QuickLinkId = typeof ALL_QUICK_LINKS[number]['id'];

const LS_KEY = 'sis_admin_quick_links';

function loadVisibleIds(): QuickLinkId[] {
  try {
    const raw = localStorage.getItem(LS_KEY);
    if (raw) return JSON.parse(raw) as QuickLinkId[];
  } catch { /* ignore */ }
  return ALL_QUICK_LINKS.map(l => l.id);
}

function saveVisibleIds(ids: QuickLinkId[]) {
  localStorage.setItem(LS_KEY, JSON.stringify(ids));
}

// ─── Term Status Card ────────────────────────────────────────────

function TermStatusCard({
  activeTerm,
  isLoading,
}: {
  activeTerm: { name: string; termStart: string; termEnd: string } | undefined;
  isLoading: boolean;
}) {
  const isActive = activeTerm ? isWithinRange(activeTerm.termStart, activeTerm.termEnd) : false;

  const borderColor = !activeTerm
    ? 'border-l-[var(--color-destructive)]'
    : isActive
      ? 'border-l-[var(--color-success)]'
      : 'border-l-[var(--color-warning)]';

  return (
    <Card className={`border-l-4 ${borderColor}`}>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center gap-2">
          <CalendarDays className="h-4.5 w-4.5 text-primary" />
          Aktif Dönem
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? (
          <Skeleton className="h-16 w-full" />
        ) : !activeTerm ? (
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <XCircle className="h-5 w-5 text-destructive" />
              <p className="text-sm font-semibold text-destructive">Aktif dönem yok</p>
            </div>
            <p className="text-xs text-muted-foreground">Öğrenciler ders kaydı yapamıyor.</p>
          </div>
        ) : (
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <CheckCircle2 className="h-5 w-5 text-success" />
              <p className="text-sm font-semibold text-foreground">{activeTerm.name}</p>
            </div>
            <p className="text-xs text-muted-foreground">
              {fmtDate(activeTerm.termStart)} — {fmtDate(activeTerm.termEnd)}
            </p>
          </div>
        )}
        <Link
          to={ROUTES.TERM_MANAGEMENT}
          className="inline-flex items-center gap-1.5 text-xs font-medium text-primary hover:underline"
        >
          Dönem Yönetimi <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </CardContent>
    </Card>
  );
}

// ─── Registration Status Card ────────────────────────────────────

function RegistrationStatusCard({
  activeTerm,
  isLoading,
}: {
  activeTerm: { registrationStart?: string | null; registrationEnd?: string | null } | undefined;
  isLoading: boolean;
}) {
  const open = activeTerm?.registrationStart && activeTerm?.registrationEnd
    ? isWithinRange(activeTerm.registrationStart, activeTerm.registrationEnd)
    : false;
  const upcoming = activeTerm?.registrationStart && !open && isUpcoming(activeTerm.registrationStart);

  const borderColor = open
    ? 'border-l-[var(--color-success)]'
    : upcoming
      ? 'border-l-[var(--color-warning)]'
      : 'border-l-border';

  return (
    <Card className={`border-l-4 ${borderColor}`}>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center gap-2">
          <ClipboardList className="h-4.5 w-4.5 text-primary" />
          Ders Kaydı
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? (
          <Skeleton className="h-16 w-full" />
        ) : !activeTerm || (!activeTerm.registrationStart && !activeTerm.registrationEnd) ? (
          <p className="text-sm text-muted-foreground">Kayıt tarihi tanımlı değil.</p>
        ) : open ? (
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <CheckCircle2 className="h-5 w-5 text-success" />
              <p className="text-sm font-semibold text-success">Açık</p>
            </div>
            <p className="text-xs text-muted-foreground">
              Bitiş: {activeTerm.registrationEnd ? fmtDate(activeTerm.registrationEnd) : '—'}
            </p>
            <p className="text-xs font-medium text-success">
              {activeTerm.registrationEnd ? daysUntil(activeTerm.registrationEnd) : '—'} gün kaldı
            </p>
          </div>
        ) : upcoming ? (
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <Clock className="h-5 w-5 text-warning" />
              <p className="text-sm font-semibold text-warning">Henüz başlamadı</p>
            </div>
            <p className="text-xs text-muted-foreground">
              Başlangıç: {activeTerm.registrationStart ? fmtDate(activeTerm.registrationStart) : '—'}
            </p>
            <p className="text-xs font-medium text-warning">
              {activeTerm.registrationStart ? daysUntil(activeTerm.registrationStart) : '—'} gün sonra
            </p>
          </div>
        ) : (
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <AlertCircle className="h-5 w-5 text-muted-foreground" />
              <p className="text-sm text-muted-foreground">Tamamlandı</p>
            </div>
            <p className="text-xs text-muted-foreground">
              {activeTerm.registrationStart ? fmtDate(activeTerm.registrationStart) : '—'} — {activeTerm.registrationEnd ? fmtDate(activeTerm.registrationEnd) : '—'}
            </p>
          </div>
        )}
        <Link
          to={ROUTES.ACADEMIC_CALENDAR_ADMIN}
          className="inline-flex items-center gap-1.5 text-xs font-medium text-primary hover:underline"
        >
          Tarihleri Düzenle <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </CardContent>
    </Card>
  );
}

// ─── Add/Drop Status Card ────────────────────────────────────────

function AddDropStatusCard({
  activeTerm,
  isLoading,
}: {
  activeTerm: { addDropStart?: string | null; addDropEnd?: string | null } | undefined;
  isLoading: boolean;
}) {
  const open = activeTerm?.addDropStart && activeTerm?.addDropEnd
    ? isWithinRange(activeTerm.addDropStart, activeTerm.addDropEnd)
    : false;
  const upcoming = activeTerm?.addDropStart && !open && isUpcoming(activeTerm.addDropStart);

  const borderColor = open
    ? 'border-l-[var(--color-success)]'
    : upcoming
      ? 'border-l-[var(--color-warning)]'
      : 'border-l-border';

  return (
    <Card className={`border-l-4 ${borderColor}`}>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center gap-2">
          <BookOpen className="h-4.5 w-4.5 text-primary" />
          Ders Ekle / Bırak
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? (
          <Skeleton className="h-16 w-full" />
        ) : !activeTerm || (!activeTerm.addDropStart && !activeTerm.addDropEnd) ? (
          <p className="text-sm text-muted-foreground">Ekle/bırak tarihi tanımlı değil.</p>
        ) : open ? (
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <CheckCircle2 className="h-5 w-5 text-success" />
              <p className="text-sm font-semibold text-success">Açık</p>
            </div>
            <p className="text-xs text-muted-foreground">
              Bitiş: {activeTerm.addDropEnd ? fmtDate(activeTerm.addDropEnd) : '—'}
            </p>
            <p className="text-xs font-medium text-success">
              {activeTerm.addDropEnd ? daysUntil(activeTerm.addDropEnd) : '—'} gün kaldı
            </p>
          </div>
        ) : upcoming ? (
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <Clock className="h-5 w-5 text-warning" />
              <p className="text-sm font-semibold text-warning">Henüz başlamadı</p>
            </div>
            <p className="text-xs text-muted-foreground">
              Başlangıç: {activeTerm.addDropStart ? fmtDate(activeTerm.addDropStart) : '—'}
            </p>
            <p className="text-xs font-medium text-warning">
              {activeTerm.addDropStart ? daysUntil(activeTerm.addDropStart) : '—'} gün sonra
            </p>
          </div>
        ) : (
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <AlertCircle className="h-5 w-5 text-muted-foreground" />
              <p className="text-sm text-muted-foreground">Tamamlandı</p>
            </div>
            <p className="text-xs text-muted-foreground">
              {activeTerm.addDropStart ? fmtDate(activeTerm.addDropStart) : '—'} — {activeTerm.addDropEnd ? fmtDate(activeTerm.addDropEnd) : '—'}
            </p>
          </div>
        )}
        <Link
          to={ROUTES.ACADEMIC_CALENDAR_ADMIN}
          className="inline-flex items-center gap-1.5 text-xs font-medium text-primary hover:underline"
        >
          Tarihleri Düzenle <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </CardContent>
    </Card>
  );
}

// ─── Compact Stat (tüm kart tıklanabilir) ───────────────────────

function StatCard({
  icon: Icon,
  value,
  label,
  to,
  isLoading,
}: {
  icon: React.ComponentType<{ className?: string }>;
  value: number | undefined;
  label: string;
  to: string;
  isLoading?: boolean;
}) {
  return (
    <Link to={to} className="block group">
      <Card className="transition-colors hover:border-primary/40 hover:bg-muted/30">
        <CardContent className="flex h-20 items-center gap-3 px-5 py-0">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/8 text-primary flex-shrink-0 group-hover:bg-primary/15 transition-colors">
            <Icon className="h-5 w-5" />
          </div>
          <div className="flex-1 min-w-0">
            {isLoading || value === undefined ? (
              <Skeleton className="h-8 w-20 mb-1" />
            ) : (
              <p className="font-heading text-3xl font-bold text-foreground">
                {value.toLocaleString('tr-TR')}
              </p>
            )}
            <p className="text-sm text-muted-foreground leading-tight">{label}</p>
          </div>
          <ArrowRight className="h-4 w-4 text-muted-foreground flex-shrink-0 opacity-0 group-hover:opacity-100 transition-opacity" />
        </CardContent>
      </Card>
    </Link>
  );
}

// ─── Quick Link Card ─────────────────────────────────────────────

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
      className="flex w-[175px] items-center gap-3 rounded-xl border border-border/70 p-4 transition-colors hover:bg-muted/50 hover:border-primary/30 group"
    >
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary/8 text-primary group-hover:bg-primary/15 transition-colors">
        <Icon className="h-5 w-5" />
      </div>
      <p className="font-semibold text-foreground text-sm">{title}</p>
      <ArrowRight className="h-4 w-4 text-muted-foreground ml-auto shrink-0 opacity-0 group-hover:opacity-100 transition-opacity" />
    </Link>
  );
}

// ─── Customizable Quick Access Section ──────────────────────────

function QuickAccessSection() {
  const [visibleIds, setVisibleIds] = useState<QuickLinkId[]>(loadVisibleIds);
  const [editing, setEditing] = useState(false);

  function toggle(id: QuickLinkId) {
    const next = visibleIds.includes(id)
      ? visibleIds.filter(v => v !== id)
      : [...visibleIds, id];
    setVisibleIds(next);
    saveVisibleIds(next);
  }

  const visibleLinks = ALL_QUICK_LINKS.filter(l => visibleIds.includes(l.id));

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium text-muted-foreground">Hızlı Erişim</p>
        <Button
          variant="ghost"
          size="sm"
          className="h-7 gap-1.5 text-xs text-muted-foreground hover:text-foreground"
          onClick={() => setEditing(e => !e)}
        >
          {editing ? (
            <><Check className="h-3.5 w-3.5" /> Bitti</>
          ) : (
            <><Pencil className="h-3.5 w-3.5" /> Düzenle</>
          )}
        </Button>
      </div>

      {editing ? (
        /* Düzenleme modu: tüm öğeler toggle düğmesi olarak */
        <div className="rounded-xl border border-border/70 bg-muted/20 p-4 space-y-3">
          <p className="text-xs text-muted-foreground">
            Hızlı erişimde görmek istediklerini seç. Seçimler tarayıcında kaydedilir.
          </p>
          <div className="flex flex-wrap gap-2">
            {ALL_QUICK_LINKS.map(link => {
              const Icon = link.icon;
              const active = visibleIds.includes(link.id);
              return (
                <button
                  key={link.id}
                  onClick={() => toggle(link.id)}
                  className={`flex items-center gap-2 rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${
                    active
                      ? 'border-primary/40 bg-primary/8 text-primary'
                      : 'border-border/60 bg-background text-muted-foreground hover:border-border hover:text-foreground'
                  }`}
                >
                  <Icon className="h-4 w-4" />
                  {link.title}
                  {active ? (
                    <Check className="h-3.5 w-3.5 ml-0.5" />
                  ) : (
                    <EyeOff className="h-3.5 w-3.5 ml-0.5 opacity-50" />
                  )}
                </button>
              );
            })}
          </div>
        </div>
      ) : visibleLinks.length === 0 ? (
        <p className="text-sm text-muted-foreground py-2">
          Hiçbir öğe seçilmedi.{' '}
          <button
            onClick={() => setEditing(true)}
            className="text-primary underline-offset-2 hover:underline"
          >
            Düzenle
          </button>
        </p>
      ) : (
        <div className="flex flex-wrap gap-3">
          {visibleLinks.map(link => (
            <QuickLinkCard key={link.id} icon={link.icon} title={link.title} to={link.to} />
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Main Page ───────────────────────────────────────────────────

export default function SystemAdminDashboardPage() {
  const { user } = useAuth();

  const { data: usersData, isLoading: usersLoading } = useAdminUsers({ pageSize: 1, page: 1 });
  const { data: activeStudentsData, isLoading: studentsLoading } = useAdminStudents({ pageSize: 1, page: 1, status: 'Active' });
  const { data: instructors, isLoading: instructorsLoading } = useAdminInstructors({});
  const { data: terms = [], isLoading: termsLoading } = useAdminTerms();
  const { data: activeCalendar, isLoading: calendarLoading } = useActiveStudentCalendar();
  const { data: calendarTerms } = useAdminCalendarTerms();

  const activeTerm = terms.find(t => t.isActive);
  const activeTermNoCalendar = calendarTerms?.find(t => t.isActive && !t.isCancelled && !t.hasCalendar);
  const termLoading = termsLoading;

  const totalUsers = usersData?.totalCount;
  const activeStudents = activeStudentsData?.totalCount;
  const totalInstructors = instructors?.length;

  return (
    <div className="space-y-6">

      {/* ── Hero Banner ─────────────────────────────────────── */}
      <div className="bg-gradient-to-r from-primary via-primary-light to-accent rounded-2xl overflow-hidden px-6 py-5 sm:px-8 sm:py-6">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div>
            <h1 className="font-heading text-xl font-bold text-white">
              Hoş geldiniz, {user?.name ?? ''}
            </h1>
            <p className="text-sm text-white/80">
              Sistem Yöneticisi
              {activeTerm ? ` · ${activeTerm.name}` : ' · Aktif dönem yok'}
            </p>
          </div>
        </div>
      </div>

      {/* ── Missing Calendar Warning ─────────────────────── */}
      {activeTermNoCalendar && (
        <div className="rounded-lg border-2 border-amber-300 bg-amber-50 p-4 flex items-start gap-3">
          <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
          <div className="flex-1">
            <h3 className="font-semibold text-amber-900">Aktif dönemin akademik takvimi tanımlı değil</h3>
            <p className="text-sm text-amber-800 mt-1">
              <strong>{activeTermNoCalendar.termName}</strong> dönemi aktif ama kayıt/ekle-bırak/not girişi tarihleri tanımlanmamış. Öğrenciler kayıt yapamaz, notlar girilemez.
            </p>
            <Link to={ROUTES.ACADEMIC_CALENDAR_ADMIN} className="text-sm text-amber-900 underline mt-2 inline-block">
              Akademik Takvim Yönetimi'ne git →
            </Link>
          </div>
        </div>
      )}

      {/* ── Status Cards ────────────────────────────────────── */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <TermStatusCard activeTerm={activeTerm} isLoading={termLoading} />
        <RegistrationStatusCard activeTerm={activeCalendar} isLoading={calendarLoading} />
        <AddDropStatusCard activeTerm={activeCalendar} isLoading={calendarLoading} />
      </div>

      {/* ── Stats ────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          icon={Users}
          value={totalUsers}
          label="Toplam Kullanıcı"
          to={ROUTES.USER_MANAGEMENT}
          isLoading={usersLoading}
        />
        <StatCard
          icon={GraduationCap}
          value={activeStudents}
          label="Aktif Öğrenci"
          to={ROUTES.STUDENT_DIRECTORY}
          isLoading={studentsLoading}
        />
        <StatCard
          icon={BookUser}
          value={totalInstructors}
          label="Öğretim Üyesi"
          to={ROUTES.INSTRUCTOR_DIRECTORY}
          isLoading={instructorsLoading}
        />
        <StatCard
          icon={CalendarDays}
          value={terms.length}
          label="Dönem"
          to={ROUTES.TERM_MANAGEMENT}
          isLoading={termLoading}
        />
      </div>

      {/* ── Quick Access ─────────────────────────────────────── */}
      <QuickAccessSection />

      <DashboardAnnouncementWidget max={5} />

    </div>
  );
}
