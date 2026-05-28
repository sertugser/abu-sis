import {
  Table, TableBody, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import type { CatalogClassYear, CatalogCourseRow, CatalogSemester } from '@/types/registrationCatalog';
import { CurriculumCourseRow } from './CurriculumCourseRow';

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

function semesterLabel(s: CatalogSemester): string {
  const parityLabel = s.parity === 'Guz' ? 'Güz' : 'Bahar';
  return `${s.semesterNumber}. Yarıyıl (${parityLabel})`;
}

export function CurriculumCourseGrid({
  classYearData,
  parityFilter,
  onPickSection,
  registrationActionsOpen,
}: Props) {
  if (classYearData == null) {
    return (
      <div className="px-4 py-8 text-center text-xs text-muted-foreground border-b border-border/40">
        Sınıf seçin.
      </div>
    );
  }

  const visibleSemesters = filterSemesters(classYearData.semesters, parityFilter);

  if (visibleSemesters.length === 0) {
    return (
      <div className="px-4 py-8 text-center text-xs text-muted-foreground border-b border-border/40">
        Bu dönemde ders bulunmuyor.
      </div>
    );
  }

  // Sort semesters: Güz first (odd semesterNumber), then Bahar (even)
  const sorted = [...visibleSemesters].sort((a, b) => a.semesterNumber - b.semesterNumber);

  return (
    <div>
      {sorted.map(semester => {
        const courses = sortCourses(semester.courses);

        return (
          <div
            key={`${semester.parity}-${semester.semesterNumber}`}
            className="border-b border-border/40"
          >
            <h3 className="text-xs font-semibold uppercase tracking-wide bg-muted/40 px-3 py-1.5 text-muted-foreground">
              {semesterLabel(semester)}
            </h3>

            <Table>
              <TableHeader>
                <TableRow className="bg-muted/20 hover:bg-muted/20">
                  <TableHead className="w-10 py-1 px-2 text-xs" />
                  <TableHead className="w-28 py-1 px-2 text-xs">Ders Kodu</TableHead>
                  <TableHead className="py-1 px-2 text-xs">Ders Adı</TableHead>
                  <TableHead className="w-12 py-1 px-2 text-center text-xs">Z/S</TableHead>
                  <TableHead className="w-12 py-1 px-2 text-center text-xs">KRD</TableHead>
                  <TableHead className="w-14 py-1 px-2 text-center text-xs">AKTS</TableHead>
                  <TableHead className="hidden md:table-cell w-14 py-1 px-2 text-center text-xs">
                    Sınıf
                  </TableHead>
                  <TableHead className="hidden lg:table-cell py-1 px-2 text-xs">
                    Önceki Tekrar Durumu
                  </TableHead>
                  <TableHead className="w-32 py-1 px-2 text-center text-xs">
                    Aktif Kayıt Durumu
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {courses.map(course => (
                  <CurriculumCourseRow
                    key={course.courseId}
                    row={course}
                    onPickSection={onPickSection}
                    registrationActionsOpen={registrationActionsOpen}
                  />
                ))}
              </TableBody>
            </Table>
          </div>
        );
      })}
    </div>
  );
}
