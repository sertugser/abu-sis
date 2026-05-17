import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { ChevronRight, AlertTriangle, RotateCcw, CheckCircle2 } from 'lucide-react';
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import SectionRow from './SectionRow';
import type { StudentCourseDto } from '../../types/course';

interface CourseCardProps {
  course: StudentCourseDto;
  onAddToPlan: (sectionId: number) => void;
  isAdding: boolean;
  registeredSectionIds: number[];
  registrationActionsOpen: boolean;
}

export default function CourseCard({
  course,
  onAddToPlan,
  isAdding,
  registeredSectionIds,
  registrationActionsOpen,
}: CourseCardProps) {
  const [isOpen, setIsOpen] = useState(false);

  const courseHasRegistration = course.sections.some(s => registeredSectionIds.includes(s.sectionId));
  const isAlttan = course.takeStatus === 'Alttan';
  const isPreviouslyTaken = course.takeStatus === 'OncedenAlindi';

  const handleAddToPlanFromDialog = (sectionId: number) => {
    onAddToPlan(sectionId);
    // Plana eklenince modal kapansın — kullanıcı sonucu üst sayfada görsün
    setIsOpen(false);
  };

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        className={cn(
          'w-full flex items-center justify-between gap-4 rounded-md border bg-card p-4 text-left transition-all duration-200 hover:shadow-sm hover:bg-muted/30',
          isAlttan ? 'border-destructive/40 bg-destructive/5 hover:bg-destructive/10' : 'border-border/70',
          !course.prerequisitesMet && !isAlttan && 'border-warning/40 bg-warning/5 hover:bg-warning/10'
        )}
      >
        <div className="flex flex-wrap items-center gap-2.5 min-w-0">
          <span className="font-mono text-sm font-semibold text-primary">{course.code}</span>
          <span className="text-foreground font-medium">{course.name}</span>
          <Badge variant="default" className="text-[11px] font-mono">{course.ects} AKTS</Badge>

          {course.curriculumType === 'Zorunlu' && (
            <Badge variant="accent" className="text-[11px]">Zorunlu</Badge>
          )}
          {course.curriculumType === 'Secmeli' && (
            <Badge variant="secondary" className="text-[11px]">Seçmeli</Badge>
          )}

          {isAlttan && (
            <Badge variant="destructive" className="text-[11px] gap-1">
              <RotateCcw className="h-3 w-3" />
              Alttan ({course.previousGrade})
            </Badge>
          )}

          {isPreviouslyTaken && course.previousGrade && (
            <span className="inline-flex items-center gap-1 text-[11px] bg-muted px-2 py-0.5 rounded-md font-medium">
              <CheckCircle2 className="h-3 w-3 text-muted-foreground" />
              Önceki Not: <strong className="text-foreground">{course.previousGrade}</strong>
            </span>
          )}

          {course.suggestedSemester > 0 && (
            <span className="text-[11px] text-muted-foreground">{course.suggestedSemester}. Dönem</span>
          )}

          {!course.prerequisitesMet && (
            <span className="flex items-center gap-1 text-[11px] text-warning font-medium">
              <AlertTriangle className="h-3 w-3" />
              Önkoşul eksik
            </span>
          )}
        </div>

        <div className="flex items-center gap-2 shrink-0 ml-4">
          <span className="text-xs text-muted-foreground">{course.sections.length} şube</span>
          <ChevronRight className="h-4 w-4 text-muted-foreground" />
        </div>
      </button>

      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent className="max-w-3xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="flex flex-wrap items-center gap-2">
              <span className="font-mono text-primary">{course.code}</span>
              <span className="text-foreground">{course.name}</span>
              <Badge variant="default" className="font-mono">{course.ects} AKTS</Badge>
            </DialogTitle>
            <DialogDescription>
              {course.sections.length} şube · Şubeden birini seçip plana ekleyebilirsin.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            {isAlttan && (
              <div className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
                Bu dersten <strong>{course.previousGrade}</strong> aldınız. Tekrar almanız zorunludur.
              </div>
            )}

            {isPreviouslyTaken && (
              <div className="rounded-md bg-muted/50 px-3 py-2 text-sm text-muted-foreground">
                Bu dersi daha önce <strong>{course.previousGrade}</strong> notu ile geçmişsiniz. Notunuzu yükseltmek isterseniz tekrar alabilirsiniz.
              </div>
            )}

            {!course.prerequisitesMet && (
              <div className="rounded-md bg-warning/10 px-3 py-2 text-sm text-warning">
                Bu dersin önkoşul(lar)ı karşılanmamıştır. Kayıt sırasında reddedilebilir.
              </div>
            )}

            <div className="space-y-2">
              {course.sections.map(section => (
                <SectionRow
                  key={section.sectionId}
                  section={section}
                  onAddToPlan={handleAddToPlanFromDialog}
                  isAdding={isAdding}
                  isInPlan={registeredSectionIds.includes(section.sectionId)}
                  isCourseRegistered={courseHasRegistration && !registeredSectionIds.includes(section.sectionId)}
                  prerequisitesMet={course.prerequisitesMet}
                  registrationActionsOpen={registrationActionsOpen}
                />
              ))}
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
