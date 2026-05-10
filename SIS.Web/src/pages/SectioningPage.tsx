import { Fragment, useEffect, useMemo, useState } from 'react';
import { AlertCircle, ChevronDown, Eye, Layers, Search, Users } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import DepartmentOperationsLayout, { resolveInitialDepartmentId } from '@/components/academic-operations/DepartmentOperationsLayout';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import { useAcademicOperationsContext, useDepartmentClassLists } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';

export default function SectioningPage() {
  const contextQuery = useAcademicOperationsContext();
  const activeTermQuery = useActiveTerm();
  const activeTerm = activeTermQuery.data;
  const [departmentId, setDepartmentId] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [expandedSectionId, setExpandedSectionId] = useState<number | null>(null);

  useEffect(() => {
    if (departmentId === null && contextQuery.data) {
      setDepartmentId(resolveInitialDepartmentId(contextQuery.data));
    }
  }, [contextQuery.data, departmentId]);

  const termId = activeTerm?.id;
  const classListsQuery = useDepartmentClassLists(departmentId, termId, !!departmentId && !!termId, search);
  const sections = classListsQuery.data?.sections ?? [];

  useEffect(() => {
    setExpandedSectionId(null);
  }, [departmentId, termId, search]);

  const stats = useMemo(() => {
    const totalCapacity = sections.reduce((sum, section) => sum + section.capacity, 0);
    const totalActiveStudents = sections.reduce((sum, section) => sum + section.activeStudentCount, 0);
    const fullSections = sections.filter(section => section.capacity > 0 && section.activeStudentCount >= section.capacity).length;
    const emptySections = sections.filter(section => section.activeStudentCount === 0).length;
    const crossListed = sections.filter(section => section.crossListGroupId !== null).length;

    return {
      totalCapacity,
      totalActiveStudents,
      fullSections,
      emptySections,
      crossListed,
      occupancy: totalCapacity > 0 ? Math.round((totalActiveStudents / totalCapacity) * 100) : 0,
    };
  }, [sections]);

  return (
    <DepartmentOperationsLayout
      icon={Layers}
      title="Şubelendirme İşlemleri"
      description="Bölümün aktif dönem şubelerini, doluluk durumunu, cross-list ilişkilerini ve öğrenci dağılımını izleyin."
      context={contextQuery.data ?? null}
      departmentId={departmentId}
      onDepartmentChange={setDepartmentId}
      termName={activeTerm?.name}
      isContextLoading={contextQuery.isLoading}
      contextError={contextQuery.isError ? getAcademicOperationsErrorMessage(contextQuery.error, 'Kapsam bilgileri alınamadı.') : null}
      hideHeader
      actions={
        <div className="relative">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={event => setSearch(event.target.value)}
            placeholder="Ders, şube veya derslik ara..."
            className="h-9 w-[260px] pl-8"
          />
        </div>
      }
    >
      <div className="space-y-4">
        {classListsQuery.isError && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {getAcademicOperationsErrorMessage(classListsQuery.error, 'Şube listesi yüklenemedi.')}
            </AlertDescription>
          </Alert>
        )}

        {!activeTermQuery.isLoading && !activeTerm && (
          <Alert variant="warning">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>Aktif akademik dönem bulunamadığı için şube dağılımı listelenemiyor.</AlertDescription>
          </Alert>
        )}

        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
          <MetricCard title="Şube" value={classListsQuery.isLoading ? null : sections.length} />
          <MetricCard title="Aktif Öğrenci" value={classListsQuery.isLoading ? null : stats.totalActiveStudents} />
          <MetricCard title="Doluluk" value={classListsQuery.isLoading ? null : `%${stats.occupancy}`} progress={stats.occupancy} />
          <MetricCard title="Kapasitesi Dolu" value={classListsQuery.isLoading ? null : stats.fullSections} tone={stats.fullSections > 0 ? 'warning' : 'default'} />
          <MetricCard title="Cross-list" value={classListsQuery.isLoading ? null : stats.crossListed} />
        </div>

        {!activeTermQuery.isLoading && !activeTerm ? (
          <Card>
            <CardContent className="flex flex-col items-center justify-center py-16 text-center">
              <Users className="mb-3 h-9 w-9 text-muted-foreground/45" />
              <p className="text-sm font-medium">Aktif dönem seçimi bekleniyor.</p>
              <p className="mt-1 text-sm text-muted-foreground">Şubelendirme görünümü dönem bazlı çalışır.</p>
            </CardContent>
          </Card>
        ) : classListsQuery.isLoading ? (
          <Skeleton className="h-[420px] rounded-lg" />
        ) : sections.length === 0 ? (
          <Card>
            <CardContent className="flex flex-col items-center justify-center py-16 text-center">
              <Users className="mb-3 h-9 w-9 text-muted-foreground/45" />
              <p className="text-sm font-medium">Bu filtreyle şube bulunamadı.</p>
              <p className="mt-1 text-sm text-muted-foreground">Dönem, bölüm veya arama filtresini değiştirerek tekrar deneyin.</p>
            </CardContent>
          </Card>
        ) : (
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Şube Dağılımı</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Ders</TableHead>
                    <TableHead className="w-[90px]">Şube</TableHead>
                    <TableHead className="w-[160px]">Doluluk</TableHead>
                    <TableHead>Öğretim Elemanı</TableHead>
                    <TableHead>Derslik / Saat</TableHead>
                    <TableHead className="w-[130px]">Durum</TableHead>
                    <TableHead className="w-[120px] text-right">Liste</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sections.map(section => {
                    const fill = section.capacity > 0 ? Math.round((section.activeStudentCount / section.capacity) * 100) : 0;
                    const isExpanded = expandedSectionId === section.sectionId;
                    return (
                      <Fragment key={section.sectionId}>
                        <TableRow
                          className="cursor-pointer"
                          onClick={() => setExpandedSectionId(current => (current === section.sectionId ? null : section.sectionId))}
                        >
                          <TableCell>
                            <div className="font-medium">{section.courseCode}</div>
                            <div className="text-xs text-muted-foreground">{section.courseName}</div>
                            {section.crossListPeerCodes.length > 0 && (
                              <div className="mt-1 text-xs text-muted-foreground">
                                Cross-list: {section.crossListPeerCodes.join(', ')}
                              </div>
                            )}
                          </TableCell>
                          <TableCell>#{section.sectionNumber}</TableCell>
                          <TableCell>
                            <div className="flex items-center justify-between text-xs">
                              <span>{section.activeStudentCount}/{section.capacity}</span>
                              <span className="text-muted-foreground">%{fill}</span>
                            </div>
                            <Progress value={Math.min(fill, 100)} className="mt-1 h-2" />
                            {(section.internalQuota !== null || section.externalQuota !== null) && (
                              <div className="mt-1 text-[11px] text-muted-foreground">
                                İç: {section.internalQuota ?? '-'} / Dış: {section.externalQuota ?? '-'}
                              </div>
                            )}
                          </TableCell>
                          <TableCell>{section.instructorName || <span className="text-muted-foreground">Atanmamış</span>}</TableCell>
                          <TableCell>
                            <div>{section.classroomName || <span className="text-muted-foreground">Derslik yok</span>}</div>
                            <div className="text-xs text-muted-foreground">{section.scheduleSummary || 'Saat yok'}</div>
                          </TableCell>
                          <TableCell>
                            <Badge variant={section.isActive ? 'success' : 'secondary'}>
                              {section.isActive ? 'Aktif' : 'Taslak'}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-right">
                            <Button
                              type="button"
                              variant={isExpanded ? 'secondary' : 'outline'}
                              size="sm"
                              onClick={event => {
                                event.stopPropagation();
                                setExpandedSectionId(current => (current === section.sectionId ? null : section.sectionId));
                              }}
                            >
                              {isExpanded ? <ChevronDown className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                              Liste
                            </Button>
                          </TableCell>
                        </TableRow>
                        {isExpanded && (
                          <TableRow key={`${section.sectionId}-students`} className="bg-muted/20 hover:bg-muted/20">
                            <TableCell colSpan={7} className="p-0">
                              <div className="border-t px-6 py-4">
                                {section.students.length === 0 ? (
                                  <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
                                    <Users className="h-4 w-4" />
                                    Bu şubede kayıtlı öğrenci yok.
                                  </div>
                                ) : (
                                  <div className="max-h-[280px] overflow-auto rounded-md border bg-background">
                                    <Table>
                                      <TableHeader>
                                        <TableRow>
                                          <TableHead>Öğrenci</TableHead>
                                          <TableHead>Numara</TableHead>
                                          <TableHead>E-posta</TableHead>
                                          <TableHead className="w-[120px]">Kayıt</TableHead>
                                        </TableRow>
                                      </TableHeader>
                                      <TableBody>
                                        {section.students.map(student => (
                                          <TableRow key={student.studentId}>
                                            <TableCell className="font-medium">{student.studentName}</TableCell>
                                            <TableCell className="font-mono text-xs">{student.studentNumber}</TableCell>
                                            <TableCell className="text-sm text-muted-foreground">{student.email || '-'}</TableCell>
                                            <TableCell>
                                              <Badge variant="outline">{student.status}</Badge>
                                            </TableCell>
                                          </TableRow>
                                        ))}
                                      </TableBody>
                                    </Table>
                                  </div>
                                )}
                              </div>
                            </TableCell>
                          </TableRow>
                        )}
                      </Fragment>
                    );
                  })}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        )}
      </div>
    </DepartmentOperationsLayout>
  );
}

function MetricCard({
  title,
  value,
  progress,
  tone = 'default',
}: {
  title: string;
  value: number | string | null;
  progress?: number;
  tone?: 'default' | 'warning';
}) {
  return (
    <Card className={tone === 'warning' ? 'border-amber-200 bg-amber-50/40' : undefined}>
      <CardContent className="p-4">
        <p className="text-xs font-medium text-muted-foreground">{title}</p>
        {value === null ? <Skeleton className="mt-2 h-7 w-16" /> : <p className="mt-1 text-2xl font-semibold">{value}</p>}
        {progress !== undefined && <Progress value={Math.min(progress, 100)} className="mt-3 h-2" />}
      </CardContent>
    </Card>
  );
}
