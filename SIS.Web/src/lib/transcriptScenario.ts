import type { StudentTranscriptDto } from '../types/transcript';

// ─────────────────────────────────────────────────────────────────────────────
// Transkript Senaryosu (what-if) — FRONTEND yalnızca düzenlenebilir GİRDİ state'ini
// hazırlar ve dropdown etiketlerini üretir. AGNO/ANO/AKTS/geçti HESABI backend'de
// TEK kaynaktan (GradePointMap + GpaMath) yapılır; bu dosyada GPA hesabı YOKTUR.
//
// GRADE_OPTIONS yalnızca not seçim dropdown'ı ve katsayı etiketini gösterir; hesabı
// SÜRÜKLEMEZ (sonuç backend'den gelir). Akademik katsayılar (AA=4.0 …) statik sabittir.
// ─────────────────────────────────────────────────────────────────────────────

export const GRADE_OPTIONS = [
  { value: 'AA', label: 'AA', point: 4.0, passed: true, countsTowardsGpa: true },
  { value: 'BA', label: 'BA', point: 3.5, passed: true, countsTowardsGpa: true },
  { value: 'BB', label: 'BB', point: 3.0, passed: true, countsTowardsGpa: true },
  { value: 'CB', label: 'CB', point: 2.5, passed: true, countsTowardsGpa: true },
  { value: 'CC', label: 'CC', point: 2.0, passed: true, countsTowardsGpa: true },
  { value: 'DC', label: 'DC', point: 1.5, passed: true, countsTowardsGpa: true },
  { value: 'DD', label: 'DD', point: 1.0, passed: true, countsTowardsGpa: true },
  { value: 'FF', label: 'FF', point: 0.0, passed: false, countsTowardsGpa: true },
  { value: 'NA', label: 'NA', point: 0.0, passed: false, countsTowardsGpa: true },
  // NI: kredili ama GPA-dışı tanınan ders → GEÇER sayılır (backend ile aynı: GpaMath.IsPassedGrade).
  { value: 'NI', label: 'NI', point: null, passed: true, countsTowardsGpa: false },
  { value: 'S', label: 'S', point: null, passed: true, countsTowardsGpa: false },
  { value: 'U', label: 'U', point: null, passed: false, countsTowardsGpa: false },
  { value: 'W', label: 'W', point: null, passed: false, countsTowardsGpa: false },
  { value: 'I', label: 'I', point: null, passed: false, countsTowardsGpa: false },
] as const;

export type ScenarioGrade = (typeof GRADE_OPTIONS)[number]['value'];

// Boş harf = "not henüz girilmedi/sonuçlandırılmadı". Bu durumda ders ortalamaya katılmaz
// ve tabloda "—" gösterilir; öğrenci elle harf seçince hesaba girer. Aktif dönemin notsuz
// (projeksiyon) dersleri bu değerle başlar — varsayılan bir not ATANMAZ.
const EMPTY_GRADE_META = { value: '', label: '—', point: null, passed: false, countsTowardsGpa: false } as const;

export interface ScenarioTerm {
  termId: number;
  termName: string;
  year: number;
  semester: string;
  order: number;
}

export interface ScenarioCourse {
  scenarioId: string;
  sourceRegistrationId: number | null;
  courseId: number | null;
  termId: number;
  courseCode: string;
  courseName: string;
  credits: number;
  ects: number;
  equivalentCourseIds: number[];
  averageScore: number | null;
  // '' = not henüz girilmedi (projeksiyon/yeni ders); ortalamaya katılmaz, "—" gösterilir.
  grade: ScenarioGrade | '';
  originalGrade: ScenarioGrade | null;
  excludeFromAverage: boolean;
  continuationCourseId: string;
  isAdded: boolean;
  isPlannedFromCurriculum: boolean;
  wasRepeated: boolean;
  sortOrder: number;
}

export function buildScenarioTerms(transcript: StudentTranscriptDto): ScenarioTerm[] {
  const terms: ScenarioTerm[] = transcript.terms.map((term, index) => ({
    termId: term.termId,
    termName: term.termName,
    year: term.year,
    semester: term.semester,
    order: index,
  }));

  // Aktif dönemin notsuz dersleri ayrı bir terimde olabilir (henüz transkriptte değil).
  // O dönem transkriptte yoksa senaryo terim listesine ekle (en sona — en güncel dönem).
  const existingTermIds = new Set(terms.map(term => term.termId));
  for (const course of transcript.inProgressCourses) {
    if (existingTermIds.has(course.termId)) {
      continue;
    }
    existingTermIds.add(course.termId);
    terms.push({
      termId: course.termId,
      termName: course.termName,
      year: course.year,
      semester: course.semester,
      order: terms.length,
    });
  }

  return terms;
}

export function buildInitialScenarioCourses(transcript: StudentTranscriptDto): ScenarioCourse[] {
  const rootScenarioIdByChainKey = new Map<string, string>();
  const transcriptChainKeyByCourseId = buildTranscriptChainKeyByCourseId(transcript);
  let sortOrder = 0;

  const gradedCourses = transcript.terms.flatMap(term =>
    term.courses.map(course => {
      const scenarioId = `reg-${course.registrationId}`;
      const chainKey = getTranscriptChainKey(course.courseId, course.courseCode, transcriptChainKeyByCourseId);
      const continuationCourseId = rootScenarioIdByChainKey.get(chainKey) ?? scenarioId;

      if (!rootScenarioIdByChainKey.has(chainKey)) {
        rootScenarioIdByChainKey.set(chainKey, continuationCourseId);
      }

      return {
        scenarioId,
        sourceRegistrationId: course.registrationId,
        courseId: course.courseId,
        termId: term.termId,
        courseCode: course.courseCode,
        courseName: course.courseName,
        credits: course.credits,
        ects: course.ects,
        equivalentCourseIds: course.equivalentCourseIds,
        averageScore: course.averageScore,
        grade: normalizeGrade(course.grade),
        originalGrade: normalizeGrade(course.grade) || null,
        excludeFromAverage: !course.countsTowardsGpa,
        continuationCourseId,
        isAdded: false,
        isPlannedFromCurriculum: false,
        wasRepeated: course.isRepeated,
        sortOrder: sortOrder++,
      } satisfies ScenarioCourse;
    })
  );

  // Aktif dönemin notsuz dersleri: projeksiyon satırı olarak eklenir. grade = '' (not henüz
  // girilmedi) → varsayılan bir not ATANMAZ, ortalamaya katılmaz, tabloda "—" gösterilir.
  // Öğrenci harf seçince hesaba girer. originalGrade = null (gerçek not yok) → backend
  // "başarısız denemeyi onarma" tetiklenmez. Tekrar zinciri varsa (aynı/eşdeğer ders daha önce
  // alınmış) ona bağlanır → öğrenci not seçtiğinde "son deneme geçerli" kuralı uygulanır.
  const inProgressCourses = transcript.inProgressCourses.map(course => {
    const scenarioId = `reg-${course.registrationId}`;
    const chainKey = getTranscriptChainKey(course.courseId, course.courseCode, transcriptChainKeyByCourseId);
    const continuationCourseId = rootScenarioIdByChainKey.get(chainKey) ?? scenarioId;

    if (!rootScenarioIdByChainKey.has(chainKey)) {
      rootScenarioIdByChainKey.set(chainKey, continuationCourseId);
    }

    return {
      scenarioId,
      sourceRegistrationId: course.registrationId,
      courseId: course.courseId,
      termId: course.termId,
      courseCode: course.courseCode,
      courseName: course.courseName,
      credits: course.credits,
      ects: course.ects,
      equivalentCourseIds: course.equivalentCourseIds,
      averageScore: null,
      grade: '',
      originalGrade: null,
      excludeFromAverage: false,
      continuationCourseId,
      isAdded: false,
      isPlannedFromCurriculum: false,
      wasRepeated: continuationCourseId !== scenarioId,
      sortOrder: sortOrder++,
    } satisfies ScenarioCourse;
  });

  return [...gradedCourses, ...inProgressCourses];
}

export function createScenarioId() {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return `scenario-${globalThis.crypto.randomUUID()}`;
  }

  return `scenario-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

export function areScenarioCoursesEqual(left: ScenarioCourse[], right: ScenarioCourse[]) {
  if (left.length !== right.length) {
    return false;
  }

  const serialize = (courses: ScenarioCourse[]) =>
    [...courses]
      .sort((a, b) => a.scenarioId.localeCompare(b.scenarioId))
      .map(course => ({
        scenarioId: course.scenarioId,
        sourceRegistrationId: course.sourceRegistrationId,
        courseId: course.courseId,
        termId: course.termId,
        courseCode: course.courseCode,
        courseName: course.courseName,
        credits: course.credits,
        ects: course.ects,
        equivalentCourseIds: course.equivalentCourseIds,
        averageScore: course.averageScore,
        grade: course.grade,
        originalGrade: course.originalGrade,
        excludeFromAverage: course.excludeFromAverage,
        continuationCourseId: course.continuationCourseId,
        isAdded: course.isAdded,
        isPlannedFromCurriculum: course.isPlannedFromCurriculum,
        wasRepeated: course.wasRepeated,
        sortOrder: course.sortOrder,
      }));

  return JSON.stringify(serialize(left)) === JSON.stringify(serialize(right));
}

// Dropdown etiketi/katsayı gösterimi (display-only) — hesabı sürüklemez.
// Boş/geçersiz harf → "henüz girilmedi" meta'sı (point null, GPA-dışı, geçmedi).
export function getGradeMeta(grade: string) {
  if (!grade || !grade.trim()) {
    return EMPTY_GRADE_META;
  }
  const normalized = grade.toUpperCase();
  return GRADE_OPTIONS.find(option => option.value === normalized) ?? EMPTY_GRADE_META;
}

export function formatGradeOptionLabel(grade: ScenarioGrade) {
  const meta = getGradeMeta(grade);
  return `${meta.label} -> Katsayı:${meta.point == null ? 'GPA Dışı' : trimNumber(meta.point)}`;
}

// Boş → '' (not girilmedi); bilinen harf → kendisi; tanınmayan dolu değer → '' (sessizce
// FF'e düşürmek yerine "girilmedi" say — kullanıcı bilerek seçmedikçe ortalamayı bozmaz).
export function normalizeGrade(grade: string): ScenarioGrade | '' {
  if (!grade || !grade.trim()) {
    return '';
  }
  const normalized = grade.toUpperCase() as ScenarioGrade;
  return GRADE_OPTIONS.some(option => option.value === normalized) ? normalized : '';
}

export function normalizeCourseCode(courseCode: string) {
  return courseCode.trim().toUpperCase();
}

export function round2(value: number) {
  return Math.round(value * 100) / 100;
}

function trimNumber(value: number) {
  return value.toString().replace(/\.0$/, '');
}

// Başlangıç tekrar-zinciri kurulumu: transkriptteki eşdeğer/aynı dersleri tek zincire
// bağlar (continuationCourseId). Bu yalnızca GİRDİ hazırlığıdır; nihai zincir/GPA hesabı
// backend'de yeniden yapılır.
function buildTranscriptChainKeyByCourseId(transcript: StudentTranscriptDto) {
  // Zincir haritası hem notlu (terms) hem aktif dönem notsuz (inProgressCourses) dersleri
  // kapsar; böylece şu an alınan ders, daha önce alınmış aynı/eşdeğer derse zincirlenir.
  const transcriptCourses = [
    ...transcript.terms.flatMap(term => term.courses),
    ...transcript.inProgressCourses,
  ];
  const courseIdsInTranscript = new Set(transcriptCourses.map(course => course.courseId));
  const adjacency = new Map<number, Set<number>>();

  for (const course of transcriptCourses) {
    const neighbors = adjacency.get(course.courseId) ?? new Set<number>();
    neighbors.add(course.courseId);

    for (const equivalentCourseId of course.equivalentCourseIds) {
      neighbors.add(equivalentCourseId);

      if (courseIdsInTranscript.has(equivalentCourseId)) {
        const reverseNeighbors = adjacency.get(equivalentCourseId) ?? new Set<number>();
        reverseNeighbors.add(equivalentCourseId);
        reverseNeighbors.add(course.courseId);
        adjacency.set(equivalentCourseId, reverseNeighbors);
      }
    }

    adjacency.set(course.courseId, neighbors);
  }

  const chainKeyByCourseId = new Map<number, string>();
  const visited = new Set<number>();
  const sortedCourseIds = [...courseIdsInTranscript].sort((left, right) => left - right);

  for (const courseId of sortedCourseIds) {
    if (visited.has(courseId)) {
      continue;
    }

    const queue = [courseId];
    const component: number[] = [];
    visited.add(courseId);

    while (queue.length > 0) {
      const currentCourseId = queue.shift()!;
      component.push(currentCourseId);

      for (const neighborCourseId of adjacency.get(currentCourseId) ?? []) {
        if (!courseIdsInTranscript.has(neighborCourseId) || visited.has(neighborCourseId)) {
          continue;
        }

        visited.add(neighborCourseId);
        queue.push(neighborCourseId);
      }
    }

    const chainKey = `course:${Math.min(...component)}`;
    for (const componentCourseId of component) {
      chainKeyByCourseId.set(componentCourseId, chainKey);
    }
  }

  return chainKeyByCourseId;
}

function getTranscriptChainKey(
  courseId: number,
  courseCode: string,
  chainKeyByCourseId: Map<number, string>,
) {
  return chainKeyByCourseId.get(courseId) ?? `code:${normalizeCourseCode(courseCode)}`;
}
