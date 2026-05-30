import { cn } from '@/lib/utils';
import type { RegistrationStudentHeader } from '@/types/registrationCatalog';

interface RegistrationHeaderBarProps {
  student: RegistrationStudentHeader;
}

interface InfoItemProps {
  label: string;
  value: React.ReactNode;
  emphasis?: 'default' | 'success' | 'danger';
}

/** Kutusuz inline metrik: küçük etiket + belirgin değer (tek yatay satırda). */
function InfoItem({ label, value, emphasis = 'default' }: InfoItemProps) {
  return (
    <div className="flex items-baseline gap-1.5">
      <span className="text-xs uppercase tracking-wide text-muted-foreground">{label}</span>
      <span
        className={cn(
          'text-base font-semibold tabular-nums',
          emphasis === 'success' && 'text-green-700',
          emphasis === 'danger' && 'text-destructive',
        )}
      >
        {value}
      </span>
    </div>
  );
}

export function RegistrationHeaderBar({ student }: RegistrationHeaderBarProps) {
  const isDebt = student.financialStatus !== 'Uygun';

  return (
    <section className="flex flex-col gap-4 bg-card px-5 py-4 md:flex-row md:items-center md:justify-between">
      {/* Kimlik — sol */}
      <div className="min-w-0">
        <h2 className="flex items-center gap-2 text-lg font-semibold leading-tight">
          <span className="truncate">{student.fullName}</span>
          <span className="font-mono text-base font-normal text-muted-foreground">
            {student.studentNumber}
          </span>
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          {student.programName} · {student.classYear}. Sınıf
          {student.curriculumName ? ` · ${student.curriculumName}` : ''}
        </p>
      </div>

      {/* Metrik şeridi — sağ üst, kutusuz tek yatay satır, dikey ayraçlarla */}
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2 divide-x divide-border md:justify-end md:shrink-0">
        <InfoItem label="GANO" value={student.cgpa.toFixed(2)} />
        <div className="pl-6"><InfoItem label="AKTS Limiti" value={`${student.ectsLimit}`} /></div>
        <div className="pl-6">
          <InfoItem
            label="Borç Durumu"
            value={student.financialStatus}
            emphasis={isDebt ? 'danger' : 'success'}
          />
        </div>
        <div className="pl-6"><InfoItem label="Öğrenim Süresi" value={student.studyDurationStatus} /></div>
      </div>
    </section>
  );
}
