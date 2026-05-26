import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertTriangle, ShieldAlert } from 'lucide-react';
import { useAuditLogAnomalies } from '../../hooks/useAuditLog';

export function AnomalyBanner() {
  const { data: anomalies } = useAuditLogAnomalies();

  if (!anomalies || anomalies.length === 0) return null;

  return (
    <div className="space-y-2">
      {anomalies.map((anomaly, i) => (
        <Alert key={`${anomaly.type}-${anomaly.message}-${i}`} variant={anomaly.severity === 'critical' ? 'destructive' : 'default'} className="py-2">
          {anomaly.severity === 'critical'
            ? <ShieldAlert className="h-4 w-4" />
            : <AlertTriangle className="h-4 w-4" />
          }
          <AlertDescription className="text-sm">
            <span className="font-medium">{anomaly.message}</span>
            <span className="text-muted-foreground ml-2">({anomaly.count} olay)</span>
          </AlertDescription>
        </Alert>
      ))}
    </div>
  );
}
