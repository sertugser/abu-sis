import { useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  useVerifyNvi,
  useYoksisStatus,
  useSubmitYoksis,
  useFetchOsymPlacement,
  useUpdatePlacement,
  type IdentityVerificationResult,
  type YoksisStatusResult,
  type OsymPlacementResult,
  type UpdatePlacement,
} from '../../../hooks/useStudentCard';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';

const CARD_CLASS = 'bg-white border border-border rounded-xl p-4 space-y-3';
const CARD_TITLE = 'text-sm font-semibold text-foreground';
const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';
const FIELD_VALUE = 'text-sm text-foreground';

function formatValue(value: string | number | null): string {
  if (value === null || value === '') return '—';
  return String(value);
}

/** Small reusable "this is mock/test data" badge shown next to result headers. */
function MockBadge({ isMock }: { isMock: boolean }) {
  if (!isMock) return null;
  return (
    <Badge variant="warning" className="uppercase tracking-wide">
      Mock / Test Verisi
    </Badge>
  );
}

/** A single read-only labelled value within a result grid. */
function ResultField({ label, value }: { label: string; value: string | number | null }) {
  return (
    <div>
      <p className={FIELD_LABEL}>{label}</p>
      <p className={FIELD_VALUE}>{formatValue(value)}</p>
    </div>
  );
}

export default function IntegrationPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canIntegrate = hasPermission('menu.ogrenci-rehberi.integration');

  const verifyNvi = useVerifyNvi();
  const yoksisStatus = useYoksisStatus();
  const submitYoksis = useSubmitYoksis();
  const fetchOsym = useFetchOsymPlacement();
  const updatePlacement = useUpdatePlacement();

  const [nviResult, setNviResult] = useState<IdentityVerificationResult | null>(null);
  const [nviError, setNviError] = useState<string | null>(null);

  const [yoksisResult, setYoksisResult] = useState<YoksisStatusResult | null>(null);
  const [yoksisError, setYoksisError] = useState<string | null>(null);
  const [yoksisSuccess, setYoksisSuccess] = useState<string | null>(null);

  const [osymResult, setOsymResult] = useState<OsymPlacementResult | null>(null);
  const [osymError, setOsymError] = useState<string | null>(null);
  const [osymSaveSuccess, setOsymSaveSuccess] = useState<string | null>(null);

  if (!context) return null;
  const studentId = context.student.id;

  if (!canIntegrate) {
    return <p className="text-sm text-muted-foreground">Bu işlem için yetkiniz yok.</p>;
  }

  const handleVerifyNvi = async () => {
    setNviError(null);
    try {
      const result = await verifyNvi.mutateAsync({ studentId });
      setNviResult(result);
    } catch (err) {
      setNviError(err instanceof Error ? err.message : 'NVİ sorgusu yapılamadı.');
    }
  };

  const handleYoksisStatus = async () => {
    setYoksisError(null);
    setYoksisSuccess(null);
    try {
      const result = await yoksisStatus.mutateAsync({ studentId });
      setYoksisResult(result);
    } catch (err) {
      setYoksisError(err instanceof Error ? err.message : 'YÖKSİS durumu alınamadı.');
    }
  };

  const handleSubmitYoksis = async () => {
    setYoksisError(null);
    setYoksisSuccess(null);
    try {
      const result = await submitYoksis.mutateAsync({ studentId });
      setYoksisResult(result);
      setYoksisSuccess('YÖKSİS bildirimi yapıldı.');
    } catch (err) {
      setYoksisError(err instanceof Error ? err.message : 'YÖKSİS bildirimi yapılamadı.');
    }
  };

  const handleFetchOsym = async () => {
    setOsymError(null);
    setOsymSaveSuccess(null);
    try {
      const result = await fetchOsym.mutateAsync({ studentId });
      setOsymResult(result);
    } catch (err) {
      setOsymError(err instanceof Error ? err.message : 'ÖSYM yerleştirme sorgusu yapılamadı.');
    }
  };

  const handleSaveOsym = async () => {
    setOsymError(null);
    setOsymSaveSuccess(null);
    if (!osymResult) return;
    const dto: UpdatePlacement = {
      entranceType: osymResult.entranceType,
      scoreType: osymResult.scoreType,
      score: osymResult.score,
      rankOrder: osymResult.rankOrder,
      quotaType: osymResult.quotaType,
      placementYear: osymResult.placementYear,
      preferenceOrder: null,
      placedProgramName: osymResult.placedProgramName,
      note: null,
    };
    try {
      await updatePlacement.mutateAsync({ studentId, dto });
      setOsymSaveSuccess(
        'Yerleştirme bilgileri kaydedildi. Yerleştirme Bilgileri panelinde görüntülenebilir.',
      );
    } catch (err) {
      setOsymError(err instanceof Error ? err.message : 'Yerleştirme bilgileri kaydedilemedi.');
    }
  };

  return (
    <div className="max-w-2xl space-y-4">
      {/* ─── NVİ Kimlik Doğrulama ─── */}
      <section className={CARD_CLASS}>
        <h3 className={CARD_TITLE}>NVİ Kimlik Doğrulama</h3>
        <Button
          variant="outline"
          size="sm"
          onClick={() => void handleVerifyNvi()}
          disabled={verifyNvi.isPending}
        >
          {verifyNvi.isPending ? 'Sorgulanıyor...' : "NVİ'den Sorgula"}
        </Button>

        {nviError && <p className="text-sm text-destructive">{nviError}</p>}

        {nviResult && (
          <div className="space-y-3">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant={nviResult.matched ? 'success' : 'destructive'}>
                {nviResult.matched ? '✓ Eşleşti' : '✗ Eşleşmedi'}
              </Badge>
              <MockBadge isMock={nviResult.isMock} />
            </div>
            <div className="grid grid-cols-2 gap-x-6 gap-y-3">
              <ResultField label="Ad Soyad" value={nviResult.fullName} />
              <ResultField label="Doğum Tarihi" value={nviResult.birthDate} />
              <ResultField label="Doğum Yeri" value={nviResult.birthPlace} />
              <ResultField label="İl / İlçe" value={addressLine(nviResult)} />
            </div>
            {nviResult.message && (
              <p className="text-sm text-muted-foreground">{nviResult.message}</p>
            )}
          </div>
        )}
      </section>

      {/* ─── YÖKSİS Bildirim ─── */}
      <section className={CARD_CLASS}>
        <h3 className={CARD_TITLE}>YÖKSİS Bildirim</h3>
        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => void handleYoksisStatus()}
            disabled={yoksisStatus.isPending}
          >
            {yoksisStatus.isPending ? 'Sorgulanıyor...' : 'Durumu Sorgula'}
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => void handleSubmitYoksis()}
            disabled={submitYoksis.isPending}
          >
            {submitYoksis.isPending ? 'Gönderiliyor...' : "YÖKSİS'e Gönder"}
          </Button>
        </div>

        {yoksisError && <p className="text-sm text-destructive">{yoksisError}</p>}
        {yoksisSuccess && <p className="text-sm text-emerald-600">{yoksisSuccess}</p>}

        {yoksisResult && (
          <div className="space-y-3">
            <MockBadge isMock={yoksisResult.isMock} />
            <div className="grid grid-cols-2 gap-x-6 gap-y-3">
              <ResultField label="Durum" value={yoksisResult.status} />
              <ResultField label="Belge No" value={yoksisResult.yoksisRecordNo} />
            </div>
            {yoksisResult.message && (
              <p className="text-sm text-muted-foreground">{yoksisResult.message}</p>
            )}
          </div>
        )}
      </section>

      {/* ─── ÖSYM Yerleştirme Sorgula ─── */}
      <section className={CARD_CLASS}>
        <h3 className={CARD_TITLE}>ÖSYM Yerleştirme Sorgula</h3>
        <Button
          variant="outline"
          size="sm"
          onClick={() => void handleFetchOsym()}
          disabled={fetchOsym.isPending}
        >
          {fetchOsym.isPending ? 'Sorgulanıyor...' : 'Sorgula'}
        </Button>

        {osymError && <p className="text-sm text-destructive">{osymError}</p>}

        {osymResult && (
          <div className="space-y-3">
            <MockBadge isMock={osymResult.isMock} />
            <div className="grid grid-cols-2 gap-x-6 gap-y-3">
              <ResultField label="Giriş Türü" value={osymResult.entranceType} />
              <ResultField label="Puan Türü" value={osymResult.scoreType} />
              <ResultField label="Puan" value={osymResult.score} />
              <ResultField label="Sıra" value={osymResult.rankOrder} />
              <ResultField label="Kontenjan" value={osymResult.quotaType} />
              <ResultField label="Yıl" value={osymResult.placementYear} />
              <div className="col-span-2">
                <ResultField label="Program" value={osymResult.placedProgramName} />
              </div>
            </div>
            {osymResult.message && (
              <p className="text-sm text-muted-foreground">{osymResult.message}</p>
            )}
            <Button
              size="sm"
              onClick={() => void handleSaveOsym()}
              disabled={updatePlacement.isPending}
            >
              {updatePlacement.isPending ? 'Kaydediliyor...' : 'Yerleştirme Bilgilerine Kaydet'}
            </Button>
            {osymSaveSuccess && <p className="text-sm text-emerald-600">{osymSaveSuccess}</p>}
          </div>
        )}
      </section>
    </div>
  );
}

/** Compose "İl / İlçe" from the NVİ address parts, falling back to "—" when both are empty. */
function addressLine(result: IdentityVerificationResult): string | null {
  const parts = [result.addressProvince, result.addressDistrict].filter(
    (part): part is string => part != null && part !== '',
  );
  return parts.length === 0 ? null : parts.join(' / ');
}
