import { useMemo } from 'react';
import type { DepartmentSectionClassList } from '@/types/academicOperations';
import type { CourseGroup } from '../types';

export function useCourseGroups(sections: DepartmentSectionClassList[]): CourseGroup[] {
  return useMemo(() => {
    const groups = new Map<string, CourseGroup>();
    for (const section of sections) {
      const key = section.courseCode;
      let group = groups.get(key);
      if (!group) {
        group = {
          courseId: section.courseId,
          courseCode: section.courseCode,
          courseName: section.courseName,
          sections: [],
          totalStudents: 0,
          uniqueInstructorCount: 0,
        };
        groups.set(key, group);
      }
      group.sections.push(section);
      group.totalStudents += section.activeStudentCount;
    }

    for (const group of groups.values()) {
      const instructors = new Set(group.sections.map(s => s.instructorName).filter(Boolean));
      group.uniqueInstructorCount = instructors.size;
      group.sections.sort((a, b) => a.sectionNumber - b.sectionNumber);
    }

    return Array.from(groups.values()).sort((a, b) => a.courseCode.localeCompare(b.courseCode));
  }, [sections]);
}
