import { useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Check, X } from 'lucide-react';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  useStudentPromotionStatus,
  usePromoteStudent,
  useRollbackPromotion,
} from '../../../hooks/useStudentCard';
import { Button } from '@/components/ui/button';
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogFooter,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogAction,
  AlertDialogCancel,
} from '@/components/ui/alert-dialog';

const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';
const SELECT_CLASS =
  'mt-1 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60';

type ConfirmKind = 'rule' | 'manual' | 'rollback' | null;

export default function PromotionPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const studentId = context?.student.id ?? null;

  const { data: status, isLoading } = useStudentPromotionStatus(studentId);
  const promote = usePromoteStudent();
  const rollback = useRollbackPromotion();

  const [manualLevel, setManualLevel] = useState<number>(1);
  const [confirm, setConfirm] = useState<ConfirmKind>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  if (!context) return null;
  const { student } = context;

  const canPromote = hasPermission('menu.sinif-atlatma-islemleri.promote');
  const canRollback = hasPermission('menu.sinif-atlatma-islemleri.rollback');

  const resetMessages = () => {
    setError(null);
    setSuccess(null);
  };

  const runPromoteRule = async () => {
    resetMessages();
    try {
      await promote.mutateAsync({ studentId: student.id });
      setSuccess('Öğrenci bir üst sınıfa atlatıldı.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sınıf atlatma işlemi başarısız oldu.');
    } finally {
      setConfirm(null);
    }
  };

  const runPromoteManual = async () => {
    resetMessages();
    try {
      await promote.mutateAsync({ studentId: student.id, targetLevel: manualLevel });
      setSuccess(`Öğrenci sınıfı manuel olarak ${manualLevel}. sınıfa ayarlandı.`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Manuel seviye ayarlama başarısız oldu.');
    } finally {
      setConfirm(null);
    }
  };

  const runRollback = async () => {
    resetMessages();
    try {
      await rollback.mutateAsync({ studentId: student.id });
      setSuccess('Öğrenci bir alt sınıfa düşürüldü.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sınıf düşürme işlemi başarısız oldu.');
    } finally {
      setConfirm(null);
    }
  };

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Yükleniyor...</p>;
  }

  if (!status) {
    return <p className="text-sm text-muted-foreground">Sınıf bilgisi bulunamadı.</p>;
  }

  const levelOptions = Array.from({ length: status.normalDurationYears }, (_, i) => i + 1);
  const atFirstLevel = status.currentLevel <= 1;

  return (
    <div className="max-w-2xl space-y-8">
      {/* Mevcut Durum */}
      <section className="space-y-3">
        <h3 className="text-sm font-semibold">Mevcut Durum</h3>
        <div className="grid grid-cols-2 gap-x-6 gap-y-4">
          <div>
            <div className={FIELD_LABEL}>Mevcut Sınıf</div>
            <p className="mt-1 text-sm">{status.currentLevel}. sınıf</p>
          </div>
          <div>
            <div className={FIELD_LABEL}>Program Normal Süre</div>
            <p className="mt-1 text-sm">{status.normalDurationYears} yıl</p>
          </div>
        </div>
      </section>

      {/* Uygunluk */}
      <section className="space-y-3">
        <h3 className="text-sm font-semibold">Uygunluk</h3>
        {status.canPromote ? (
          <p className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
            Sınıf atlatmaya uygun (→ {status.eligibleTargetLevel}. sınıf)
          </p>
        ) : (
          <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-700">
            Uygun değil{status.reason ? ` — ${status.reason}` : ''}
          </p>
        )}

        {status.criteria.length > 0 && (
          <ul className="space-y-2">
            {status.criteria.map((c, idx) => (
              <li key={`${c.name}-${idx}`} className="flex items-start gap-2 text-sm">
                {c.passed ? (
                  <Check className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
                ) : (
                  <X className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
                )}
                <span>
                  <span className="font-medium">{c.name}</span>
                  {c.detail ? <span className="text-muted-foreground"> — {c.detail}</span> : null}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {success && <p className="text-sm text-emerald-600">{success}</p>}

      {/* Aksiyonlar */}
      {(canPromote || canRollback) && (
        <section className="space-y-4">
          <h3 className="text-sm font-semibold">Aksiyonlar</h3>

          {canPromote && (
            <div className="space-y-2">
              <Button
                onClick={() => {
                  resetMessages();
                  setConfirm('rule');
                }}
                disabled={!status.canPromote || promote.isPending}
              >
                Kurallara Göre Atlat
              </Button>
            </div>
          )}

          {canPromote && (
            <div className="space-y-2">
              <label className={FIELD_LABEL} htmlFor="field-manual-level">
                Manuel Seviye Ayarla
              </label>
              <div className="flex items-end gap-3">
                <select
                  id="field-manual-level"
                  className={`${SELECT_CLASS} max-w-[10rem]`}
                  value={manualLevel}
                  onChange={(e) => setManualLevel(Number(e.target.value))}
                >
                  {levelOptions.map((lvl) => (
                    <option key={lvl} value={lvl}>
                      {lvl}. sınıf
                    </option>
                  ))}
                </select>
                <Button
                  variant="secondary"
                  onClick={() => {
                    resetMessages();
                    setConfirm('manual');
                  }}
                  disabled={promote.isPending}
                >
                  Uygula
                </Button>
              </div>
            </div>
          )}

          {canRollback && (
            <div className="space-y-2">
              <Button
                variant="outline"
                onClick={() => {
                  resetMessages();
                  setConfirm('rollback');
                }}
                disabled={atFirstLevel || rollback.isPending}
              >
                Bir Sınıf Düşür
              </Button>
            </div>
          )}
        </section>
      )}

      <AlertDialog open={confirm !== null} onOpenChange={(open) => !open && setConfirm(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {confirm === 'rollback' ? 'Sınıf düşürme onayı' : 'Sınıf atlatma onayı'}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {confirm === 'rule' &&
                `Öğrenciyi kurallara göre ${
                  status.eligibleTargetLevel ?? status.currentLevel
                }. sınıfa atlatmak üzeresiniz. Onaylıyor musunuz?`}
              {confirm === 'manual' &&
                `Öğrenci sınıfını manuel olarak ${manualLevel}. sınıfa ayarlamak üzeresiniz. ` +
                  'Bu işlem sınıf atlatma kurallarını atlar (bypass eder). Onaylıyor musunuz?'}
              {confirm === 'rollback' &&
                'Öğrenciyi bir alt sınıfa düşürmek üzeresiniz. Onaylıyor musunuz?'}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (confirm === 'rule') void runPromoteRule();
                else if (confirm === 'manual') void runPromoteManual();
                else if (confirm === 'rollback') void runRollback();
              }}
            >
              Onayla
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
