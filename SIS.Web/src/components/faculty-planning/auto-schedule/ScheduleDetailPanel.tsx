import type { AutoSchedulePreviewItem, AutoScheduleFailure, AutoSchedulePreview } from '../../../types/facultyPlanning';
import { EmptyState } from './EmptyState';
import { SuccessDetail } from './SuccessDetail';
import { FailureDetail } from './FailureDetail';

interface ScheduleDetailPanelProps {
  selectedSuccess: AutoSchedulePreviewItem | undefined;
  selectedFailure: AutoScheduleFailure | undefined;
  isChecked: boolean;
  onToggleCheck: () => void;
  onNavigate: (target: string) => void;
  unscheduledCount: number;
  availableClassroomCount: number | undefined;
  hasPreviewData: boolean;
  previewData: AutoSchedulePreview | undefined;
}

export function ScheduleDetailPanel({
  selectedSuccess,
  selectedFailure,
  isChecked,
  onToggleCheck,
  onNavigate,
  unscheduledCount,
  availableClassroomCount,
  hasPreviewData,
  previewData,
}: ScheduleDetailPanelProps) {
  if (selectedSuccess) {
    return <SuccessDetail item={selectedSuccess} isChecked={isChecked} onToggleCheck={onToggleCheck} />;
  }

  if (selectedFailure) {
    return <FailureDetail item={selectedFailure} onNavigate={onNavigate} />;
  }

  if (!hasPreviewData) {
    return <EmptyState unscheduledCount={unscheduledCount} availableClassroomCount={availableClassroomCount} />;
  }

  return (
    <div className="flex items-center justify-center h-full">
      <p className="text-sm text-muted-foreground">Detay görmek için sol listeden bir şube seçin.</p>
    </div>
  );
}
