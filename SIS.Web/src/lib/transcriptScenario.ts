import type { StudentTranscriptDto } from '../types/transcript';

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
  { value: 'S', label: 'S', point: null, passed: true, countsTowardsGpa: false },
  { value: 'U', label: 'U', point: null, passed: false, countsTowardsGpa: false },
  { value: 'W', label: 'W', point: null, passed: false, countsTowardsGpa: false },
  { value: 'I', label: 'I', point: null, passed: false, countsTowardsGpa: false },
  { value: 'NI', label: 'NI', point: null, passed: false, countsTowardsGpa: false },
] as const;

export type ScenarioGrade = (typeof GRADE_OPTIONS)[number]['value'];

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
  termId: number;
  courseCode: string;
  courseName: string;
  credits: number;
  ects: number;
  averageScore: number | null;
  grade: ScenarioGrade;
  excludeFromAverage: boolean;
  continuationCourseId: string;
  isAdded: boolean;
  wasRepeated: boolean;
  sortOrder: number;
}

export interface ScenarioComputedCourse extends ScenarioCourse {
  gradePoint: number | null;
  countsTowardsGpa: boolean;
  isPassed: boolean;
  weightedEcts: number | null;
  chainId: string;
  isRepeated: boolean;
}

export interface ScenarioTermReport {
  termId: number;
  termName: string;
  year: number;
  semester: string;
  attemptedCredits: number;
  attemptedEcts: number;
  earnedCredits: number;
  earnedEcts: number;
  gpaEcts: number;
  gpaPoints: number;
  cumulativeGpaEcts: number;
  cumulativeGpaPoints: number;
  sgpa: number;
  cgpa: number;
  courses: ScenarioComputedCourse[];
}

export interface ScenarioReport {
  currentCgpa: number;
  totalAttemptedCredits: number;
  totalAttemptedEcts: number;
  totalEarnedCredits: number;
  totalEarnedEcts: number;
  totalGpaEcts: number;
  totalGpaPoints: number;
  terms: ScenarioTermReport[];
}

export function buildScenarioTerms(transcript: StudentTranscriptDto): ScenarioTerm[] {
  return transcript.terms.map((term, index) => ({
    termId: term.termId,
    termName: term.termName,
    year: term.year,
    semester: term.semester,
    order: index,
  }));
}

export function buildInitialScenarioCourses(transcript: StudentTranscriptDto): ScenarioCourse[] {
  const rootScenarioIdByCourseCode = new Map<string, string>();
  let sortOrder = 0;

  return transcript.terms.flatMap(term =>
    term.courses.map(course => {
      const scenarioId = `reg-${course.registrationId}`;
      const normalizedCourseCode = normalizeCourseCode(course.courseCode);
      const continuationCourseId = rootScenarioIdByCourseCode.get(normalizedCourseCode) ?? scenarioId;

      if (!rootScenarioIdByCourseCode.has(normalizedCourseCode)) {
        rootScenarioIdByCourseCode.set(normalizedCourseCode, continuationCourseId);
      }

      return {
        scenarioId,
        sourceRegistrationId: course.registrationId,
        termId: term.termId,
        courseCode: course.courseCode,
        courseName: course.courseName,
        credits: course.credits,
        ects: course.ects,
        averageScore: course.averageScore,
        grade: normalizeGrade(course.grade),
        excludeFromAverage: !course.countsTowardsGpa,
        continuationCourseId,
        isAdded: false,
        wasRepeated: course.isRepeated,
        sortOrder: sortOrder++,
      } satisfies ScenarioCourse;
    })
  );
}

export function calculateScenarioReport(
  terms: ScenarioTerm[],
  courses: ScenarioCourse[],
): ScenarioReport {
  const sortedTerms = [...terms].sort((left, right) => left.order - right.order);
  const sortedCourses = [...courses].sort((left, right) => left.sortOrder - right.sortOrder);
  const coursesById = new Map(sortedCourses.map(course => [course.scenarioId, course]));
  const chainIdByCourseId = new Map(sortedCourses.map(course => [course.scenarioId, resolveChainId(course, coursesById)]));
  const repeatedChainCounts = new Map<string, number>();

  for (const course of sortedCourses) {
    const chainId = chainIdByCourseId.get(course.scenarioId) ?? course.scenarioId;
    repeatedChainCounts.set(chainId, (repeatedChainCounts.get(chainId) ?? 0) + 1);
  }

  const latestByChainId = new Map<string, ScenarioComputedCourse>();
  const termReports: ScenarioTermReport[] = [];

  for (const term of sortedTerms) {
    const termCourses = sortedCourses
      .filter(course => course.termId === term.termId)
      .map(course => {
        const gradeMeta = getGradeMeta(course.grade);
        const countsTowardsGpa = !course.excludeFromAverage && gradeMeta.countsTowardsGpa;
        const chainId = chainIdByCourseId.get(course.scenarioId) ?? course.scenarioId;

        return {
          ...course,
          gradePoint: countsTowardsGpa ? gradeMeta.point : null,
          countsTowardsGpa,
          isPassed: gradeMeta.passed,
          weightedEcts: countsTowardsGpa && gradeMeta.point != null
            ? round2(course.ects * gradeMeta.point)
            : null,
          chainId,
          isRepeated: course.wasRepeated || (repeatedChainCounts.get(chainId) ?? 0) > 1,
        } satisfies ScenarioComputedCourse;
      });

    for (const course of termCourses) {
      latestByChainId.set(course.chainId, course);
    }

    const cumulativeCourses = Array.from(latestByChainId.values());
    const sgpa = calculateAverage(termCourses);
    const cgpa = calculateAverage(cumulativeCourses);
    const gpaEcts = termCourses.filter(course => course.countsTowardsGpa).reduce((sum, course) => sum + course.ects, 0);
    const gpaPoints = round2(termCourses.reduce((sum, course) => sum + (course.weightedEcts ?? 0), 0));
    const cumulativeGpaEcts = cumulativeCourses.filter(course => course.countsTowardsGpa).reduce((sum, course) => sum + course.ects, 0);
    const cumulativeGpaPoints = round2(cumulativeCourses.reduce((sum, course) => sum + (course.weightedEcts ?? 0), 0));

    termReports.push({
      termId: term.termId,
      termName: term.termName,
      year: term.year,
      semester: term.semester,
      attemptedCredits: termCourses.reduce((sum, course) => sum + course.credits, 0),
      attemptedEcts: termCourses.reduce((sum, course) => sum + course.ects, 0),
      earnedCredits: termCourses.filter(course => course.isPassed).reduce((sum, course) => sum + course.credits, 0),
      earnedEcts: termCourses.filter(course => course.isPassed).reduce((sum, course) => sum + course.ects, 0),
      gpaEcts,
      gpaPoints,
      cumulativeGpaEcts,
      cumulativeGpaPoints,
      sgpa,
      cgpa,
      courses: termCourses,
    });
  }

  const latestCourses = Array.from(latestByChainId.values());
  const totalGpaEcts = latestCourses.filter(course => course.countsTowardsGpa).reduce((sum, course) => sum + course.ects, 0);
  const totalGpaPoints = round2(latestCourses.reduce((sum, course) => sum + (course.weightedEcts ?? 0), 0));

  return {
    currentCgpa: calculateAverage(latestCourses),
    totalAttemptedCredits: sortedCourses.reduce((sum, course) => sum + course.credits, 0),
    totalAttemptedEcts: sortedCourses.reduce((sum, course) => sum + course.ects, 0),
    totalEarnedCredits: latestCourses.filter(course => course.isPassed).reduce((sum, course) => sum + course.credits, 0),
    totalEarnedEcts: latestCourses.filter(course => course.isPassed).reduce((sum, course) => sum + course.ects, 0),
    totalGpaEcts,
    totalGpaPoints,
    terms: termReports,
  };
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
        termId: course.termId,
        courseCode: course.courseCode,
        courseName: course.courseName,
        credits: course.credits,
        ects: course.ects,
        averageScore: course.averageScore,
        grade: course.grade,
        excludeFromAverage: course.excludeFromAverage,
        continuationCourseId: course.continuationCourseId,
        isAdded: course.isAdded,
        wasRepeated: course.wasRepeated,
        sortOrder: course.sortOrder,
      }));

  return JSON.stringify(serialize(left)) === JSON.stringify(serialize(right));
}

export function getGradeMeta(grade: string) {
  return GRADE_OPTIONS.find(option => option.value === normalizeGrade(grade)) ?? GRADE_OPTIONS[0];
}

export function formatGradeOptionLabel(grade: ScenarioGrade) {
  const meta = getGradeMeta(grade);
  return `${meta.label} -> Katsayi:${meta.point == null ? 'GPA Disi' : trimNumber(meta.point)}`;
}

export function normalizeGrade(grade: string): ScenarioGrade {
  const normalized = grade.toUpperCase() as ScenarioGrade;
  return GRADE_OPTIONS.some(option => option.value === normalized) ? normalized : 'FF';
}

export function normalizeCourseCode(courseCode: string) {
  return courseCode.trim().toUpperCase();
}

export function round2(value: number) {
  return Math.round(value * 100) / 100;
}

function calculateAverage(courses: ScenarioComputedCourse[]) {
  const gpaEcts = courses.filter(course => course.countsTowardsGpa).reduce((sum, course) => sum + course.ects, 0);
  if (gpaEcts === 0) {
    return 0;
  }

  const gpaPoints = courses.reduce((sum, course) => sum + (course.weightedEcts ?? 0), 0);
  return round2(gpaPoints / gpaEcts);
}

function resolveChainId(course: ScenarioCourse, coursesById: Map<string, ScenarioCourse>, visited = new Set<string>()): string {
  if (!course.continuationCourseId || course.continuationCourseId === course.scenarioId) {
    return course.scenarioId;
  }

  if (visited.has(course.scenarioId)) {
    return course.scenarioId;
  }

  const targetCourse = coursesById.get(course.continuationCourseId);
  if (!targetCourse) {
    return course.scenarioId;
  }

  visited.add(course.scenarioId);
  return resolveChainId(targetCourse, coursesById, visited);
}

function trimNumber(value: number) {
  return value.toString().replace(/\.0$/, '');
}
