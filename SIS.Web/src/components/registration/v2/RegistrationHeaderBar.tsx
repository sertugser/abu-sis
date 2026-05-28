import { GraduationCap } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import type { RegistrationStudentHeader } from '@/types/registrationCatalog';

interface InfoRowProps {
  label: string;
  value: React.ReactNode;
}

function InfoRow({ label, value }: InfoRowProps) {
  return (
    <div className="flex flex-col gap-0.5 min-w-0">
      <span className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground leading-none">
        {label}
      </span>
      <span className="text-xs font-medium text-foreground leading-tight truncate">{value}</span>
    </div>
  );
}

interface RegistrationHeaderBarProps {
  student: RegistrationStudentHeader;
}

export function RegistrationHeaderBar({ student }: RegistrationHeaderBarProps) {
  const isDebt = student.financialStatus !== 'Uygun';
  const cgpaHigh = student.cgpa >= 2.0;

  return (
    <section className="bg-muted/30 border-b border-border/40 px-4 py-3">
      {/* Top row: identity left, CGPA right */}
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-2 min-w-0">
          <div className="flex-shrink-0 flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-primary">
            <GraduationCap className="h-4 w-4" />
          </div>
          <div className="min-w-0">
            <h2 className="text-sm font-semibold leading-tight truncate">{student.fullName}</h2>
            <p className="text-xs text-muted-foreground truncate">
              {student.studentNumber} &middot; {student.programName}
            </p>
          </div>
        </div>

        {/* CGPA */}
        <div className="flex-shrink-0 text-right">
          <span className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground block leading-none mb-0.5">
            GANO
          </span>
          <span
            className={cn(
              'text-xl font-bold tabular-nums leading-none',
              cgpaHigh ? 'text-green-600' : 'text-destructive',
            )}
          >
            {student.cgpa.toFixed(2)}
          </span>
        </div>
      </div>

      {/* Info grid */}
      <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-x-6 gap-y-1.5 pt-2 mt-2 border-t border-border/30">
        <InfoRow
          label="Sınıf / Dönem"
          value={`${student.classYear}. Sınıf / ${student.completedSemesters}. Dönem`}
        />
        <InfoRow label="Müfredat" value={student.curriculumName} />
        <InfoRow label="Öğrenim Süresi" value={student.studyDurationStatus} />
        <InfoRow label="AKTS Limiti" value={`${student.ectsLimit} AKTS`} />
        <InfoRow
          label="Borç Durumu"
          value={
            <Badge
              variant={isDebt ? 'destructive' : 'success'}
              aria-label={`Borç durumu: ${student.financialStatus}`}
              className="mt-0.5 text-[10px] px-1.5 py-0"
            >
              {student.financialStatus}
            </Badge>
          }
        />
      </div>
    </section>
  );
}
