import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import type { PoolProgressDto } from '../../hooks/useStudentPoolStatus';
import type { StudentCourseDto } from '../../types/course';

interface SlotCandidatesTableProps {
  pool: PoolProgressDto;
  courses: StudentCourseDto[];
}

export default function SlotCandidatesTable({ pool, courses }: SlotCandidatesTableProps) {
  if (courses.length === 0) {
    return (
      <div className="text-center py-8 text-sm text-muted-foreground">
        Bu slot için aday ders bulunamadı.
        <p className="text-xs mt-1">
          Grup boş olabilir veya öğrenci kaydı için açık dersi yok.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <span>{courses.length} aday ders</span>
        {pool.ectsPerCourse !== null && (
          <Badge variant="outline" className="text-[10px] bg-blue-50 text-blue-700 border-blue-200">
            Bu havuzda her ders {pool.ectsPerCourse} AKTS
          </Badge>
        )}
      </div>
      <div className="overflow-y-auto max-h-[60vh] -mx-1">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-24">Kod</TableHead>
              <TableHead>Ad</TableHead>
              <TableHead className="text-right w-16">AKTS</TableHead>
              <TableHead className="text-center w-24">Durum</TableHead>
              <TableHead className="text-center w-20">Açık?</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {courses.map(c => (
              <TableRow key={c.courseId}>
                <TableCell className="font-mono text-xs">{c.code}</TableCell>
                <TableCell className="text-sm">{c.name}</TableCell>
                <TableCell className="text-right tabular-nums">{c.ects}</TableCell>
                <TableCell className="text-center">
                  {c.takeStatus === 'Alttan' && (
                    <Badge variant="destructive" className="text-[10px]">Alttan</Badge>
                  )}
                  {c.takeStatus === 'OncedenAlindi' && (
                    <Badge variant="secondary" className="text-[10px]">Alındı</Badge>
                  )}
                  {c.takeStatus === 'IlkDefa' && (
                    <Badge variant="outline" className="text-[10px]">İlk defa</Badge>
                  )}
                </TableCell>
                <TableCell className="text-center">
                  {c.sections.length > 0 ? (
                    <Badge variant="outline" className="text-[10px] bg-emerald-50 text-emerald-700 border-emerald-200">
                      {c.sections.length} şube
                    </Badge>
                  ) : (
                    <span className="text-[10px] text-muted-foreground italic">Açık değil</span>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
