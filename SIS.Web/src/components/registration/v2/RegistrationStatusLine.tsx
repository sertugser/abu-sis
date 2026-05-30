import { Badge } from '@/components/ui/badge';
import { computeRegistrationStatus } from '../../../lib/registrationStatus';
import type { SelectedCourse } from '../../../types/registrationCatalog';

interface Props {
  selectedCourses: SelectedCourse[];
}

export default function RegistrationStatusLine({ selectedCourses }: Props) {
  const view = computeRegistrationStatus(selectedCourses);

  return (
    <div className="flex flex-wrap items-center gap-x-8 gap-y-2 border-b bg-muted/40 px-4 py-3 text-sm">
      <div className="flex items-center gap-2">
        <span className="font-medium text-foreground">Öğrenci Kesinleştirme Durumu:</span>
        <Badge variant={view.finalizationVariant} className="text-xs px-2 py-0.5">{view.finalizationLabel}</Badge>
      </div>
      <div className="flex items-center gap-2">
        <span className="font-medium text-foreground">Danışman Onay Durumu:</span>
        <Badge variant={view.advisorVariant} className="text-xs px-2 py-0.5">{view.advisorLabel}</Badge>
      </div>
    </div>
  );
}
