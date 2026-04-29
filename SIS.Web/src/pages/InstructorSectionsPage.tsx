import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ROUTES } from '@/lib/constants';
import { useInstructorSections, useSectionStudents } from '../hooks/useInstructorSections';
import { BookOpen, Users, Mail, ClipboardEdit, Calendar, Search, ClipboardCheck } from 'lucide-react';

export default function InstructorSectionsPage() {
  const { data, isLoading } = useInstructorSections();
  const [searchTerm, setSearchTerm] = useState('');
  const [studentDialogSection, setStudentDialogSection] = useState<{
    sectionId: number;
    courseCode: string;
    courseName: string;
  } | null>(null);
  const navigate = useNavigate();

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-12 rounded-xl" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    );
  }

  if (!data || data.sections.length === 0) {
    return (
      <div className="text-center py-16 text-muted-foreground text-[15px]">
        Bu dönem size atanmış ders bulunmamaktadır.
      </div>
    );
  }

  const filteredSections = data.sections.filter(section => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.toLowerCase();
    return `${section.courseCode} ${section.courseName} ${section.classroomName} ${section.schedule}`.toLowerCase().includes(q);
  });

  return (
    <div className="flex flex-col gap-4 flex-1">
      {/* Search + stats in one row */}
      <div className="flex items-center gap-3">
        <div className="relative flex-1">
          <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
          <Input
            value={searchTerm}
            onChange={event => setSearchTerm(event.target.value)}
            placeholder="Ders kodu, ders adı, derslik veya programa göre ara..."
            className="pl-9 text-[15px]"
          />
        </div>
        <Badge variant="outline" className="text-sm py-1.5 px-3 shrink-0">
          <BookOpen className="h-4 w-4 mr-1.5" />
          {data.totalSections} ders
        </Badge>
        <Badge variant="outline" className="text-sm py-1.5 px-3 shrink-0">
          <Users className="h-4 w-4 mr-1.5" />
          {data.totalStudents} öğrenci
        </Badge>
      </div>

      {/* Table */}
      <Card className="flex-1">
        <CardContent className="p-0">
          {filteredSections.length === 0 ? (
            <div className="py-16 text-center text-muted-foreground text-[15px]">
              Filtreye uyan ders bulunamadı.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-[15px]">
                <thead>
                  <tr className="border-b bg-muted/30">
                    <th className="text-left py-3 px-4 font-medium text-muted-foreground">Ders Kodu</th>
                    <th className="text-left py-3 px-4 font-medium text-muted-foreground">Ders Adı</th>
                    <th className="text-left py-3 px-4 font-medium text-muted-foreground">Program</th>
                    <th className="text-center py-3 px-4 font-medium text-muted-foreground">Öğrenci</th>
                    <th className="text-center py-3 px-4 font-medium text-muted-foreground">Durum</th>
                    <th className="text-right py-3 px-4 font-medium text-muted-foreground">İşlemler</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredSections.map(section => {
                    const occupancyPercent = section.capacity > 0 ? Math.round((section.enrolledCount / section.capacity) * 100) : 0;

                    return (
                      <tr key={section.sectionId} className="border-b hover:bg-muted/30 transition-colors">
                        <td className="py-3.5 px-4">
                          <span className="font-mono font-semibold text-primary">{section.courseCode}</span>
                        </td>
                        <td className="py-3.5 px-4">
                          <div>
                            <span className="font-medium text-foreground">{section.courseName}</span>
                            <p className="text-xs text-muted-foreground mt-0.5">{section.ects} AKTS • {section.theoryHours} Teori + {section.labHours} Uygulama</p>
                          </div>
                        </td>
                        <td className="py-3.5 px-4">
                          <div className="text-sm text-muted-foreground">
                            <p>{section.schedule || 'Belirlenmedi'}</p>
                            {section.classroomName && <p className="text-xs mt-0.5">{section.classroomName}</p>}
                          </div>
                        </td>
                        <td className="py-3.5 px-4 text-center">
                          <div className="inline-flex flex-col items-center">
                            <span className="font-mono font-semibold">{section.activeStudents}/{section.capacity}</span>
                            <div className="w-16 h-1.5 bg-muted rounded-full mt-1">
                              <div
                                className={`h-full rounded-full transition-all ${occupancyPercent > 90 ? 'bg-destructive' : occupancyPercent > 70 ? 'bg-yellow-500' : 'bg-primary'}`}
                                style={{ width: `${Math.min(occupancyPercent, 100)}%` }}
                              />
                            </div>
                          </div>
                        </td>
                        <td className="py-3.5 px-4 text-center">
                          {section.isFinalized ? (
                            <Badge variant="success">Kesinleşti</Badge>
                          ) : (
                            <Badge variant="secondary">Bekliyor</Badge>
                          )}
                        </td>
                        <td className="py-3.5 px-4 text-right">
                          <div className="flex items-center justify-end gap-2">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => setStudentDialogSection({
                                sectionId: section.sectionId,
                                courseCode: section.courseCode,
                                courseName: section.courseName,
                              })}
                              className="text-xs gap-1.5"
                            >
                              <Users className="h-3.5 w-3.5" />
                              Öğrenciler
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => navigate(`${ROUTES.INSTRUCTOR_SCHEDULING}?sectionId=${section.sectionId}`)}
                              className="text-xs gap-1.5"
                            >
                              <Calendar className="h-3.5 w-3.5" />
                              Program
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => navigate(`${ROUTES.ATTENDANCE_ENTRY}?sectionId=${section.sectionId}`)}
                              className="text-xs gap-1.5"
                            >
                              <ClipboardCheck className="h-3.5 w-3.5" />
                              Yoklama
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => navigate(`${ROUTES.GRADE_ENTRY}/${section.sectionId}`)}
                              className="text-xs gap-1.5"
                            >
                              <ClipboardEdit className="h-3.5 w-3.5" />
                              Not Girişi
                            </Button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Student List Dialog */}
      {studentDialogSection && (
        <StudentListDialog
          sectionId={studentDialogSection.sectionId}
          courseCode={studentDialogSection.courseCode}
          courseName={studentDialogSection.courseName}
          onClose={() => setStudentDialogSection(null)}
        />
      )}
    </div>
  );
}

function StudentListDialog({
  sectionId,
  courseCode,
  courseName,
  onClose,
}: {
  sectionId: number;
  courseCode: string;
  courseName: string;
  onClose: () => void;
}) {
  const { data: studentData, isLoading } = useSectionStudents(sectionId);
  const [search, setSearch] = useState('');

  const students = studentData?.students ?? [];
  const filtered = students.filter(s => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return `${s.studentNumber} ${s.firstName} ${s.lastName} ${s.email}`.toLowerCase().includes(q);
  });

  return (
    <Dialog open onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent className="max-w-3xl max-h-[85vh] flex flex-col">
        <DialogHeader>
          <DialogTitle className="text-lg">
            <span className="font-mono text-primary">{courseCode}</span> — {courseName}
          </DialogTitle>
          <p className="text-sm text-muted-foreground">{students.length} kayıtlı öğrenci</p>
        </DialogHeader>

        {isLoading ? (
          <div className="space-y-2 py-4">
            {[1, 2, 3, 4, 5].map(i => <Skeleton key={i} className="h-10" />)}
          </div>
        ) : students.length === 0 ? (
          <p className="py-8 text-center text-muted-foreground">Bu derste kayıtlı öğrenci bulunmamaktadır.</p>
        ) : (
          <>
            {students.length > 10 && (
              <div className="relative">
                <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
                <Input
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  placeholder="Öğrenci ara..."
                  className="pl-9 text-sm"
                />
              </div>
            )}

            <div className="overflow-y-auto flex-1 -mx-6 px-6" style={{ maxHeight: '60vh' }}>
              <table className="w-full text-sm">
                <thead className="sticky top-0 bg-background">
                  <tr className="border-b text-left">
                    <th className="pb-2 pt-1 font-medium text-muted-foreground w-10">#</th>
                    <th className="pb-2 pt-1 font-medium text-muted-foreground">Öğrenci No</th>
                    <th className="pb-2 pt-1 font-medium text-muted-foreground">Ad Soyad</th>
                    <th className="pb-2 pt-1 font-medium text-muted-foreground">E-posta</th>
                    <th className="pb-2 pt-1 font-medium text-muted-foreground">Durum</th>
                    <th className="pb-2 pt-1 font-medium text-muted-foreground">Not</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((student, index) => (
                    <tr key={student.studentId} className="border-b last:border-0 hover:bg-muted/30">
                      <td className="py-2.5 text-muted-foreground">{index + 1}</td>
                      <td className="py-2.5 font-mono text-xs">{student.studentNumber}</td>
                      <td className="py-2.5 font-medium">{student.firstName} {student.lastName}</td>
                      <td className="py-2.5">
                        <a href={`mailto:${student.email}`} className="text-primary hover:underline inline-flex items-center gap-1">
                          <Mail className="h-3 w-3" />
                          {student.email}
                        </a>
                      </td>
                      <td className="py-2.5">
                        <Badge variant={student.status === 'Active' ? 'success' : 'secondary'} className="text-xs">
                          {student.status === 'Active' ? 'Aktif' : student.status}
                        </Badge>
                      </td>
                      <td className="py-2.5 font-mono">{student.grade ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {filtered.length === 0 && search.trim() && (
                <p className="py-6 text-center text-muted-foreground text-sm">Aramayla eşleşen öğrenci bulunamadı.</p>
              )}
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
