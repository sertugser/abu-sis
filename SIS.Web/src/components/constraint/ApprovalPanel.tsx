import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { CheckCircle2, XCircle, AlertTriangle, Clock } from 'lucide-react';
import {
  usePendingExceptionRequests,
  useApproveExceptionRequest,
  useRejectExceptionRequest,
} from '../../hooks/useConstraints';
import ApprovalTimeline from './ApprovalTimeline';
import type { ExceptionRequestDto } from '../../types/constraint';

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Beklemede',
  ApprovedByManager: 'Bolum Muduru Onayladi',
  ApprovedByDean: 'Dekan Onayladi',
  ApprovedByAdmin: 'Tamamlandi',
  Rejected: 'Reddedildi',
  Expired: 'Suresi Doldu',
};

function EntityInfo({ request }: { request: ExceptionRequestDto }) {
  const parts: string[] = [];
  if (request.departmentName) parts.push(`Bolum: ${request.departmentName}`);
  if (request.instructorName) parts.push(`Ogretim Uyesi: ${request.instructorName}`);
  if (request.studentName) parts.push(`Ogrenci: ${request.studentName}`);
  if (request.courseSectionInfo) parts.push(`Sube: ${request.courseSectionInfo}`);
  if (request.termName) parts.push(`Donem: ${request.termName}`);
  if (parts.length === 0) return null;
  return <p className="text-xs text-muted-foreground">{parts.join(' | ')}</p>;
}

function RequestCard({ request }: { request: ExceptionRequestDto }) {
  const [comment, setComment] = useState('');
  const [showActions, setShowActions] = useState(false);
  const approveMutation = useApproveExceptionRequest();
  const rejectMutation = useRejectExceptionRequest();
  const isPending = approveMutation.isPending || rejectMutation.isPending;

  return (
    <Card className="border-amber-200 dark:border-amber-800">
      <CardContent className="p-4 space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <Badge variant="outline" className="text-[10px] shrink-0">
                {request.constraintCode}
              </Badge>
              <span className="text-sm font-medium truncate">{request.constraintName}</span>
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Talep eden: {request.requestedByName ?? request.requestedByUserId}
              {' '}&middot;{' '}
              {new Date(request.requestedAt).toLocaleString('tr-TR')}
            </p>
            <EntityInfo request={request} />
          </div>
          <Badge variant="secondary" className="text-[10px] shrink-0">
            {STATUS_LABELS[request.status] ?? request.status}
          </Badge>
        </div>

        <div className="bg-muted/50 rounded-md px-3 py-2">
          <p className="text-xs"><span className="font-medium">Neden:</span> {request.reason}</p>
          {request.isExempt && (
            <p className="text-[10px] text-amber-600 dark:text-amber-400 mt-1">
              <AlertTriangle className="inline h-3 w-3 mr-0.5" />
              Bu istisna kisitlamayi tamamen devre disi birakacak
            </p>
          )}
          {request.parameterOverride && (
            <p className="text-[10px] text-muted-foreground mt-1 font-mono">
              Parametre: {request.parameterOverride}
            </p>
          )}
          {request.expiresAt && (
            <p className="text-[10px] text-muted-foreground mt-1">
              <Clock className="inline h-3 w-3 mr-0.5" />
              Son gecerlilik: {new Date(request.expiresAt).toLocaleDateString('tr-TR')}
            </p>
          )}
        </div>

        <ApprovalTimeline steps={request.steps} />

        {!showActions ? (
          <Button variant="outline" size="sm" className="text-xs" onClick={() => setShowActions(true)}>
            Islem Yap
          </Button>
        ) : (
          <div className="space-y-2 border-t pt-2">
            <Textarea
              className="text-xs h-14"
              placeholder="Yorum (opsiyonel)..."
              value={comment}
              onChange={e => setComment(e.target.value)}
            />
            <div className="flex gap-2">
              <Button
                size="sm"
                className="text-xs bg-green-600 hover:bg-green-700 text-white"
                disabled={isPending}
                onClick={() => approveMutation.mutate(
                  { id: request.id, dto: { comment: comment || null } },
                  { onSuccess: () => { setComment(''); setShowActions(false); } },
                )}
              >
                <CheckCircle2 className="h-3.5 w-3.5 mr-1" />
                {approveMutation.isPending ? 'Onaylaniyor...' : 'Onayla'}
              </Button>
              <Button
                size="sm"
                variant="destructive"
                className="text-xs"
                disabled={isPending}
                onClick={() => rejectMutation.mutate(
                  { id: request.id, dto: { comment: comment || null } },
                  { onSuccess: () => { setComment(''); setShowActions(false); } },
                )}
              >
                <XCircle className="h-3.5 w-3.5 mr-1" />
                {rejectMutation.isPending ? 'Reddediliyor...' : 'Reddet'}
              </Button>
              <Button
                size="sm"
                variant="ghost"
                className="text-xs"
                onClick={() => setShowActions(false)}
              >
                Iptal
              </Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

export default function ApprovalPanel() {
  const { data: requests, isLoading } = usePendingExceptionRequests();

  if (isLoading) return <Skeleton className="h-24 rounded-xl" />;
  if (!requests || requests.length === 0) return null;

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <Clock className="h-4 w-4 text-amber-600 dark:text-amber-400" />
        <h2 className="text-sm font-semibold">Onay Bekleyen Talepler</h2>
        <Badge variant="warning" className="text-[10px]">{requests.length}</Badge>
      </div>
      <div className="space-y-2">
        {requests.map(r => <RequestCard key={r.id} request={r} />)}
      </div>
    </div>
  );
}
