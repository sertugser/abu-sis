import type { AdvisorStudent } from '../api/advisor.api';

export type ApprovalCategory = 'pending' | 'approved' | 'noreg' | 'draft' | 'all';

export const APPROVAL_CATEGORIES: { key: ApprovalCategory; label: string }[] = [
  { key: 'pending', label: 'Onay Bekleyen Öğrenciler' },
  { key: 'approved', label: 'Onaylanan Öğrenciler' },
  { key: 'noreg', label: 'Ders Kaydı Yapmayan Öğrenciler' },
  { key: 'draft', label: 'Ders Kaydı Yapıp Kesinleştirmeyen Öğrenciler' },
  { key: 'all', label: 'Tümü' },
];

/** planStatus → kategori eşlemesi. Rejected, "yapıp kesinleştirmeyen" kovasında. */
export function matchesCategory(student: AdvisorStudent, category: ApprovalCategory): boolean {
  switch (category) {
    case 'pending':  return student.planStatus === 'Submitted';
    case 'approved': return student.planStatus === 'Approved';
    case 'noreg':    return student.planStatus === 'None';
    case 'draft':    return student.planStatus === 'Draft' || student.planStatus === 'Rejected';
    case 'all':      return true;
  }
}

export function countByCategory(students: AdvisorStudent[]): Record<ApprovalCategory, number> {
  const counts: Record<ApprovalCategory, number> = { pending: 0, approved: 0, noreg: 0, draft: 0, all: students.length };
  for (const s of students) {
    if (matchesCategory(s, 'pending')) counts.pending++;
    if (matchesCategory(s, 'approved')) counts.approved++;
    if (matchesCategory(s, 'noreg')) counts.noreg++;
    if (matchesCategory(s, 'draft')) counts.draft++;
  }
  return counts;
}

export interface ApprovalFilters {
  category: ApprovalCategory;
  program: string;        // '' = tümü (programName birebir)
  studentNumber: string;  // contains, case-insensitive
  /** Ad ve/veya soyad — birleşik tam ada göre (contains, tr-lower). Tek arama çubuğu. */
  name: string;
}

const tr = (v: string) => v.toLocaleLowerCase('tr');

export function filterStudents(students: AdvisorStudent[], f: ApprovalFilters): AdvisorStudent[] {
  const nameQ = tr(f.name.trim());
  return students.filter(s => {
    const fullName = tr(`${s.firstName ?? ''} ${s.lastName ?? ''}`.trim() || (s.studentName ?? ''));
    return matchesCategory(s, f.category)
      && (f.program === '' || s.programName === f.program)
      && (f.studentNumber === '' || s.studentNumber.includes(f.studentNumber.trim()))
      && (nameQ === '' || fullName.includes(nameQ));
  });
}

/** Listede görünen programlardan distinct dropdown kaynağı. */
export function distinctPrograms(students: AdvisorStudent[]): string[] {
  return Array.from(new Set(students.map(s => s.programName).filter(Boolean))).sort((a, b) => a.localeCompare(b, 'tr'));
}
