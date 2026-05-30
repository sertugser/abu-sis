import {
  Table, TableBody, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import type { CatalogClassYear, CatalogCourseRow, CatalogSemester } from '@/types/registrationCatalog';
import { CurriculumCourseRow } from './CurriculumCourseRow';
import { REG_COL } from './registrationColumns';

export type ParityFilter = 'Tumu' | 'Guz' | 'Bahar';

interface Props {
  classYearData: CatalogClassYear | null;
  parityFilter: ParityFilter;
  onPickSection: (course: CatalogCourseRow) => void;
  registrationActionsOpen: boolean;
}

const TAKE_STATUS_ORDER: Record<string, number> = {
  Alttan: 0,
  IlkDefa: 1,
  OncedenAlindi: 2,
};

function sortCourses(courses: CatalogCourseRow[]): CatalogCourseRow[] {
  return [...courses].sort((a, b) => {
    const oa = TAKE_STATUS_ORDER[a.takeStatus] ?? 3;
    const ob = TAKE_STATUS_ORDER[b.takeStatus] ?? 3;
    if (oa !== ob) return oa - ob;
    return a.code.localeCompare(b.code);
  });
}

function filterSemesters(
  semesters: CatalogSemester[],
  parityFilter: ParityFilter,
): CatalogSemester[] {
  if (parityFilter === 'Tumu') return semesters;
  return semesters.filter(s => s.parity === parityFilter);
}

export function CurriculumCourseGrid({
  classYearData,
  parityFilter,
  onPickSection,
  registrationActionsOpen,
}: Props) {
  if (classYearData == null) {
    return (
      <div className="px-3 py-6 text-center text-xs text-muted-foreground border-b">
        Sınıf seçin.
      </div>
    );
  }

  const visibleSemesters = filterSemesters(classYearData.semesters, parityFilter);

  if (visibleSemesters.length === 0) {
    return (
      <div className="px-3 py-6 text-center text-xs text-muted-foreground border-b">
        Bu dönemde ders bulunmuyor.
      </div>
    );
  }

  // Flatten all courses from all visible semesters into one sorted list
  const sorted = [...visibleSemesters].sort((a, b) => a.semesterNumber - b.semesterNumber);
  const allCourses = sortCourses(sorted.flatMap(s => s.courses));

  return (
    <div className="border-b">
      {/* Section title strip — "Seçilen Dersler" ile simetrik */}
      <div className="flex items-center justify-between border-b bg-muted/40 px-4 py-2.5">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <span className="h-4 w-1 rounded-full bg-primary" />
          Müfredat Dersleri
        </h3>
        <span className="rounded-full bg-background px-2 py-0.5 text-xs font-medium text-muted-foreground">
          {allCourses.length} ders
        </span>
      </div>
      <div className="overflow-x-auto">
        <Table className="min-w-[900px] table-fixed">
          <TableHeader>
            <TableRow className="border-b bg-muted/60 hover:bg-muted/60">
              <TableHead className="h-11 w-12 px-2 text-sm font-semibold" />
              <TableHead className="h-11 w-32 px-2 text-sm font-semibold">Ders Kodu</TableHead>
              <TableHead className={`h-11 ${REG_COL.name} px-3 text-sm font-semibold`}>Ders Adı</TableHead>
              <TableHead className="h-11 w-16 px-2 text-center text-sm font-semibold">Z/S</TableHead>
              <TableHead className="h-11 w-16 px-2 text-center text-sm font-semibold">T+U</TableHead>
              <TableHead className="h-11 w-16 px-2 text-center text-sm font-semibold">KRD</TableHead>
              <TableHead className="h-11 w-16 px-2 text-center text-sm font-semibold">AKTS</TableHead>
              <TableHead className="hidden md:table-cell h-11 w-16 px-2 text-center text-sm font-semibold">
                Sınıf
              </TableHead>
              <TableHead className="hidden lg:table-cell h-11 px-3 text-center text-sm font-semibold">
                Önceki Tekrar Durumu
              </TableHead>
              <TableHead className="h-11 w-32 px-2 text-center text-sm font-semibold">
                Aktif Kayıt Durumu
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {allCourses.map((course, index) => (
              <CurriculumCourseRow
                key={course.courseId}
                row={course}
                index={index}
                onPickSection={onPickSection}
                registrationActionsOpen={registrationActionsOpen}
              />
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
