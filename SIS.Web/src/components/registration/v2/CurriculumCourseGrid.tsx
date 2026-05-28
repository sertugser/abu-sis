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
      <div className="px-4 py-8 text-center text-[13px] text-muted-foreground border-b border-slate-200">
        Sınıf seçin.
      </div>
    );
  }

  const visibleSemesters = filterSemesters(classYearData.semesters, parityFilter);

  if (visibleSemesters.length === 0) {
    return (
      <div className="px-4 py-8 text-center text-[13px] text-muted-foreground border-b border-slate-200">
        Bu dönemde ders bulunmuyor.
      </div>
    );
  }

  const sorted = [...visibleSemesters].sort((a, b) => a.semesterNumber - b.semesterNumber);

  return (
    <div>
      {sorted.map(semester => {
        const courses = sortCourses(semester.courses);

        return (
          <div
            key={`${semester.parity}-${semester.semesterNumber}`}
            className="border-b border-slate-200"
          >
            {/* Slate-50 section title strip — NOT navy */}
            <div className="border-b border-slate-200 bg-slate-50 px-4 py-2.5">
              <h3 className="text-[15px] font-semibold text-slate-700">
                {semesterLabel(semester)}
              </h3>
            </div>

            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  {/* Dark navy column headers */}
                  <TableRow className="border-b-0 bg-[#003366] hover:bg-[#003366]">
                    <TableHead className="h-10 w-10 px-2 text-[14px] font-semibold text-white" />
                    <TableHead className="h-10 w-28 px-2 text-[14px] font-semibold text-white">Ders Kodu</TableHead>
                    <TableHead className="h-10 px-2 text-[14px] font-semibold text-white">Ders Adı</TableHead>
                    <TableHead className="h-10 w-12 px-2 text-center text-[14px] font-semibold text-white">Z/S</TableHead>
                    <TableHead className="h-10 w-12 px-2 text-center text-[14px] font-semibold text-white">KRD</TableHead>
                    <TableHead className="h-10 w-14 px-2 text-center text-[14px] font-semibold text-white">AKTS</TableHead>
                    <TableHead className="hidden md:table-cell h-10 w-14 px-2 text-center text-[14px] font-semibold text-white">
                      Sınıf
                    </TableHead>
                    <TableHead className="hidden lg:table-cell h-10 px-2 text-[14px] font-semibold text-white">
                      Önceki Tekrar Durumu
                    </TableHead>
                    <TableHead className="h-10 w-32 px-2 text-center text-[14px] font-semibold text-white">
                      Aktif Kayıt Durumu
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {courses.map((course, index) => (
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
      })}
    </div>
  );
}
