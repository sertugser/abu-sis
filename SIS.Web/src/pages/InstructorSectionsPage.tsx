import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useInstructorSections, useSectionStudents } from '../hooks/useInstructorSections';
import { BookOpen, Users, ChevronDown, ChevronUp, Mail, ClipboardEdit } from 'lucide-react';

export default function InstructorSectionsPage() {
  const { data, isLoading } = useInstructorSections();
  const [expandedSection, setExpandedSection] = useState<number | null>(null);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        {[1, 2, 3].map(i => <Skeleton key={i} className="h-32 rounded-xl" />)}
      </div>
    );
  }

  if (!data || data.sections.length === 0) {
    return (
      <div className="text-center py-16 text-muted-foreground">
        Bu dönem size atanmış ders bulunmamaktadır.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">Derslerim</h1>
          <p className="text-muted-foreground mt-1">{data.term}</p>
        </div>
        <div className="flex gap-3">
          <Badge variant="outline" className="text-sm py-1 px-3">
            <BookOpen className="h-4 w-4 mr-1.5" />
            {data.totalSections} ders
          </Badge>
          <Badge variant="outline" className="text-sm py-1 px-3">
            <Users className="h-4 w-4 mr-1.5" />
            {data.totalStudents} öğrenci
          </Badge>
        </div>
      </div>

      <div className="space-y-4">
        {data.sections.map(section => (
          <SectionCard
            key={section.sectionId}
            section={section}
            isExpanded={expandedSection === section.sectionId}
            onToggle={() => setExpandedSection(prev => prev === section.sectionId ? null : section.sectionId)}
          />
        ))}
      </div>
    </div>
  );
}

interface SectionCardProps {
  section: {
    sectionId: number;
    courseCode: string;
    courseName: string;
    ects: number;
    theoryHours: number;
    labHours: number;
    capacity: number;
    enrolledCount: number;
    availableSeats: number;
    classroomName: string;
    schedule: string;
    isActive: boolean;
    activeStudents: number;
  };
  isExpanded: boolean;
  onToggle: () => void;
}

function SectionCard({ section, isExpanded, onToggle }: SectionCardProps) {
  const { data: studentData, isLoading: studentsLoading } = useSectionStudents(
    isExpanded ? section.sectionId : null
  );
  const navigate = useNavigate();

  const occupancyPercent = section.capacity > 0 ? Math.round((section.enrolledCount / section.capacity) * 100) : 0;

  return (
    <Card className="hover:shadow-md transition-all">
      <CardContent className="p-0">
        {/* Header */}
        <button
          onClick={onToggle}
          className="w-full p-5 flex items-center justify-between text-left hover:bg-muted/30 transition-colors rounded-t-lg"
        >
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-3">
              <span className="font-mono text-base font-bold text-primary">{section.courseCode}</span>
              <span className="text-base font-medium text-foreground truncate">{section.courseName}</span>
              <Badge variant={section.isActive ? 'success' : 'secondary'} className="shrink-0">
                {section.isActive ? 'Aktif' : 'Pasif'}
              </Badge>
            </div>
            <div className="flex items-center gap-4 mt-2 text-sm text-muted-foreground">
              <span>{section.ects} AKTS</span>
              <span>{section.theoryHours}T + {section.labHours}U</span>
              <span>{section.schedule || 'Program belirlenmedi'}</span>
              {section.classroomName && <span>{section.classroomName}</span>}
            </div>
          </div>
          <div className="flex items-center gap-4 ml-4">
            <Button
              size="sm"
              variant="default"
              onClick={(e) => { e.stopPropagation(); navigate(`/not-girisi/${section.sectionId}`); }}
              className="shrink-0"
            >
              <ClipboardEdit className="h-4 w-4 mr-1" /> Not Girişi
            </Button>
            {/* Occupancy bar */}
            <div className="text-right">
              <div className="flex items-center gap-2">
                <Users className="h-4 w-4 text-muted-foreground" />
                <span className="font-mono text-sm font-semibold">{section.activeStudents}/{section.capacity}</span>
              </div>
              <div className="w-24 h-1.5 bg-muted rounded-full mt-1">
                <div
                  className={`h-full rounded-full transition-all ${occupancyPercent > 90 ? 'bg-destructive' : occupancyPercent > 70 ? 'bg-yellow-500' : 'bg-primary'}`}
                  style={{ width: `${Math.min(occupancyPercent, 100)}%` }}
                />
              </div>
            </div>
            {isExpanded ? <ChevronUp className="h-5 w-5 text-muted-foreground" /> : <ChevronDown className="h-5 w-5 text-muted-foreground" />}
          </div>
        </button>

        {/* Student list */}
        {isExpanded && (
          <div className="border-t px-5 pb-5">
            {studentsLoading ? (
              <div className="py-4 space-y-2">
                {[1, 2, 3].map(i => <Skeleton key={i} className="h-10" />)}
              </div>
            ) : !studentData || studentData.students.length === 0 ? (
              <p className="py-4 text-sm text-muted-foreground">Bu sectionda kayıtlı öğrenci bulunmamaktadır.</p>
            ) : (
              <div className="mt-4">
                <div className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-3">
                  Kayıtlı Öğrenciler ({studentData.students.length})
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b text-left">
                        <th className="pb-2 font-medium text-muted-foreground">#</th>
                        <th className="pb-2 font-medium text-muted-foreground">Öğrenci No</th>
                        <th className="pb-2 font-medium text-muted-foreground">Ad Soyad</th>
                        <th className="pb-2 font-medium text-muted-foreground">E-posta</th>
                        <th className="pb-2 font-medium text-muted-foreground">Durum</th>
                        <th className="pb-2 font-medium text-muted-foreground">Not</th>
                      </tr>
                    </thead>
                    <tbody>
                      {studentData.students.map((student, index) => (
                        <tr key={student.studentId} className="border-b last:border-0 hover:bg-muted/30">
                          <td className="py-2.5 text-muted-foreground">{index + 1}</td>
                          <td className="py-2.5 font-mono text-xs">{student.studentNumber}</td>
                          <td className="py-2.5 font-medium">{student.firstName} {student.lastName}</td>
                          <td className="py-2.5">
                            <a href={`mailto:${student.email}`} className="text-primary hover:underline flex items-center gap-1">
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
                </div>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
