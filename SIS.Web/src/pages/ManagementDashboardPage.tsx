import { Link } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { ROUTES } from '@/lib/constants';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import { AlertCircle, BookOpenCheck, CalendarRange, ClipboardList, Network } from 'lucide-react';

const QUICK_LINKS = [
  {
    title: 'Planlama Merkezi',
    description: 'Tahminleme, şube ihtiyacı ve mevcut term schedule doğrulamasını birlikte görüntüleyin.',
    to: ROUTES.MANAGEMENT_PLANNING,
    icon: Network,
  },
  {
    title: 'Sınıf Listeleri',
    description: 'Bölümde açılan sectionları ve kayıtlı öğrencileri ders bazında inceleyin.',
    to: ROUTES.MANAGEMENT_CLASS_LISTS,
    icon: ClipboardList,
  },
  {
    title: 'Müfredat Görünümü',
    description: 'Program bazında aktif müfredat yapısını, semester dağılımını ve kredi yükünü gösterin.',
    to: ROUTES.MANAGEMENT_CURRICULUM,
    icon: BookOpenCheck,
  },
] as const;

export default function ManagementDashboardPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();

  if (contextQuery.isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-72" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {[1, 2, 3].map(index => <Skeleton key={index} className="h-28 rounded-xl" />)}
        </div>
        <Skeleton className="h-72 rounded-xl" />
      </div>
    );
  }

  if (contextQuery.isError || !contextQuery.data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Yonetim gorunumu acilamadi</AlertTitle>
        <AlertDescription>
          {getAcademicOperationsErrorMessage(contextQuery.error, 'Akademik operasyon verileri yuklenemedi.')}
        </AlertDescription>
      </Alert>
    );
  }

  const context = contextQuery.data;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">
          {context.roleLabel}
        </h1>
        <p className="text-muted-foreground mt-1">
          Planlama, sinif listeleri ve mufredat gorunumleri tek akista demo yapilabilecek sekilde hazir.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <StatCard
          title="Erisilebilir Bolum"
          value={String(context.accessibleDepartments.length)}
          description={context.canViewAllDepartments ? 'Tum bolumler goruntulenebilir' : 'Rol kapsaminda sinirli erisim'}
        />
        <StatCard
          title="Aktif Donem"
          value={activeTerm?.name ?? 'Tanimsiz'}
          description={activeTerm ? 'Planlama ozetleri bu doneme gore hesaplanir' : 'Aktif term kaydi bulunamadi'}
        />
        <StatCard
          title="Rol Kapsami"
          value={context.canViewAllDepartments ? 'Merkezi' : 'Bolum Bazli'}
          description={context.roleLabel}
        />
      </div>

      {context.notes.length > 0 && (
        <Alert>
          <CalendarRange className="h-4 w-4" />
          <AlertTitle>Sistem notlari</AlertTitle>
          <AlertDescription className="space-y-1">
            {context.notes.map(note => (
              <p key={note}>{note}</p>
            ))}
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Hizli Erisim</CardTitle>
          <CardDescription>En kritik demo ekranlari tek tikla hazir.</CardDescription>
        </CardHeader>
        <CardContent className="grid grid-cols-1 xl:grid-cols-3 gap-4">
          {QUICK_LINKS.map(link => {
            const Icon = link.icon;

            return (
              <Card key={link.to} className="border border-border/70 shadow-none">
                <CardContent className="p-5 space-y-4">
                  <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
                      <Icon className="h-5 w-5" />
                    </div>
                    <div>
                      <h3 className="font-semibold text-foreground">{link.title}</h3>
                      <p className="text-sm text-muted-foreground">{link.description}</p>
                    </div>
                  </div>
                  <Button asChild className="w-full">
                    <Link to={link.to}>Ekrani Ac</Link>
                  </Button>
                </CardContent>
              </Card>
            );
          })}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Erisilebilir Bolumler</CardTitle>
          <CardDescription>Operasyon sayfalarinda kullanabileceginiz kapsam.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          {context.accessibleDepartments.length > 0 ? (
            context.accessibleDepartments.map(department => (
              <Badge key={department.departmentId} variant="outline" className="py-1.5 px-3">
                {department.departmentCode} - {department.departmentName}
              </Badge>
            ))
          ) : (
            <p className="text-sm text-muted-foreground">Bu rol icin erisilebilir bolum bulunamadi.</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function StatCard({ title, value, description }: { title: string; value: string; description: string }) {
  return (
    <Card className="border-l-4 border-l-primary">
      <CardContent className="p-5">
        <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">{title}</p>
        <p className="mt-2 text-2xl font-heading font-bold text-foreground">{value}</p>
        <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      </CardContent>
    </Card>
  );
}
