import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertTriangle, XCircle } from 'lucide-react';
import type { ValidationError } from '../../types/registration';

interface ValidationErrorsProps {
  errors: ValidationError[];
}

export default function ValidationErrors({ errors }: ValidationErrorsProps) {
  if (!errors.length) return null;

  const blockingErrors = errors.filter(e => e.rule !== 'ScheduleConflict_NeedsApproval');
  const warningErrors = errors.filter(e => e.rule === 'ScheduleConflict_NeedsApproval');

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
            <span className="font-semibold">Onay Gerekli: </span>
            {error.message}
          </AlertDescription>
        </Alert>
      ))}
    </div>
  );
}
