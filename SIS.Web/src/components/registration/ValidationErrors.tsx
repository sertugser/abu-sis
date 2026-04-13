import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertTriangle, XCircle } from 'lucide-react';
import type { ValidationError } from '../../types/registration';

interface ValidationErrorsProps {
  errors?: ValidationError[];
  warnings?: ValidationError[];
}

export default function ValidationErrors({ errors = [], warnings = [] }: ValidationErrorsProps) {
  if (!errors.length && !warnings.length) return null;

  const blockingErrors = errors.filter(e => e.rule !== 'ScheduleConflict_NeedsApproval');
  const warningErrors = [
    ...errors.filter(e => e.rule === 'ScheduleConflict_NeedsApproval'),
    ...warnings,
  ];

  return (
    <div className="space-y-3">
      {blockingErrors.map((error, i) => (
        <Alert key={`block-${i}`} variant="destructive">
          <XCircle className="h-4 w-4" />
          <AlertDescription>
            <span className="font-semibold">Engel: </span>
            {error.message}
          </AlertDescription>
        </Alert>
      ))}
      {warningErrors.map((error, i) => (
        <Alert key={`warn-${i}`} variant="warning">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            <span className="font-semibold">Uyari: </span>
            {error.message}
          </AlertDescription>
        </Alert>
      ))}
    </div>
  );
}
