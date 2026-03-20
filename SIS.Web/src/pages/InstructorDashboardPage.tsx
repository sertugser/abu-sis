import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { useInstructorProfile } from '../hooks/useInstructorProfile';
import { useInstructorSections } from '../hooks/useInstructorSections';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { formatDate } from '../lib/utils';
import { BookOpen, Users, GraduationCap } from 'lucide-react';

export default function InstructorDashboardPage() {
  const { data: profile, isLoading: profileLoading } = useInstructorProfile();
  const { data: sectionsData, isLoading: sectionsLoading } = useInstructorSections();
  const { data: term } = useActiveTerm();

  if (profileLoading || sectionsLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {[1, 2, 3].map(i => <Skeleton key={i} className="h-28 rounded-xl" />)}
        </div>
      </div>
    );
  }

  if (!profile) {
    return (
      <div className="text-center py-16 text-muted-foreground">
        Öğretim üyesi bilgisi bulunamadı.
      </div>
    );
  }

  return (
    <div className="space-y-8">
      {/* Welcome */}
      <div>
        <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">
          Hoş Geldiniz, <span className="text-primary">{profile.title} {profile.firstName} {profile.lastName}</span>
        </h1>
        <p className="text-muted-foreground mt-1">
          {profile.departmentName} — {profile.facultyName}
        </p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 stagger-children">
        <Card className="border-l-4 border-l-primary hover:-translate-y-0.5 hover:shadow-md transition-all">
          <CardContent className="p-5">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                <BookOpen className="h-5 w-5 text-primary" />
              </div>
              <div>
                <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">Verilen Ders</p>
                <p className="text-2xl font-bold text-foreground font-heading">{sectionsData?.totalSections ?? 0}</p>
                <p className="text-xs text-muted-foreground">aktif sectionlar</p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="border-l-4 border-l-accent hover:-translate-y-0.5 hover:shadow-md transition-all">
          <CardContent className="p-5">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-accent/10">
                <Users className="h-5 w-5 text-accent" />
              </div>
              <div>
                <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">Toplam Öğrenci</p>
                <p className="text-2xl font-bold text-foreground font-heading">{sectionsData?.totalStudents ?? 0}</p>
                <p className="text-xs text-muted-foreground">kayıtlı öğrenci</p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="border-l-4 border-l-info hover:-translate-y-0.5 hover:shadow-md transition-all">
          <CardContent className="p-5">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-500/10">
                <GraduationCap className="h-5 w-5 text-blue-500" />
              </div>
              <div>
                <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">Unvan</p>
                <p className="text-lg font-bold text-foreground font-heading">{profile.title}</p>
                <p className="text-xs text-muted-foreground">{profile.email}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* My Sections quick view */}
      {sectionsData && sectionsData.sections.length > 0 && (
        <Card>
          <CardContent className="p-5">
            <h3 className="font-heading text-sm font-semibold text-foreground mb-4">
              Bu Dönem Verilen Dersler — {sectionsData.term}
            </h3>
            <div className="space-y-3">
              {sectionsData.sections.map(section => (
                <div key={section.sectionId} className="flex items-center justify-between p-3 rounded-lg bg-muted/50 hover:bg-muted transition-colors">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-sm font-semibold text-primary">{section.courseCode}</span>
                      <span className="text-sm text-foreground truncate">{section.courseName}</span>
                    </div>
                    <div className="flex items-center gap-3 mt-1 text-xs text-muted-foreground">
                      <span>{section.schedule || 'Program belirlenmedi'}</span>
                      {section.classroomName && <span>• {section.classroomName}</span>}
                    </div>
                  </div>
                  <div className="flex items-center gap-3 ml-4">
                    <Badge variant="outline" className="font-mono">
                      {section.activeStudents}/{section.capacity}
                    </Badge>
                    <Badge variant={section.isActive ? 'success' : 'secondary'}>
                      {section.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Active term */}
      {term && (
        <Card>
          <CardContent className="p-5">
            <h3 className="font-heading text-sm font-semibold text-foreground mb-4">Aktif Dönem Bilgisi</h3>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
              <div>
                <span className="text-muted-foreground">Dönem</span>
                <p className="font-medium mt-0.5">{term.name}</p>
              </div>
              <div>
                <span className="text-muted-foreground">Kayıt Tarihleri</span>
                <p className="font-medium mt-0.5">{formatDate(term.registrationStart)} — {formatDate(term.registrationEnd)}</p>
              </div>
              <div>
                <span className="text-muted-foreground">Ek/Bırak Tarihleri</span>
                <p className="font-medium mt-0.5">{formatDate(term.addDropStart)} — {formatDate(term.addDropEnd)}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
