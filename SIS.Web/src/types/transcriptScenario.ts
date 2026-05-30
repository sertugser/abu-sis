// Transkript Senaryosu (what-if) — backend hesap SONUCU tipleri.
// Hesap TEK kaynakta (backend GradePointMap + GpaMath) yapılır; frontend bu sonucu
// yalnızca gösterir, kendi GPA/AKTS/geçti hesabını TUTMAZ (ikibaşlılık yok).

import type { ScenarioGrade } from '../lib/transcriptScenario';

export interface ScenarioTermInput {
  termId: number;
  termName: string;
  year: number;
  semester: string;
  order: number;
}

export interface ScenarioCourseInput {
  scenarioId: string;
  sourceRegistrationId: number | null;
  courseId: number | null;
  termId: number;
  courseCode: string;
  courseName: string;
  credits: number;
  ects: number;
  averageScore: number | null;
  grade: string;
  originalGrade: string | null;
  excludeFromAverage: boolean;
  continuationCourseId: string;
  isAdded: boolean;
  isPlannedFromCurriculum: boolean;
  wasRepeated: boolean;
  sortOrder: number;
}

export interface TranscriptScenarioRequest {
  terms: ScenarioTermInput[];
  courses: ScenarioCourseInput[];
}

export interface ScenarioComputedCourse {
  scenarioId: string;
  sourceRegistrationId: number | null;
  courseId: number | null;
  termId: number;
  courseCode: string;
  courseName: string;
  credits: number;
  ects: number;
  averageScore: number | null;
  // '' = not henüz girilmedi → ortalamaya katılmaz, "—" gösterilir.
  grade: ScenarioGrade | '';
  originalGrade: ScenarioGrade | null;
  excludeFromAverage: boolean;
  continuationCourseId: string;
  isAdded: boolean;
  isPlannedFromCurriculum: boolean;
  sortOrder: number;
  // Computed (backend)
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
  // GPA'ya giren ders yoksa null → UI "—" gösterir (sahte 0,00 değil).
  sgpa: number | null;
  cgpa: number | null;
  courses: ScenarioComputedCourse[];
}

export interface ScenarioReport {
  // GPA'ya giren ders yoksa null → UI "—".
  currentCgpa: number | null;
  totalAttemptedCredits: number;
  totalAttemptedEcts: number;
  totalEarnedCredits: number;
  totalEarnedEcts: number;
  totalGpaEcts: number;
  totalGpaPoints: number;
  terms: ScenarioTermReport[];
}
