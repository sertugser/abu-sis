/**
 * Derslik tipi gösterim etiketleri ve renkleri — tek kaynak.
 * Önceden 4 ayrı bileşende kopyalanmıştı (ClassroomManagementPage, ClassroomFilters,
 * AvailableClassroomSearchTab, ClassroomListTab); DRY için buraya taşındı.
 */
export const CLASSROOM_TYPE_LABELS: Record<string, string> = {
  Lecture: 'Normal Sınıf',
  Lab: 'Laboratuvar',
  Computer: 'Bilgisayar Lab',
  Seminar: 'Seminer',
  Amphitheater: 'Amfi',
};

export const CLASSROOM_TYPE_COLORS: Record<string, string> = {
  Lecture: 'bg-blue-100 text-blue-700 border-blue-200',
  Lab: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  Computer: 'bg-violet-100 text-violet-700 border-violet-200',
  Seminar: 'bg-amber-100 text-amber-700 border-amber-200',
  Amphitheater: 'bg-rose-100 text-rose-700 border-rose-200',
};

export function classroomTypeLabel(type: string): string {
  return CLASSROOM_TYPE_LABELS[type] ?? type;
}
