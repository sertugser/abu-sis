import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { Badge } from '@/components/ui/badge';
import { ChevronDown, AlertTriangle, RotateCcw, CheckCircle2 } from 'lucide-react';
import { useState } from 'react';
import { cn } from '@/lib/utils';
import SectionRow from './SectionRow';
import type { StudentCourseDto } from '../../types/course';

interface CourseCardProps {
  course: StudentCourseDto;
  onAddToPlan: (sectionId: number) => void;
  isAdding: boolean;
  registeredSectionIds: number[];
}

export default function CourseCard({ course, onAddToPlan, isAdding, registeredSectionIds }: CourseCardProps) {
  const [isOpen, setIsOpen] = useState(false);

  const courseHasRegistration = course.sections.some(s => registeredSectionIds.includes(s.sectionId));
  const isAlttan = course.takeStatus === 'Alttan';
  const isPreviouslyTaken = course.takeStatus === 'OncedenAlindi';

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen}>
      <div className={cn(
        "rounded-xl border bg-card overflow-hidden transition-all duration-200 hover:shadow-md",
        isAlttan ? "border-destructive/40 bg-destructive/5" : "border-border",
        !course.prerequisitesMet && !isAlttan && "border-warning/40 bg-warning/5"
      )}>
        <CollapsibleTrigger asChild>
          <button className="w-full flex items-center justify-between p-4 hover:bg-muted/50 transition-colors text-left cursor-pointer">
            <div className="flex items-center gap-2.5 flex-wrap min-w-0">
              <span className="font-mono text-sm font-semibold text-primary">{course.code}</span>
              <span className="text-foreground font-medium">{course.name}</span>
              <Badge variant="default" className="text-[11px] font-mono">{course.ects} AKTS</Badge>

              {/* Müfredat tipi */}
              {course.curriculumType === 'Zorunlu' && (
                <Badge variant="accent" className="text-[11px]">Zorunlu</Badge>
              )}
              {course.curriculumType === 'Secmeli' && (
                <Badge variant="secondary" className="text-[11px]">Secmeli</Badge>
              )}

              {/* Alttan — kirmizi, zorunlu tekrar */}
              {isAlttan && (
                <Badge variant="destructive" className="text-[11px] gap-1">
                  <RotateCcw className="h-3 w-3" />
                  Alttan ({course.previousGrade})
                </Badge>
              )}

              {/* Onceden alinmis — onceki notu goster */}
              {isPreviouslyTaken && course.previousGrade && (
                <span className="inline-flex items-center gap-1 text-[11px] bg-muted px-2 py-0.5 rounded-md font-medium">
                  <CheckCircle2 className="h-3 w-3 text-muted-foreground" />
                  Onceki Not: <strong className="text-foreground">{course.previousGrade}</strong>
                </span>
              )}

              {/* Donem bilgisi */}
              {course.suggestedSemester > 0 && (
                <span className="text-[11px] text-muted-foreground">{course.suggestedSemester}. Donem</span>
              )}

              {/* Onkosul uyarisi */}
              {!course.prerequisitesMet && (
                <span className="flex items-center gap-1 text-[11px] text-warning font-medium">
                  <AlertTriangle className="h-3 w-3" />
                  Onkosul eksik
                </span>
              )}
            </div>
            <div className="flex items-center gap-2 shrink-0 ml-4">
              <span className="text-xs text-muted-foreground">{course.sections.length} sube</span>
              <ChevronDown className={cn(
                "h-4 w-4 text-muted-foreground transition-transform duration-200",
                isOpen && "rotate-180"
              )} />
            </div>
          </button>
        </CollapsibleTrigger>

        <CollapsibleContent>
          <div className="px-4 pb-4 space-y-2 border-t border-border pt-3">
            {/* Alttan uyarisi */}
            {isAlttan && (
              <div className="text-xs text-destructive bg-destructive/10 rounded-lg px-3 py-2">
                Bu dersten <strong>{course.previousGrade}</strong> aldiniz. Tekrar almaniz zorunludur.
              </div>
            )}

            {/* Onceden alinmis bilgilendirme */}
            {isPreviouslyTaken && (
              <div className="text-xs text-muted-foreground bg-muted/50 rounded-lg px-3 py-2">
                Bu dersi daha once <strong>{course.previousGrade}</strong> notu ile gecmissiniz. Notunuzu yukseltmek isterseniz tekrar alabilirsiniz.
              </div>
            )}

            {/* Onkosul uyarisi */}
            {!course.prerequisitesMet && (
              <div className="text-xs text-warning bg-warning/10 rounded-lg px-3 py-2">
                Bu dersin onkosul(lar)i karsilanmamistir. Kayit sirasinda reddedilebilir.
              </div>
            )}

            {course.sections.map(section => (
              <SectionRow
                key={section.sectionId}
                section={section}
                onAddToPlan={onAddToPlan}
                isAdding={isAdding}
                isInPlan={registeredSectionIds.includes(section.sectionId)}
                isCourseRegistered={courseHasRegistration && !registeredSectionIds.includes(section.sectionId)}
                prerequisitesMet={course.prerequisitesMet}
              />
            ))}
          </div>
        </CollapsibleContent>
      </div>
    </Collapsible>
  );
}
