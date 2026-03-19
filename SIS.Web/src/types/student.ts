export interface StudentInfoDto {
  id: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  cgpa: number;
  sgpa: number;
  completedSemesters: number;
  status: string;
  isScholarshipStudent: boolean;
  hasFinancialBlock: boolean;
  programName: string;
  departmentName: string;
  facultyName: string;
  withdrawalCount: number;
  programId: number;
}
