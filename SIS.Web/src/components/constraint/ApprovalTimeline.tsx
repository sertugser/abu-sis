import { Check, X, Clock, Circle } from 'lucide-react';
import {
  Tooltip, TooltipContent, TooltipTrigger,
} from '@/components/ui/tooltip';
import type { ApprovalStepDto } from '../../types/constraint';

const ROLE_LABELS: Record<string, string> = {
  DepartmentManager: 'Bolum Muduru',
  Dean: 'Dekan',
  SystemAdministrator: 'Sistem Yoneticisi',
};

function StepIcon({ status }: { status: string }) {
  if (status === 'Approved') return <Check className="h-3.5 w-3.5 text-white" />;
  if (status === 'Rejected') return <X className="h-3.5 w-3.5 text-white" />;
  return <Clock className="h-3.5 w-3.5 text-white" />;
}

function stepBg(status: string, isActive: boolean) {
  if (status === 'Approved') return 'bg-green-500';
  if (status === 'Rejected') return 'bg-red-500';
  if (isActive) return 'bg-amber-500';
  return 'bg-muted-foreground/30';
}

function lineBg(status: string) {
  if (status === 'Approved') return 'bg-green-500';
  if (status === 'Rejected') return 'bg-red-500';
  return 'bg-border';
}

export default function ApprovalTimeline({ steps }: { steps: ApprovalStepDto[] }) {
  const sorted = [...steps].sort((a, b) => a.stepOrder - b.stepOrder);
  const firstPendingOrder = sorted.find(s => s.status === 'Pending')?.stepOrder ?? -1;

  return (
    <div className="flex items-center gap-0">
      {sorted.map((step, idx) => {
        const isActive = step.status === 'Pending' && step.stepOrder === firstPendingOrder;
        const bg = stepBg(step.status, isActive);

        return (
          <div key={step.id} className="flex items-center">
            {idx > 0 && (
              <div className={`h-0.5 w-6 sm:w-10 ${lineBg(sorted[idx - 1].status)}`} />
            )}
            <Tooltip>
              <TooltipTrigger asChild>
                <div className={`flex items-center justify-center h-7 w-7 rounded-full ${bg} shrink-0 cursor-help`}>
                  {step.status === 'Pending' && !isActive
                    ? <Circle className="h-3 w-3 text-white/60" />
                    : <StepIcon status={step.status} />
                  }
                </div>
              </TooltipTrigger>
              <TooltipContent side="bottom" className="max-w-xs">
                <p className="text-xs font-semibold">{ROLE_LABELS[step.requiredRole] ?? step.requiredRole}</p>
                <p className="text-xs text-muted-foreground">
                  {step.status === 'Approved' && `Onaylayan: ${step.decidedByName ?? step.decidedByUserId}`}
                  {step.status === 'Rejected' && `Reddeden: ${step.decidedByName ?? step.decidedByUserId}`}
                  {step.status === 'Pending' && isActive && 'Onay bekliyor'}
                  {step.status === 'Pending' && !isActive && 'Sirada bekliyor'}
                </p>
                {step.comment && (
                  <p className="text-xs mt-1 italic">"{step.comment}"</p>
                )}
                {step.decidedAt && (
                  <p className="text-[10px] text-muted-foreground mt-0.5">
                    {new Date(step.decidedAt).toLocaleString('tr-TR')}
                  </p>
                )}
              </TooltipContent>
            </Tooltip>
            <span className="text-[10px] text-muted-foreground ml-1.5 hidden sm:inline">
              {ROLE_LABELS[step.requiredRole] ?? step.requiredRole}
            </span>
          </div>
        );
      })}
    </div>
  );
}
