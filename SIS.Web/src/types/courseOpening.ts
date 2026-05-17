// ── Response Types ──

export interface PoolSlotForOpeningDto {
  poolId: number;
  slotName: string;
  electiveGroupId: number;
  electiveGroupName: string;
  /** ElectiveSubtype.Code — TE / NDE / OC / custom; renk için kullanılır. */
  subtypeCode: string;
  subtypeName: string;
  suggestedSemesterStart: number;
  suggestedSemesterEnd: number;
  /** Havuz override AKTS. Null ise her aday dersin kendi katalog AKTS'si geçerli. */
  ectsPerCourse: number | null;
  candidateCount: number;
  /** Aday dersler arasında verilen termId'de en az 1 aktif şubeye sahip olanların sayısı. */
  openedCandidateCount: number;
}

export interface PoolCandidateDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  theoryHours: number;
  labHours: number;
  nationalCredit: number;
  ects: number;
  alreadyOpened: boolean;
  existingSectionCount: number;
  canOpen: boolean;
  blockReason: 'TermNotInPlanning' | 'NotOwner' | 'CourseUnassigned' | null;
}


export interface OpenedCourseSectionDto {
  sectionId: number;
  sectionNumber: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  theoryHours: number;
  labHours: number;
  credits: number;
  ects: number;
  suggestedSemester: number;
  capacity: number;
  internalQuota: number | null;
  externalQuota: number | null;
  enrolledCount: number;
  registeredCount: number;
  draftOrPendingCount: number;
  instructorId: number | null;
  instructorName: string | null;
  isActive: boolean;
  departmentId: number | null;
  departmentName: string | null;
  crossListGroupId: number | null;
  /** Z (zorunlu) / S (seçmeli) — bölümün aktif müfredatından alınır. */
  isCompulsory: boolean;
}

export interface OpenedCourseListDto {
  departmentId: number;
  departmentName: string;
  termId: number;
  termName: string;
  totalSections: number;
  sections: OpenedCourseSectionDto[];
}

export interface CrossDepartmentDemandDto {
  departmentId: number;
  departmentName: string;
  programName: string;
  estimatedStudentCount: number;
}

export interface CurriculumCourseForOpeningDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  theoryHours: number;
  labHours: number;
  nationalCredit: number;
  ects: number;
  suggestedSemester: number;
  isCompulsory: boolean;
  alreadyOpened: boolean;
  existingSectionCount: number;
  predictedDemand?: number;
  retakeCount?: number;
  recommendedSectionCount?: number;
  recommendedCapacity?: number;
  crossDepartmentDemand?: CrossDepartmentDemandDto[];
  previousTermEnrollment?: number;
  canOpen?: boolean;
  /** CanOpen false olduğunda sebep — null = açılabilir. */
  blockReason?: 'TermNotInPlanning' | 'NotOwner' | 'CourseUnassigned' | null;
  // ── Seçmeli havuz bilgileri (null = havuz üyesi değil) ──
  /** Kaynak: "Compulsory" (zorunlu), "Elective" (tekil seçmeli), "Pool" (havuz üyesi). */
  source?: 'Compulsory' | 'Elective' | 'Pool';
  /** Havuzun Id'si (CurriculumElectivePool). */
  electivePoolId?: number | null;
  /** Havuz kısaltması — "NDE", "TE", "OC". */
  electiveGroupCode?: string | null;
  /** Havuz adı — "Bölüm Dışı Seçmeli". */
  electiveGroupName?: string | null;
}

export interface InstructorOptionDto {
  instructorId: number;
  fullName: string;
  departmentId: number;
  departmentName: string;
  departmentCode: string;
  maxWeeklyHours: number;
  assignedWeeklyHours: number;
  /** Toplu danışman atamasında bu hocaya verilebilecek max öğrenci sayısı. null = sınırsız. */
  advisorAssignmentCapacity: number | null;
  /** Toplu danışman atamasına bu hoca dahil edilsin mi. */
  isAvailableForAdvisorAssignment: boolean;
  /** Bu hocaya şu an atanmış öğrenci sayısı. */
  currentStudentCount: number;
}

// ── Request Types ──

export interface OpenCourseRequest {
  courseId: number;
  sectionCount: number;
  capacityPerSection: number;
  internalQuota?: number;
  externalQuota?: number;
  instructorId?: number;
  accessibleDepartmentIds?: number[];
  /** Madde 14: Cross-list'li dersler için paralel section da açılsın mı? */
  includeCrossLists?: boolean;
}

export interface BulkOpenCoursesRequest {
  courses: OpenCourseRequest[];
}

export interface OpenedCourseSummaryDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  sectionCount: number;
  sectionNumbers: number[];
  totalCapacity: number;
  instructorName: string | null;
}

export interface BulkOpenCoursesResultDto {
  totalCoursesOpened: number;
  totalSectionsCreated: number;
  openedCourses: OpenedCourseSummaryDto[];
  errors: string[];
}

export interface UpdateOpenedSectionRequest {
  sectionId: number;
  instructorId?: number;
  capacity?: number;
  internalQuota?: number | null;
  externalQuota?: number | null;
}

// ── Bulk Assign ──

export interface BulkAssignInstructorRequest {
  assignments: { sectionId: number; instructorId: number }[];
}

export interface BulkAssignInstructorResultDto {
  successCount: number;
  failedCount: number;
  errors: string[];
}

// ── Section-to-Section Student Transfer ──

export interface TransferStudentsToSectionRequest {
  sourceSectionId: number;
  targetSectionId: number;
  studentIds: number[];
}

export interface TransferStudentsToSectionResultDto {
  requestedCount: number;
  transferredCount: number;
  transferredStudentIds: number[];
}

// ── Auto-Split (Otomatik Şubelendirme) ──

export type AutoSplitStrategy = 'Equal' | 'EqualRepeat' | 'ModOddEven';

export interface AutoSplitCourseRequest {
  courseId: number;
  termId: number;
  targetSectionCount: number;
  strategy: AutoSplitStrategy;
  defaultCapacityPerSection?: number;
}

export interface AutoSplitSectionSummary {
  sectionId: number;
  sectionNumber: number;
  assignedStudentCount: number;
}

export interface AutoSplitCourseResultDto {
  finalSectionCount: number;
  createdSectionCount: number;
  closedSectionCount: number;
  redistributedRegistrationCount: number;
  sections: AutoSplitSectionSummary[];
}

// ── Term Transfer ──

export interface PreviousTermCourseDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  credits: number;
  suggestedSemester: number;
  isCompulsory: boolean;
  sectionCount: number;
  totalCapacity: number;
  instructorName: string | null;
  instructorId: number | null;
  alreadyOpened: boolean;
  totalEnrolled: number;
  predictedDemand: number | null;
  sourceInternalQuota: number | null;
  sourceExternalQuota: number | null;
  sourceAccessibleDepartmentIds: number[] | null;
  hasCrossLists: boolean;
  hasSchedule: boolean;
}

export interface TransferCourseItem {
  courseId: number;
  sectionCount?: number;
  capacityPerSection?: number;
  instructorId?: number | null;
  internalQuota?: number | null;
  externalQuota?: number | null;
  accessibleDepartmentIds?: number[] | null;
  includeCrossLists?: boolean;
  includeSchedule?: boolean;
}

export interface TransferFromTermRequest {
  departmentId: number;
  sourceTermId: number;
  targetTermId: number;
  courses: TransferCourseItem[];
}

export interface TransferCourseResultItem {
  courseId: number;
  courseCode: string;
  courseName: string;
  success: boolean;
  error?: string;
  sectionsCreated: number;
  warnings: string[];
  incidentId?: string;
}

export interface TransferFromTermResultDto {
  successCount: number;
  failedCount: number;
  totalSectionsCreated: number;
  results: TransferCourseResultItem[];
}

export interface TransferDefaultsDto {
  defaultCapacityPerSection: number;
  maxSectionsPerCourse: number;
}

// ── Instructor Workload ──

export interface AssignedSectionDto {
  sectionId: number;
  courseCode: string;
  courseName: string;
  departmentId: number | null;
  departmentName: string | null;
  departmentCode: string | null;
  sectionNumber: number;
  weeklyHours: number;
  capacity: number;
  enrolledCount: number;
}

export interface InstructorWorkloadDto {
  instructorId: number;
  fullName: string;
  title: string;
  maxWeeklyHours: number;
  assignedWeeklyHours: number;
  remainingCapacity: number;
  assignedSections: AssignedSectionDto[];
}
