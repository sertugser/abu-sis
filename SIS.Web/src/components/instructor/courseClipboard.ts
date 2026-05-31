import type { InstructorSection } from '../../api/instructor.api';

/** Tab-separated clipboard export of the instructor course list. */
export function buildClipboardText(sections: InstructorSection[]): string {
  const header = ['Şb.', 'Ders Kodu', 'Ders Adı', 'Fakülte', 'Program', 'Durum'].join('\t');
  const rows = sections.map((s) =>
    [
      s.sectionNumber,
      s.courseCode,
      s.courseName,
      s.facultyName,
      s.programName,
      s.isFinalized ? 'Kesinleşti' : 'Aktif',
    ].join('\t'),
  );
  return [header, ...rows].join('\n');
}
