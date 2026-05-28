import { Badge } from '@/components/ui/badge';
import { computeRegistrationStatus } from '../../../lib/registrationStatus';
import type { SelectedCourse } from '../../../types/registrationCatalog';

interface Props {
  selectedCourses: SelectedCourse[];
}

export default function RegistrationStatusLine({ selectedCourses }: Props) {
  const view = computeRegistrationStatus(selectedCourses);

  return (
    <div className="bg-muted/30 border-y border-border/40 px-3 py-1 flex flex-wrap items-center gap-4 text-[12px]">
      <div className="flex items-center gap-2">
        <span className="text-muted-foreground">Öğrenci Kesinleştirme Durumu:</span>
        <Badge variant={view.finalizationVariant} className="text-[10px] px-1.5 py-0 h-5">{view.finalizationLabel}</Badge>
      </div>
      <div className="flex items-center gap-2">
        <span className="text-muted-foreground">Danışman Onay Durumu:</span>
        <Badge variant={view.advisorVariant} className="text-[10px] px-1.5 py-0 h-5">{view.advisorLabel}</Badge>
      </div>
    </div>
  );
}
