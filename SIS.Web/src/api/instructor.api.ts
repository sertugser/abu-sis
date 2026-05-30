import apiClient from './axios';

export interface InstructorProfile {
  instructorId: number;
  title: string;
  firstName: string;
  lastName: string;
  email: string;
  departmentId: number;
  departmentName: string;
  facultyName: string;
  maxWeeklyHours: number;
  officeHours: string;
  advisorOptOutRequested: boolean;
  requireOfficeHoursOnProfileUpdate: boolean;
  primaryEmailImmutable: boolean;
  enableAdvisorOptOutOptions: boolean;
  profileOrgUnitUpdateDeadline: string;
  canUpdateDepartment: boolean;
  simplifyDashboardStatistics: boolean;
  faqUrl: string;
  academicCvActiveYear: number;
  academicCvApprovedActivityEditable: boolean;
  academicCvAutoApprove: boolean;
}

export interface UpdateInstructorProfileDto {
  email: string;
  departmentId: number;
  officeHours?: string;
  advisorOptOutRequested: boolean;
}

export interface InstructorScheduleBlock {
  /** .NET DayOfWeek: 0=Sun, 1=Mon, … */
  day: number;
  /** "HH:mm:ss" */
  startTime: string;
  endTime: string;
  blockType: 'Theory' | 'Lab';
  classroomId: number | null;
  classroomName: string | null;
  classroomType: string | null;
}

export interface InstructorSection {
  sectionId: number;
  sectionNumber: number;
  courseCode: string;
  courseName: string;
  ects: number;
  theoryHours: number;
  labHours: number;
  capacity: number;
  enrolledCount: number;
  availableSeats: number;
  classroomName: string;
  /** Display-only Türkçe summary string (geriye uyumluluk için tutuluyor; UI rendering schedules[] kullanıyor). */
  schedule: string;
  /** 2026-05-10 — Plan E: structured per-block list, replaces frontend parsing of `schedule` string. */
  schedules: InstructorScheduleBlock[];
  isActive: boolean;
  isFinalized: boolean;
  activeStudents: number;
}

export interface InstructorSectionsResponse {
  term: string;
  termId: number;
  totalSections: number;
  totalStudents: number;
  sections: InstructorSection[];
  /** Sistem ayarından gelen öğle arası penceresi, "HH:mm". */
  lunchBreakStart: string;
  lunchBreakEnd: string;
  /** Term'in tanımlı ders saati pencereleri — ızgaranın satır yapısını besler. */
  slots: { start: string; end: string }[];
}

export interface InstructorTerm {
  id: number;
  name: string;
  year: number;
  semester: string;
  isActive: boolean;
}

export interface SectionStudent {
  studentId: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  status: string;
  grade: string | null;
}

export interface SectionStudentsResponse {
  courseCode: string;
  courseName: string;
  sectionId: number;
  students: SectionStudent[];
}

export interface AcademicCvActivity {
  id: number;
  activityYear: number;
  title: string;
  description: string;
  status: string;
  createdAt: string;
  updatedAt?: string | null;
  approvedAt?: string | null;
  canEdit: boolean;
}

export interface AcademicCvResponse {
  activeYear: number;
  approvedActivityEditable: boolean;
  autoApprove: boolean;
  activities: AcademicCvActivity[];
}

export interface CreateAcademicCvActivityDto {
  activityYear: number;
  title: string;
  description?: string;
}

export async function getInstructorProfile(): Promise<InstructorProfile> {
  const { data } = await apiClient.get('/instructor/profile');
  return data;
}

export async function updateInstructorProfile(dto: UpdateInstructorProfileDto): Promise<InstructorProfile> {
  const { data } = await apiClient.put('/instructor/profile', dto);
  return data;
}

export async function getInstructorSections(termId?: number): Promise<InstructorSectionsResponse> {
  const { data } = await apiClient.get('/instructor/my-sections', {
    params: termId != null ? { termId } : undefined,
  });
  return data;
}

export async function getInstructorTerms(): Promise<InstructorTerm[]> {
  const { data } = await apiClient.get('/instructor/my-terms');
  return data;
}

export async function downloadInstructorTimetablePdf(termId?: number): Promise<Blob> {
  const { data } = await apiClient.get('/instructor/my-timetable/pdf', {
    params: termId != null ? { termId } : undefined,
    responseType: 'blob',
  });
  return data;
}

export async function getSectionStudents(sectionId: number): Promise<SectionStudentsResponse> {
  const { data } = await apiClient.get(`/instructor/section/${sectionId}/students`);
  return data;
}

export async function getAcademicCv(): Promise<AcademicCvResponse> {
  const { data } = await apiClient.get('/instructor/academic-cv');
  return data;
}

export async function createAcademicCvActivity(dto: CreateAcademicCvActivityDto): Promise<AcademicCvResponse> {
  const { data } = await apiClient.post('/instructor/academic-cv', dto);
  return data;
}

export async function updateAcademicCvActivity(id: number, dto: CreateAcademicCvActivityDto): Promise<AcademicCvResponse> {
  const { data } = await apiClient.put(`/instructor/academic-cv/${id}`, dto);
  return data;
}
