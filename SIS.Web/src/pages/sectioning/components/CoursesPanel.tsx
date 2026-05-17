import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import type { CourseGroup } from '../types';
import { EmptyMessage } from './EmptyMessage';

interface CoursesPanelProps {
  courses: CourseGroup[];
  selectedCourseCode: string | null;
  onSelectCourse: (courseCode: string) => void;
  isLoading: boolean;
}

export function CoursesPanel({ courses, selectedCourseCode, onSelectCourse, isLoading }: CoursesPanelProps) {
  return (
    <Card className="overflow-hidden">
      <div className="flex items-center justify-between border-b bg-gray-50 px-4 py-2">
        <span className="text-sm font-semibold text-gray-700">Seçilen Programın Açılan Dersleri</span>
        <span className="text-xs text-gray-500 tabular-nums">{courses.length} ders</span>
      </div>
      <div className="max-h-[440px] min-h-[380px] overflow-y-auto">
        {isLoading ? (
          <div className="space-y-2 p-4">
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-8 w-full" />
          </div>
        ) : courses.length === 0 ? (
          <EmptyMessage text="Eşleşen ders bulunamadı" />
        ) : (
          <Table>
            <TableHeader className="sticky top-0 z-10 bg-gray-100">
              <TableRow>
                <TableHead className="h-9 text-xs font-semibold text-gray-700">Ders Kodu</TableHead>
                <TableHead className="h-9 text-xs font-semibold text-gray-700">Ders Adı</TableHead>
                <TableHead className="h-9 w-20 text-center text-xs font-semibold text-gray-700">Şube Say</TableHead>
                <TableHead className="h-9 w-20 text-center text-xs font-semibold text-gray-700">Ö.E Say</TableHead>
                <TableHead className="h-9 w-20 text-center text-xs font-semibold text-gray-700">Öğr. Say</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {courses.map(course => (
                <TableRow
                  key={course.courseCode}
                  onClick={() => onSelectCourse(course.courseCode)}
                  className={cn(
                    'cursor-pointer',
                    course.courseCode === selectedCourseCode ? 'bg-blue-100 hover:bg-blue-100' : 'hover:bg-blue-50',
                  )}
                >
                  <TableCell className="font-mono text-sm font-semibold text-blue-700">{course.courseCode}</TableCell>
                  <TableCell className="text-sm text-gray-800">{course.courseName}</TableCell>
                  <TableCell className="text-center text-sm tabular-nums text-gray-700">{course.sections.length}</TableCell>
                  <TableCell className="text-center text-sm tabular-nums text-gray-700">{course.uniqueInstructorCount}</TableCell>
                  <TableCell className="text-center text-sm tabular-nums text-gray-700">{course.totalStudents}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </Card>
  );
}
