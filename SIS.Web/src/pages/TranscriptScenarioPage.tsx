import { TranscriptScenarioView } from '../components/scenario/TranscriptScenarioView';
import { useCurriculumStatus } from '../hooks/useCurriculumStatus';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useStudentTranscript } from '../hooks/useStudentTranscript';
import { calculateTranscriptScenario } from '../api/student.api';

export default function TranscriptScenarioPage() {
  const transcriptQuery = useStudentTranscript();
  const studentQuery = useStudentInfo();
  const curriculumStatusQuery = useCurriculumStatus();

  const student = studentQuery.data;
  // Danışman adı yalnızca öğrenci görünümünde gösterilir (transkript DTO'sunda yok).
  const advisorName = student
    ? student.advisorName
      ? `${student.advisorTitle ? `${student.advisorTitle} ` : ''}${student.advisorName}`.trim()
      : 'Atanmamış'
    : undefined;

  return (
    <TranscriptScenarioView
      studentName={transcriptQuery.data?.fullName ?? ''}
      transcript={transcriptQuery.data}
      curriculumStatus={curriculumStatusQuery.data}
      isLoading={transcriptQuery.isLoading || studentQuery.isLoading}
      isError={transcriptQuery.isError || studentQuery.isError || !student}
      calculate={calculateTranscriptScenario}
      advisorName={advisorName}
      isCurriculumLoading={curriculumStatusQuery.isLoading}
    />
  );
}
