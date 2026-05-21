import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  useCreateAcademicCvActivity,
  useInstructorAcademicCv,
  useInstructorProfile,
  useUpdateAcademicCvActivity,
  useUpdateInstructorProfile,
} from '../hooks/useInstructorProfile';
import { useInstructorSections } from '../hooks/useInstructorSections';
import type { AcademicCvActivity, InstructorSection } from '../api/instructor.api';
import { ROUTES } from '../lib/constants';
import { Clock, AlertCircle, BookOpen, ChevronRight, HelpCircle, PencilLine, FilePenLine } from 'lucide-react';
import DashboardAnnouncementWidget from '../components/announcements/DashboardAnnouncementWidget';
import { useAdminDepartments } from '../hooks/useAdminLookup';
import { getErrorMessage } from '../lib/errorMessage';

const TURKISH_DAYS: Record<string, number> = {
  'pazartesi': 1, 'sali': 2, 'salı': 2, 'carsamba': 3, 'çarşamba': 3,
  'persembe': 4, 'perşembe': 4, 'cuma': 5, 'cumartesi': 6, 'pazar': 0,
};

function getTodaysSections(sections: InstructorSection[]) {
  const todayJs = new Date().getDay(); // 0=Sun, 1=Mon...
  return sections.filter(s => {
    if (!s.schedule) return false;
    return s.schedule.split(',').some(slot => {
      const dayName = slot.trim().split(' ')[0]?.toLowerCase();
      return TURKISH_DAYS[dayName] === todayJs;
    });
  });
}

function getTodayScheduleTime(schedule: string): string {
  const todayJs = new Date().getDay();
  const slots = schedule.split(',');
  for (const slot of slots) {
    const parts = slot.trim().split(' ');
    const dayName = parts[0]?.toLowerCase();
    if (TURKISH_DAYS[dayName] === todayJs) {
      return parts[1] || '';
    }
  }
  return '';
}

export default function InstructorDashboardPage() {
  const navigate = useNavigate();
  const { data: profile, isLoading: profileLoading } = useInstructorProfile();
  const { data: academicCvData, isLoading: academicCvLoading } = useInstructorAcademicCv();
  const updateProfileMutation = useUpdateInstructorProfile();
  const createAcademicCvMutation = useCreateAcademicCvActivity();
  const updateAcademicCvMutation = useUpdateAcademicCvActivity();
  const { data: sectionsData, isLoading: sectionsLoading } = useInstructorSections();
  const { data: departments = [] } = useAdminDepartments();
  const [profileDialogOpen, setProfileDialogOpen] = useState(false);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileSuccess, setProfileSuccess] = useState<string | null>(null);
  const [cvDialogOpen, setCvDialogOpen] = useState(false);
  const [cvError, setCvError] = useState<string | null>(null);
  const [cvEditingActivity, setCvEditingActivity] = useState<AcademicCvActivity | null>(null);
  const [profileForm, setProfileForm] = useState({
    email: '',
    departmentId: '',
    officeHours: '',
    advisorOptOutRequested: false,
  });
  const [cvForm, setCvForm] = useState({
    activityYear: '',
    title: '',
    description: '',
  });

  useEffect(() => {
    if (!profileSuccess) return;
    const timer = setTimeout(() => setProfileSuccess(null), 3500);
    return () => clearTimeout(timer);
  }, [profileSuccess]);

  if (profileLoading || sectionsLoading || academicCvLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {[1, 2].map(i => <Skeleton key={i} className="h-32 rounded-xl" />)}
        </div>
        <Skeleton className="h-48 rounded-xl" />
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

  const sections = sectionsData?.sections ?? [];
  const cvActivities = academicCvData?.activities ?? [];
  const todaySections = getTodaysSections(sections);
  const awaitingGrade = sections.filter(s => !s.isFinalized);
  const showDashboardStatistics = !profile.simplifyDashboardStatistics;

  function openProfileDialog() {
    if (!profile) return;
    setProfileError(null);
    setProfileForm({
      email: profile.email,
      departmentId: String(profile.departmentId),
      officeHours: profile.officeHours ?? '',
      advisorOptOutRequested: profile.advisorOptOutRequested,
    });
    setProfileDialogOpen(true);
  }

  async function handleProfileSave() {
    setProfileError(null);

    try {
      await updateProfileMutation.mutateAsync({
        email: profileForm.email.trim(),
        departmentId: Number(profileForm.departmentId),
        officeHours: profileForm.officeHours,
        advisorOptOutRequested: profileForm.advisorOptOutRequested,
      });
      setProfileDialogOpen(false);
      setProfileSuccess('Profil bilgileri güncellendi.');
    } catch (error) {
      setProfileError(getErrorMessage(error, 'Profil bilgileri güncellenemedi.'));
    }
  }

  function openCvDialog(activity?: AcademicCvActivity) {
    if (!profile) return;
    setCvError(null);
    setCvEditingActivity(activity ?? null);
    setCvForm({
      activityYear: String(activity?.activityYear ?? academicCvData?.activeYear ?? profile.academicCvActiveYear),
      title: activity?.title ?? '',
      description: activity?.description ?? '',
    });
    setCvDialogOpen(true);
  }

  async function handleCvSave() {
    setCvError(null);

    try {
      const dto = {
        activityYear: Number(cvForm.activityYear),
        title: cvForm.title.trim(),
        description: cvForm.description.trim(),
      };

      if (cvEditingActivity) {
        await updateAcademicCvMutation.mutateAsync({ id: cvEditingActivity.id, dto });
      } else {
        await createAcademicCvMutation.mutateAsync(dto);
      }

      setCvDialogOpen(false);
      setProfileSuccess(cvEditingActivity ? 'CV faaliyeti güncellendi.' : 'CV faaliyeti oluşturuldu.');
    } catch (error) {
      setCvError(getErrorMessage(error, 'CV faaliyeti kaydedilemedi.'));
    }
  }

  return (
    <div className="space-y-6">
      {/* Welcome */}
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">
            Hoş Geldiniz, <span className="text-primary">{profile.title} {profile.firstName} {profile.lastName}</span>
          </h1>
          <p className="text-muted-foreground mt-1 text-[15px]">
            {profile.departmentName} — {profile.facultyName}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="outline" size="sm" className="gap-2 self-start" onClick={openProfileDialog}>
            <PencilLine className="h-4 w-4" />
            Profili Düzenle
          </Button>
          {profile.faqUrl && (
            <Button asChild variant="outline" size="sm" className="gap-2 self-start">
              <a href={profile.faqUrl} target="_blank" rel="noopener noreferrer">
                <HelpCircle className="h-4 w-4" />
                SSS
              </a>
            </Button>
          )}
        </div>
      </div>

      {profileSuccess && (
        <div className="rounded-xl border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
          {profileSuccess}
        </div>
      )}

      <Card>
        <CardContent className="grid gap-4 p-5 md:grid-cols-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Ofis Saatleri</p>
            <p className="mt-1 text-sm text-foreground">{profile.officeHours || 'Tanımlanmadı'}</p>
            {profile.requireOfficeHoursOnProfileUpdate && (
              <p className="mt-1 text-xs text-amber-600">Bu alan sistem ayarına göre zorunlu.</p>
            )}
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">E-Posta</p>
            <p className="mt-1 text-sm text-foreground">{profile.email}</p>
            {profile.primaryEmailImmutable && (
              <p className="mt-1 text-xs text-muted-foreground">Birincil e-posta değişikliği sistem ayarıyla kilitli.</p>
            )}
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Danışmanlık Tercihi</p>
            <p className="mt-1 text-sm text-foreground">
              {profile.advisorOptOutRequested ? 'Danışmanlık görevi istemiyor' : 'Danışmanlık görevi alabilir'}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              Bölüm güncelleme son tarihi: {profile.profileOrgUnitUpdateDeadline || 'Tanımlı değil'}
            </p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-5">
          <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
            <div>
              <h3 className="font-heading text-[15px] font-semibold text-foreground">Akademik CV Faaliyetleri</h3>
              <p className="mt-1 text-sm text-muted-foreground">
                Aktif yıl: {academicCvData?.activeYear ?? profile.academicCvActiveYear} •
                Otomatik onay: {(academicCvData?.autoApprove ?? profile.academicCvAutoApprove) ? ' Açık' : ' Kapalı'} •
                Onaylı kayıt düzenleme: {(academicCvData?.approvedActivityEditable ?? profile.academicCvApprovedActivityEditable) ? ' Açık' : ' Kapalı'}
              </p>
            </div>
            <Button variant="outline" size="sm" className="gap-2 self-start" onClick={() => openCvDialog()}>
              <FilePenLine className="h-4 w-4" />
              Faaliyet Ekle
            </Button>
          </div>

          <div className="mt-4 space-y-2">
            {cvActivities.length === 0 ? (
              <div className="rounded-lg border border-dashed border-border px-4 py-4 text-sm text-muted-foreground">
                Henüz CV faaliyet kaydı yok. Sistem ayarına göre yalnızca aktif yıl için giriş yapılabilir.
              </div>
            ) : (
              cvActivities.slice(0, 4).map(activity => (
                <div key={activity.id} className="flex flex-col gap-3 rounded-lg border border-border px-4 py-3 md:flex-row md:items-start md:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-sm font-semibold text-foreground">{activity.title}</p>
                      <Badge variant={activity.status === 'Approved' ? 'success' : 'secondary'}>
                        {activity.status === 'Approved' ? 'Onaylandı' : 'Onay Bekliyor'}
                      </Badge>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Yıl: {activity.activityYear}
                      {activity.approvedAt ? ` • Onay: ${new Date(activity.approvedAt).toLocaleDateString('tr-TR')}` : ''}
                    </p>
                    {activity.description && (
                      <p className="mt-2 text-sm text-muted-foreground">{activity.description}</p>
                    )}
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => openCvDialog(activity)}
                    disabled={!activity.canEdit}
                    className="self-start"
                  >
                    Düzenle
                  </Button>
                </div>
              ))
            )}
          </div>
        </CardContent>
      </Card>

      {/* Action cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Today's classes */}
        <Card className="border-l-4 border-l-primary hover:shadow-md transition-all">
          <CardContent className="p-5">
            <div className="flex items-center gap-2 mb-3">
              <Clock className="h-5 w-5 text-primary" />
              <h3 className="font-heading text-[15px] font-semibold text-foreground">Bugünkü Dersler</h3>
            </div>
            {todaySections.length === 0 ? (
              <p className="text-muted-foreground text-sm">Bugün dersiniz bulunmuyor.</p>
            ) : (
              <div className="space-y-2">
                {todaySections.map((section: InstructorSection) => (
                  <div key={section.sectionId} className="flex items-center justify-between p-2.5 rounded-md bg-muted/50">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="font-mono text-sm font-semibold text-primary">{section.courseCode}</span>
                        <span className="text-sm text-foreground truncate">{section.courseName}</span>
                      </div>
                      <p className="text-xs text-muted-foreground mt-0.5">
                        {getTodayScheduleTime(section.schedule)}
                        {section.classroomName && ` • ${section.classroomName}`}
                      </p>
                    </div>
                    {showDashboardStatistics && (
                      <Badge variant="outline" className="font-mono shrink-0 ml-2">
                        {section.activeStudents}/{section.capacity}
                      </Badge>
                    )}
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Awaiting grade entry */}
        <Card
          className="border-l-4 border-l-amber-500 hover:shadow-md transition-all cursor-pointer"
          onClick={() => navigate(ROUTES.INSTRUCTOR_SECTIONS)}
        >
          <CardContent className="p-5">
            <div className="flex items-center gap-2 mb-3">
              <AlertCircle className="h-5 w-5 text-amber-500" />
              <h3 className="font-heading text-[15px] font-semibold text-foreground">Not Girişi Bekleyen</h3>
            </div>
            {awaitingGrade.length === 0 ? (
              <p className="text-muted-foreground text-sm">Tüm derslerinizin notları kesinleştirilmiş.</p>
            ) : (
              <div className="space-y-2">
                {awaitingGrade.slice(0, 4).map((section: InstructorSection) => (
                  <div key={section.sectionId} className="flex items-center justify-between p-2.5 rounded-md bg-amber-50 dark:bg-amber-500/5">
                    <div className="flex items-center gap-2 min-w-0">
                      <span className="font-mono text-sm font-semibold text-amber-700 dark:text-amber-400">{section.courseCode}</span>
                      <span className="text-sm text-foreground truncate">{section.courseName}</span>
                    </div>
                    {showDashboardStatistics && (
                      <Badge variant="outline" className="font-mono shrink-0 ml-2 border-amber-300 text-amber-700 dark:text-amber-400">
                        {section.activeStudents} öğrenci
                      </Badge>
                    )}
                  </div>
                ))}
                {showDashboardStatistics && awaitingGrade.length > 4 && (
                  <p className="text-xs text-muted-foreground">+{awaitingGrade.length - 4} ders daha</p>
                )}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <DashboardAnnouncementWidget max={5} />

      {/* Course list */}
      {sections.length > 0 && (
        <Card>
          <CardContent className="p-5">
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-heading text-[15px] font-semibold text-foreground">
                Bu Dönem Derslerim — {sectionsData?.term}
              </h3>
              <Button variant="ghost" size="sm" onClick={() => navigate(ROUTES.INSTRUCTOR_SECTIONS)} className="text-xs gap-1">
                Tümünü Gör <ChevronRight className="h-3.5 w-3.5" />
              </Button>
            </div>
            <div className="space-y-2">
              {sections.map(section => (
                <div
                  key={section.sectionId}
                  className="flex items-center justify-between p-3 rounded-lg bg-muted/50 hover:bg-muted transition-colors cursor-pointer"
                  onClick={() => navigate(`${ROUTES.GRADE_ENTRY}/${section.sectionId}`)}
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10 shrink-0">
                      <BookOpen className="h-4 w-4 text-primary" />
                    </div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="font-mono text-sm font-semibold text-primary">{section.courseCode}</span>
                        <span className="text-sm text-foreground truncate">{section.courseName}</span>
                      </div>
                      <p className="text-xs text-muted-foreground mt-0.5">
                        {section.schedule || 'Program belirlenmedi'}
                        {section.classroomName && ` • ${section.classroomName}`}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 ml-4 shrink-0">
                    {showDashboardStatistics && (
                      <Badge variant="outline" className="font-mono">
                        {section.activeStudents}/{section.capacity}
                      </Badge>
                    )}
                    {section.isFinalized ? (
                      <Badge variant="success">Kesinleşti</Badge>
                    ) : (
                      <Badge variant="secondary">Bekliyor</Badge>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      <Dialog open={profileDialogOpen} onOpenChange={setProfileDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Profil Bilgilerini Güncelle</DialogTitle>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="instructor-email">E-posta</Label>
              <Input
                id="instructor-email"
                value={profileForm.email}
                onChange={event => setProfileForm(current => ({ ...current, email: event.target.value }))}
                disabled={profile.primaryEmailImmutable || updateProfileMutation.isPending}
              />
              {profile.primaryEmailImmutable && (
                <p className="text-xs text-muted-foreground">Bu alan sistem ayarı nedeniyle düzenlenemez.</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="instructor-department">Bölüm</Label>
              <select
                id="instructor-department"
                value={profileForm.departmentId}
                onChange={event => setProfileForm(current => ({ ...current, departmentId: event.target.value }))}
                disabled={!profile.canUpdateDepartment || updateProfileMutation.isPending}
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              >
                {departments.map(department => (
                  <option key={department.id} value={department.id}>{department.name}</option>
                ))}
              </select>
              <p className="text-xs text-muted-foreground">
                Güncelleme son tarihi: {profile.profileOrgUnitUpdateDeadline || 'Tanımlı değil'}
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="instructor-office-hours">
                Ofis Saatleri
                {profile.requireOfficeHoursOnProfileUpdate ? ' *' : ''}
              </Label>
              <Textarea
                id="instructor-office-hours"
                value={profileForm.officeHours}
                onChange={event => setProfileForm(current => ({ ...current, officeHours: event.target.value }))}
                placeholder="Örn. Salı 13:30-15:30, Perşembe 10:00-11:00"
                disabled={updateProfileMutation.isPending}
              />
            </div>

            <div className="flex items-center justify-between rounded-lg border border-border px-3 py-3">
              <div>
                <p className="text-sm font-medium text-foreground">Danışmanlık yapmak istemiyorum</p>
                <p className="text-xs text-muted-foreground">
                  Açık olduğunda yeni danışman atamalarında listeden çıkarılırsınız.
                </p>
              </div>
              <Switch
                checked={profileForm.advisorOptOutRequested}
                onCheckedChange={checked => setProfileForm(current => ({ ...current, advisorOptOutRequested: checked }))}
                disabled={!profile.enableAdvisorOptOutOptions || updateProfileMutation.isPending}
              />
            </div>

            {!profile.enableAdvisorOptOutOptions && (
              <p className="text-xs text-muted-foreground">Danışmanlık tercihi değiştirme seçeneği sistem ayarında kapalı.</p>
            )}

            {profileError && (
              <div className="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {profileError}
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setProfileDialogOpen(false)} disabled={updateProfileMutation.isPending}>
              Vazgeç
            </Button>
            <Button onClick={handleProfileSave} disabled={updateProfileMutation.isPending}>
              Kaydet
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cvDialogOpen} onOpenChange={setCvDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{cvEditingActivity ? 'CV Faaliyetini Düzenle' : 'CV Faaliyeti Ekle'}</DialogTitle>
          </DialogHeader>

          <div className="space-y-4">
            <div className="rounded-lg border border-border bg-muted/30 px-3 py-3 text-sm text-muted-foreground">
              Bu kayıt yalnızca aktif yıl için girilebilir: <span className="font-semibold text-foreground">{academicCvData?.activeYear ?? profile.academicCvActiveYear}</span>
              {(academicCvData?.autoApprove ?? profile.academicCvAutoApprove) && (
                <span className="mt-1 block text-green-700">Yeni kayıtlar otomatik olarak onaylanır.</span>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="cv-year">Faaliyet Yılı</Label>
              <Input
                id="cv-year"
                value={cvForm.activityYear}
                onChange={event => setCvForm(current => ({ ...current, activityYear: event.target.value }))}
                disabled={createAcademicCvMutation.isPending || updateAcademicCvMutation.isPending}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="cv-title">Başlık</Label>
              <Input
                id="cv-title"
                value={cvForm.title}
                onChange={event => setCvForm(current => ({ ...current, title: event.target.value }))}
                disabled={createAcademicCvMutation.isPending || updateAcademicCvMutation.isPending}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="cv-description">Açıklama</Label>
              <Textarea
                id="cv-description"
                value={cvForm.description}
                onChange={event => setCvForm(current => ({ ...current, description: event.target.value }))}
                disabled={createAcademicCvMutation.isPending || updateAcademicCvMutation.isPending}
              />
            </div>

            {cvError && (
              <div className="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {cvError}
              </div>
            )}

            {cvEditingActivity && !cvEditingActivity.canEdit && (
              <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-700">
                Bu kayıt onaylı olduğu için sistem ayarına göre düzenlenemez.
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCvDialogOpen(false)} disabled={createAcademicCvMutation.isPending || updateAcademicCvMutation.isPending}>
              Vazgeç
            </Button>
            <Button onClick={handleCvSave} disabled={createAcademicCvMutation.isPending || updateAcademicCvMutation.isPending || (cvEditingActivity ? !cvEditingActivity.canEdit : false)}>
              Kaydet
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
