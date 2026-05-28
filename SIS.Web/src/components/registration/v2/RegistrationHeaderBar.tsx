import { GraduationCap } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
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
      <span className="text-sm font-medium text-foreground leading-tight truncate">{value}</span>
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
    <Card>
      <CardContent className="p-4">
        {/* Top row: identity left, CGPA right */}
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-3 min-w-0">
            <div className="flex-shrink-0 flex h-10 w-10 items-center justify-center rounded-full bg-primary/10 text-primary">
              <GraduationCap className="h-5 w-5" />
            </div>
            <div className="min-w-0">
              <h2 className="text-base font-semibold leading-tight truncate">{student.fullName}</h2>
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
                'text-2xl font-bold tabular-nums leading-none',
                cgpaHigh ? 'text-green-600' : 'text-destructive',
              )}
            >
              {student.cgpa.toFixed(2)}
            </span>
          </div>
        </div>

        {/* Info grid */}
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-x-6 gap-y-2 pt-4 border-t mt-3">
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
                className="mt-0.5 text-[11px] px-2 py-0"
              >
                {student.financialStatus}
              </Badge>
            }
          />
        </div>
      </CardContent>
    </Card>
  );
}
