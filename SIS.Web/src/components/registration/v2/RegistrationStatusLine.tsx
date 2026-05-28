import { Badge } from '@/components/ui/badge';
import { computeRegistrationStatus } from '../../../lib/registrationStatus';
import type { SelectedCourse } from '../../../types/registrationCatalog';

interface Props {
  selectedCourses: SelectedCourse[];
}

export default function RegistrationStatusLine({ selectedCourses }: Props) {
  const view = computeRegistrationStatus(selectedCourses);

  return (
    <div className="flex flex-wrap items-center gap-4 py-2 text-sm">
      <div className="flex items-center gap-2">
        <span className="text-muted-foreground">Öğrenci Kesinleştirme Durumu:</span>
        <Badge variant={view.finalizationVariant}>{view.finalizationLabel}</Badge>
      </div>
      <div className="flex items-center gap-2">
        <span className="text-muted-foreground">Danışman Onay Durumu:</span>
        <Badge variant={view.advisorVariant}>{view.advisorLabel}</Badge>
      </div>
    </div>
  );
}
