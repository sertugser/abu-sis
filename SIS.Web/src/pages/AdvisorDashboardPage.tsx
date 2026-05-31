import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Users, ClipboardList, AlertTriangle, CheckCircle2, ChevronRight, BookOpen } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useAdvisorDashboardStats } from '../hooks/useAdvisor';
import DashboardAnnouncementWidget from '../components/announcements/DashboardAnnouncementWidget';

interface StatCardProps {
  label: string;
  value: number | string;
  hint?: string;
  icon: React.ReactNode;
  accent: string;
  onClick?: () => void;
}

function StatCard({ label, value, hint, icon, accent, onClick }: StatCardProps) {
  return (
    <Card
      className={`border-l-4 ${accent} transition-all ${onClick ? 'cursor-pointer hover:shadow-md' : ''}`}
      onClick={onClick}
    >
      <CardContent className="flex items-start justify-between gap-3 p-5">
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{label}</p>
          <p className="mt-1 font-heading text-3xl font-extrabold text-foreground">{value}</p>
          {hint && <p className="mt-1 text-xs text-muted-foreground">{hint}</p>}
        </div>
        <div className="shrink-0 rounded-xl bg-muted/60 p-2.5">{icon}</div>
      </CardContent>
    </Card>
  );
}

export default function AdvisorDashboardPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { data: stats, isLoading } = useAdvisorDashboardStats();

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-72" />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {[1, 2, 3, 4].map(i => <Skeleton key={i} className="h-28 rounded-xl" />)}
        </div>
        <Skeleton className="h-40 rounded-xl" />
      </div>
    );
  }

  const s = stats ?? {
    adviseeCount: 0,
    pendingApprovalCount: 0,
    atRiskCount: 0,
    approvedThisTerm: 0,
    rejectedThisTerm: 0,
    retentionThreshold: 0,
  };

  return (
    <div className="space-y-6">
      {/* Welcome */}
      <div>
        <h1 className="font-heading text-2xl font-extrabold tracking-tight text-foreground">
          Hoş Geldiniz, <span className="text-primary">{user?.name ?? 'Danışman'}</span>
        </h1>
        <p className="mt-1 text-[15px] text-muted-foreground">
          Danışmanlık öğrencilerinizin durumunu ve onay bekleyen kayıtları buradan takip edin.
        </p>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          label="Danışmanlık Öğrencileri"
          value={s.adviseeCount}
          hint="Aktif atanmış öğrenci"
          accent="border-l-primary"
          icon={<Users className="h-5 w-5 text-primary" />}
          onClick={() => navigate('/ogrenci-rehberi')}
        />
        <StatCard
          label="Onay Bekleyen"
          value={s.pendingApprovalCount}
          hint={s.pendingApprovalCount > 0 ? 'İncelemek için tıklayın' : 'Bekleyen plan yok'}
          accent="border-l-amber-500"
          icon={<ClipboardList className="h-5 w-5 text-amber-500" />}
          onClick={() => navigate('/ders-kayit-onay')}
        />
        <StatCard
          label="Riskli Öğrenci"
          value={s.atRiskCount}
          hint={`GNO < ${s.retentionThreshold.toFixed(2)} (1. dönem sonrası)`}
          accent="border-l-destructive"
          icon={<AlertTriangle className="h-5 w-5 text-destructive" />}
        />
        <StatCard
          label="Bu Dönem Karara Bağlanan"
          value={`${s.approvedThisTerm} / ${s.rejectedThisTerm}`}
          hint="Onaylanan / Reddedilen"
          accent="border-l-green-500"
          icon={<CheckCircle2 className="h-5 w-5 text-green-600" />}
        />
      </div>

      {/* Quick actions */}
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Card
          className="cursor-pointer border-border transition-all hover:shadow-md"
          onClick={() => navigate('/ders-kayit-onay')}
        >
          <CardContent className="flex items-center justify-between gap-3 p-5">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                <ClipboardList className="h-5 w-5 text-primary" />
              </div>
              <div>
                <h3 className="font-heading text-[15px] font-semibold text-foreground">Ders Kayıt Düzenleme</h3>
                <p className="text-sm text-muted-foreground">Plan onayla/reddet, öğrenci adına ders ekle/çıkar</p>
              </div>
            </div>
            <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground" />
          </CardContent>
        </Card>

        <Card
          className="cursor-pointer border-border transition-all hover:shadow-md"
          onClick={() => navigate('/ogrenci-rehberi')}
        >
          <CardContent className="flex items-center justify-between gap-3 p-5">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                <BookOpen className="h-5 w-5 text-primary" />
              </div>
              <div>
                <h3 className="font-heading text-[15px] font-semibold text-foreground">Öğrenci Rehberi</h3>
                <p className="text-sm text-muted-foreground">Öğrenci bilgilerini ve akademik durumu görüntüleyin</p>
              </div>
            </div>
            <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground" />
          </CardContent>
        </Card>
      </div>

      <DashboardAnnouncementWidget max={5} />
    </div>
  );
}
