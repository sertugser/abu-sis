export interface ImportRowDto {
  rowNumber: number;
  courseCode: string;
  sectionCode: string;
  instructorName: string;
  day: string;
  startTime: string;
  endTime: string;
  classroomName: string;
  capacity: number;
  status: 'Valid' | 'Warning' | 'Error' | 'Skipped';
  message: string;
}

export interface ImportPreviewDto {
  importSessionId: string;
  expiresAt: string;
  totalRows: number;
  rows: ImportRowDto[];
}

export interface ImportResultDto {
  appliedCount: number;
  failedCount: number;
  failures: { rowNumber: number; courseCode: string; reason: string }[];
}
