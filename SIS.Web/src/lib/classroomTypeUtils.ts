// Classroom type metadata. Backend enum: SIS.Domain/Enums/ClassroomType.cs
// Keep this list aligned with the backend enum — every enum member must have an entry here.

export const CLASSROOM_TYPE_CODES = [
  'Lecture',
  'Lab',
  'Seminar',
  'Computer',
  'Amphitheater',
  'PhysicsLab',
  'ChemistryLab',
  'BiologyLab',
  'ElectronicsLab',
  'DesignStudio',
] as const;

export type ClassroomTypeCode = typeof CLASSROOM_TYPE_CODES[number];

interface ClassroomTypeMeta {
  label: string;
  badgeClass: string;
  barClass: string;
}

const META: Record<ClassroomTypeCode, ClassroomTypeMeta> = {
  Lecture: {
    label: 'Normal Sınıf',
    badgeClass: 'bg-blue-100 text-blue-800 border-blue-200',
    barClass: 'bg-blue-500',
  },
  Lab: {
    label: 'Laboratuvar',
    badgeClass: 'bg-emerald-100 text-emerald-800 border-emerald-200',
    barClass: 'bg-emerald-500',
  },
  Seminar: {
    label: 'Seminer',
    badgeClass: 'bg-amber-100 text-amber-800 border-amber-200',
    barClass: 'bg-amber-500',
  },
  Computer: {
    label: 'Bilgisayar Lab',
    badgeClass: 'bg-violet-100 text-violet-800 border-violet-200',
    barClass: 'bg-violet-500',
  },
  Amphitheater: {
    label: 'Amfi',
    badgeClass: 'bg-rose-100 text-rose-800 border-rose-200',
    barClass: 'bg-rose-500',
  },
  PhysicsLab: {
    label: 'Fizik Lab',
    badgeClass: 'bg-cyan-100 text-cyan-800 border-cyan-200',
    barClass: 'bg-cyan-500',
  },
  ChemistryLab: {
    label: 'Kimya Lab',
    badgeClass: 'bg-teal-100 text-teal-800 border-teal-200',
    barClass: 'bg-teal-500',
  },
  BiologyLab: {
    label: 'Biyoloji Lab',
    badgeClass: 'bg-lime-100 text-lime-800 border-lime-200',
    barClass: 'bg-lime-500',
  },
  ElectronicsLab: {
    label: 'Elektronik Lab',
    badgeClass: 'bg-indigo-100 text-indigo-800 border-indigo-200',
    barClass: 'bg-indigo-500',
  },
  DesignStudio: {
    label: 'Tasarım Atölyesi',
    badgeClass: 'bg-fuchsia-100 text-fuchsia-800 border-fuchsia-200',
    barClass: 'bg-fuchsia-500',
  },
};

const FALLBACK: ClassroomTypeMeta = {
  label: '',
  badgeClass: 'bg-gray-100 text-gray-700 border-gray-200',
  barClass: 'bg-gray-400',
};

/** Returns the human-readable Turkish label for a classroom type. Falls back to the raw code if unknown. */
export function getClassroomTypeLabel(type: string): string {
  return META[type as ClassroomTypeCode]?.label || type;
}

/** Returns Tailwind classes for a small badge/pill rendering of the type. */
export function getClassroomTypeBadgeClass(type: string): string {
  return META[type as ClassroomTypeCode]?.badgeClass || FALLBACK.badgeClass;
}

/** Returns a single Tailwind background class suitable for distribution bars. */
export function getClassroomTypeBarClass(type: string): string {
  return META[type as ClassroomTypeCode]?.barClass || FALLBACK.barClass;
}
