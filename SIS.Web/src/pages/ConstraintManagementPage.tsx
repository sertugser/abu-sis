import { useState, useMemo, useEffect } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { AlertCircle, Pencil } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useToast } from '@/components/ui/global-toast';
import {
  useConstraints,
  useToggleConstraint,
  useUpdateConstraintCategory,
  useUpdateConstraintWeight,
} from '../hooks/useConstraints';
import { useAdminTerms } from '../hooks/useAdminTerms';
import {
  CATEGORY_LABELS, CONSTRAINT_GROUPS, PARAMETER_SCHEMAS, getCategoryCapability,
} from '../components/constraint/helpers';
import { useUpdateConstraintParameters } from '../hooks/useConstraints';
import { Input } from '@/components/ui/input';
import DistributionConstraintPanel from '../components/constraint/DistributionConstraintPanel';
import AlgorithmParametersPanel from '../components/constraint/AlgorithmParametersPanel';
import ApprovalPanel from '../components/constraint/ApprovalPanel';
import type { ConstraintDefinitionDto } from '../types/constraint';

// ─── Constraint Row ─────────────────────────────────────────────
// Grup adına göre renkli badge (Proliz tarzı sol-sütun etiketi).
const GROUP_BADGE_CLASS: Record<string, string> = {
  classroom: 'bg-blue-100 text-blue-800 border-blue-200',
  instructor: 'bg-indigo-100 text-indigo-800 border-indigo-200',
  curriculum: 'bg-emerald-100 text-emerald-800 border-emerald-200',
  time: 'bg-orange-100 text-orange-800 border-orange-200',
};

function ConstraintRow({
  constraint,
  groupLabel,
  groupKey,
  isAdmin,
}: {
  constraint: ConstraintDefinitionDto;
  groupLabel: string;
  groupKey: string;
  isAdmin: boolean;
}) {
  const [editOpen, setEditOpen] = useState(false);

  const categoryBadgeClass = constraint.category === 'Hard'
    ? 'bg-red-600 text-white border-red-600 dark:bg-red-700 dark:border-red-700'
    : 'bg-amber-400 text-white border-amber-400 dark:bg-amber-500 dark:border-amber-500';
  const groupClass = GROUP_BADGE_CLASS[groupKey] ?? 'bg-slate-100 text-slate-800 border-slate-200';

  const statusBadgeClass = constraint.isEnabled
    ? 'bg-green-600 text-white border-green-600 dark:bg-green-700 dark:border-green-700'
    : 'bg-gray-400 text-white border-gray-400 dark:bg-gray-600 dark:border-gray-600';

  return (
    <>
      <TableRow className={!constraint.isEnabled ? 'opacity-70' : undefined}>
        <TableCell className="w-44">
          <Badge variant="outline" className={`text-xs ${groupClass}`}>{groupLabel}</Badge>
        </TableCell>
        <TableCell>
          <div className="space-y-1">
            <p className="text-base font-medium leading-tight">{constraint.name}</p>
            {constraint.description && (
              <p className="text-sm text-muted-foreground leading-snug">{constraint.description}</p>
            )}
          </div>
        </TableCell>
        <TableCell className="w-28">
          <Badge variant="outline" className={`text-xs ${statusBadgeClass}`}>
            {constraint.isEnabled ? 'Aktif' : 'Pasif'}
          </Badge>
        </TableCell>
        <TableCell className="w-32">
          <Badge variant="outline" className={`text-xs ${categoryBadgeClass}`}>
            {CATEGORY_LABELS[constraint.category]}
          </Badge>
        </TableCell>
        <TableCell className="w-28 text-sm tabular-nums text-muted-foreground">
          {constraint.category === 'Soft' ? (
            <span>Ağırlık <strong className="text-foreground">{constraint.weight}</strong></span>
          ) : (
            <span className="text-muted-foreground/60">—</span>
          )}
        </TableCell>
        <TableCell className="w-24 text-right">
          <Button
            size="icon"
            variant="ghost"
            className="h-8 w-8"
            onClick={() => setEditOpen(true)}
            disabled={!isAdmin}
            aria-label="Düzenle"
          >
            <Pencil className="h-4 w-4" />
          </Button>
        </TableCell>
      </TableRow>

      <ConstraintEditDialog
        open={editOpen}
        onOpenChange={setEditOpen}
        constraint={constraint}
        isAdmin={isAdmin}
      />
    </>
  );
}

// ─── Constraint Edit Dialog ────────────────────────────────────
function ConstraintEditDialog({
  open,
  onOpenChange,
  constraint,
  isAdmin,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  constraint: ConstraintDefinitionDto;
  isAdmin: boolean;
}) {
  const toggleMutation = useToggleConstraint();
  const categoryMutation = useUpdateConstraintCategory();
  const weightMutation = useUpdateConstraintWeight();
  const paramsMutation = useUpdateConstraintParameters();
  const toast = useToast();

  const notifySuccess = (msg: string) => toast.showSuccess('Uygulandı', msg);
  const notifyError = (err: unknown, fallback: string) =>
    toast.showError('Kaydedilemedi', err instanceof Error ? err.message : fallback);

  const [paramDraft, setParamDraft] = useState<Record<string, string>>({});
  const schema = PARAMETER_SCHEMAS[constraint.code] ?? [];

  useEffect(() => {
    if (!open) return;
    try {
      const parsed = constraint.parameters ? JSON.parse(constraint.parameters) as Record<string, unknown> : {};
      const draft: Record<string, string> = {};
      for (const f of schema) {
        const v = parsed[f.key];
        draft[f.key] = v == null ? '' : String(v);
      }
      // eslint-disable-next-line react-hooks/set-state-in-effect -- resync params on open
      setParamDraft(draft);
    } catch {
      setParamDraft({});
    }
  }, [open, constraint.parameters, schema]);

  function saveParameters() {
    const payload: Record<string, unknown> = {};
    for (const f of schema) {
      const raw = paramDraft[f.key] ?? '';
      if (raw === '') continue;
      if (f.type === 'number') {
        const n = Number(raw);
        if (Number.isFinite(n)) payload[f.key] = n;
      } else if (f.type === 'boolean') {
        payload[f.key] = raw === 'true';
      } else {
        payload[f.key] = raw;
      }
    }
    paramsMutation.mutate(
      { id: constraint.id, parameters: JSON.stringify(payload) },
      {
        onSuccess: () => notifySuccess(`${constraint.name} parametreleri kaydedildi.`),
        onError: (e) => notifyError(e, 'Parametreler kaydedilemedi.'),
      },
    );
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{constraint.name}</DialogTitle>
          {constraint.description && (
            <DialogDescription>{constraint.description}</DialogDescription>
          )}
        </DialogHeader>

        <div className="space-y-4">
          {/* Aktif / Pasif — Plan 10: kritik geçişlerde onay */}
          <div className="space-y-2">
            <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Durum</label>
            <div className="grid grid-cols-2 gap-2">
              <Button
                type="button"
                variant={constraint.isEnabled ? 'default' : 'outline'}
                onClick={() => {
                  if (constraint.isEnabled) return;
                  toggleMutation.mutate(constraint.id, {
                    onSuccess: () => notifySuccess(`${constraint.name} aktif edildi.`),
                    onError: (e) => notifyError(e, 'Durum değiştirilemedi.'),
                  });
                }}
                disabled={!isAdmin || toggleMutation.isPending || constraint.isEnabled}
              >
                Aktif
              </Button>
              <Button
                type="button"
                variant={!constraint.isEnabled ? 'default' : 'outline'}
                onClick={() => {
                  if (!constraint.isEnabled) return;
                  const ok = window.confirm(
                    `"${constraint.name}" kuralını PASİF yapmak üzeresiniz.\n\n` +
                    `Bu kural artık çalışmayacak; ihlaller engellenmeyecek veya cezalandırılmayacak.\n\n` +
                    `Devam edilsin mi?`
                  );
                  if (!ok) return;
                  toggleMutation.mutate(constraint.id, {
                    onSuccess: () => notifySuccess(`${constraint.name} pasif edildi.`),
                    onError: (e) => notifyError(e, 'Durum değiştirilemedi.'),
                  });
                }}
                disabled={!isAdmin || toggleMutation.isPending || !constraint.isEnabled}
              >
                Pasif
              </Button>
            </div>
          </div>

          {/* Zorunlu / Tercihli — Plan 10: capability-aware + onay */}
          {(() => {
            const capability = getCategoryCapability(constraint.code);
            // hard-only / soft-only kurallarda toggle anlamsız, sadece bilgi göster.
            if (capability !== 'toggleable') {
              const fixedLabel = capability === 'hard-only' ? 'Zorunlu (sabit)' : 'Tercihli (sabit)';
              const fixedExplain = capability === 'hard-only'
                ? 'Bu kural fizik/mantık gereği değiştirilemez (örn. aynı oda + saat = imkansız).'
                : 'Bu kural doğası gereği yumuşak (penalty-bazlı), Zorunlu yapılamaz.';
              return (
                <div className="space-y-2">
                  <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Kategori</label>
                  <div className="rounded-md border bg-muted/40 px-3 py-2 text-sm">
                    <span className="font-semibold">{fixedLabel}</span>
                  </div>
                  <p className="text-[11px] text-muted-foreground">{fixedExplain}</p>
                </div>
              );
            }
            // toggleable — Hard ↔ Soft geçişi mümkün, kritik geçişlerde onay.
            return (
              <div className="space-y-2">
                <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Kategori</label>
                <div className="grid grid-cols-2 gap-2">
                  <Button
                    type="button"
                    variant={constraint.category === 'Hard' ? 'default' : 'outline'}
                    onClick={() => {
                      if (constraint.category === 'Hard') return;
                      const ok = window.confirm(
                        `"${constraint.name}" kuralını ZORUNLU (Hard) moda almak üzeresiniz.\n\n` +
                        `Bu modda kural ihlali otomatik programla sırasında YASAK olur — ihlal eden ` +
                        `atamalar mümkün değil. Bazı dersler yerleşemeyebilir.\n\nDevam edilsin mi?`
                      );
                      if (!ok) return;
                      categoryMutation.mutate(
                        { id: constraint.id, category: 'Hard' },
                        {
                          onSuccess: () => notifySuccess(`${constraint.name} → Zorunlu yapıldı.`),
                          onError: (e) => notifyError(e, 'Kategori değiştirilemedi.'),
                        },
                      );
                    }}
                    disabled={!isAdmin || categoryMutation.isPending || constraint.category === 'Hard'}
                    className={constraint.category === 'Hard' ? 'bg-red-600 hover:bg-red-700' : ''}
                  >
                    Zorunlu
                  </Button>
                  <Button
                    type="button"
                    variant={constraint.category === 'Soft' ? 'default' : 'outline'}
                    onClick={() => {
                      if (constraint.category === 'Soft') return;
                      const ok = window.confirm(
                        `"${constraint.name}" kuralını TERCİHLİ (Soft) moda almak üzeresiniz.\n\n` +
                        `Bu modda kural ihlali yasak değil — ders yerleşebilir ama programa ceza ` +
                        `puanı eklenir. Çözücü bu cezayı minimize etmeye çalışır.\n\nDevam edilsin mi?`
                      );
                      if (!ok) return;
                      categoryMutation.mutate(
                        { id: constraint.id, category: 'Soft' },
                        {
                          onSuccess: () => notifySuccess(`${constraint.name} → Tercihli yapıldı.`),
                          onError: (e) => notifyError(e, 'Kategori değiştirilemedi.'),
                        },
                      );
                    }}
                    disabled={!isAdmin || categoryMutation.isPending || constraint.category === 'Soft'}
                    className={constraint.category === 'Soft' ? 'bg-amber-500 hover:bg-amber-600' : ''}
                  >
                    Tercihli
                  </Button>
                </div>
                <p className="text-[11px] text-muted-foreground">
                  <strong>Zorunlu:</strong> ihlal edilemez (yerleşemez).{' '}
                  <strong>Tercihli:</strong> ihlal edilebilir, ceza alır.
                </p>
              </div>
            );
          })()}

          {/* Ağırlık (Tercihli için) */}
          {constraint.category === 'Soft' && (
            <div className="space-y-2">
              <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Ağırlık <span className="font-mono text-foreground ml-1">{constraint.weight}</span>
              </label>
              <input
                type="range"
                min={1}
                max={10}
                value={constraint.weight}
                onChange={(e) => weightMutation.mutate({ id: constraint.id, weight: Number(e.target.value) })}
                className="w-full accent-amber-500"
                disabled={!isAdmin}
              />
              <p className="text-[11px] text-muted-foreground">
                1 = az önemli, 10 = çok önemli. Yüksek ağırlık ihlal cezasını artırır.
              </p>
            </div>
          )}

          {/* Parametre formu */}
          {schema.length > 0 && (
            <div className="space-y-2 border-t pt-3">
              <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Parametreler</label>
              {schema.map(field => (
                <div key={field.key} className="space-y-1">
                  <label className="text-xs text-muted-foreground">{field.label}</label>
                  <Input
                    type={field.type === 'number' ? 'number' : field.type === 'time' ? 'time' : 'text'}
                    value={paramDraft[field.key] ?? ''}
                    min={field.min}
                    max={field.max}
                    onChange={(e) => setParamDraft(d => ({ ...d, [field.key]: e.target.value }))}
                    disabled={!isAdmin}
                  />
                  {field.suffix && (
                    <p className="text-[10px] text-muted-foreground">Birim: {field.suffix}</p>
                  )}
                </div>
              ))}
              <Button
                size="sm"
                onClick={saveParameters}
                disabled={!isAdmin || paramsMutation.isPending}
              >
                {paramsMutation.isPending ? 'Kaydediliyor…' : 'Parametreleri Kaydet'}
              </Button>
            </div>
          )}

          {/* İstisna sayısı bilgisi */}
          {constraint.exceptionCount > 0 && (
            <div className="rounded-md bg-blue-50 px-2.5 py-1.5 text-xs text-blue-900">
              Bu kısıt için <strong>{constraint.exceptionCount}</strong> istisna tanımlı.
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Kapat</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}


// ─── Main Page ──────────────────────────────────────────────────
export default function ConstraintManagementPage() {
  const { data: constraints, isLoading, isError } = useConstraints();
  const { data: terms } = useAdminTerms();
  const { user } = useAuth();
  const isAdmin = user?.roles.includes('SystemAdministrator') ?? false;

  const [activeTab, setActiveTab] = useState<'constraints' | 'algorithm' | 'pairing'>('constraints');
  const [view, setView] = useState<'all' | 'hard' | 'soft'>('all');

  // Default to the active term, fallback to latest
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const effectiveTermId = useMemo(() => {
    if (selectedTermId !== null) return selectedTermId;
    if (!terms || terms.length === 0) return null;
    const active = terms.find(t => t.isActive);
    return active ? active.id : terms[0].id;
  }, [selectedTermId, terms]);

  // useMemo ile stable referans (logical || [] her render'da yeni array yaratırdı)
  const items = useMemo(() => constraints ?? [], [constraints]);

  const groupedConstraints = useMemo(() => {
    return CONSTRAINT_GROUPS.map(group => ({
      group,
      items: group.codes
        .map(code => items.find(c => c.code === code))
        .filter((c): c is ConstraintDefinitionDto => c !== undefined),
    }));
  }, [items]);

  function filterByCategory(category: 'Hard' | 'Soft' | 'all') {
    return groupedConstraints.map(({ group, items: groupItems }) => ({
      group,
      items: category === 'all' ? groupItems : groupItems.filter(c => c.category === category),
    }));
  }

  if (isLoading) return <Skeleton className="h-96 rounded-xl" />;
  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Kısıtlamalar yüklenemedi.</AlertDescription>
      </Alert>
    );
  }

  const hardCount = items.filter(c => c.category === 'Hard').length;
  const softCount = items.filter(c => c.category === 'Soft').length;

  return (
    <div className="space-y-2.5">
      {/* Pending approval requests */}
      <ApprovalPanel />

      <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as typeof activeTab)}>
        {/* Tek satır: Tabs + Dönem + Filtre + Aksiyonlar */}
        <div className="flex flex-wrap items-center gap-2 rounded-xl border border-gray-200 bg-white px-3 py-2 shadow-sm">
          <TabsList className="shrink-0">
            <TabsTrigger value="constraints">Kısıtlar</TabsTrigger>
            {isAdmin && <TabsTrigger value="algorithm">Algoritma Parametreleri</TabsTrigger>}
            {isAdmin && <TabsTrigger value="pairing">Ders Eşleştirme Kuralları</TabsTrigger>}
          </TabsList>

          {isAdmin && terms && terms.length > 0 && (
            <>
              <div className="w-px h-6 bg-gray-200 mx-1" />

              <span className="text-xs font-semibold text-gray-500 whitespace-nowrap">Dönem</span>
              <select
                className="h-7 rounded-md border border-gray-200 bg-gray-50 px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none min-w-[160px]"
                value={effectiveTermId ?? ''}
                onChange={(e) => setSelectedTermId(Number(e.target.value))}
              >
                {terms.map(t => (
                  <option key={t.id} value={t.id}>
                    {t.name} {t.isActive ? '(Aktif)' : ''}
                  </option>
                ))}
              </select>

              {activeTab === 'constraints' && (
                <>
                  <span className="text-xs font-semibold text-gray-500 whitespace-nowrap">Filtre</span>
                  <select
                    className="h-7 rounded-md border border-gray-200 bg-gray-50 px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none min-w-[140px]"
                    value={view}
                    onChange={(e) => setView(e.target.value as typeof view)}
                  >
                    <option value="all">Tümü ({items.length})</option>
                    <option value="hard">Zorunlu ({hardCount})</option>
                    <option value="soft">Tercihli ({softCount})</option>
                  </select>
                </>
              )}

            </>
          )}
        </div>

        <TabsContent value="constraints" className="space-y-3 mt-3">
          {items.length === 0 ? (
            <Card>
              <CardContent className="py-8 text-center">
                <p className="text-sm text-muted-foreground">Henüz kısıtlama tanımlanmamış.</p>
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardContent className="p-0">
                <div className="overflow-x-auto">
                  <Table className="min-w-[900px]">
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-44 text-sm">Grup</TableHead>
                        <TableHead className="text-sm">Kısıt</TableHead>
                        <TableHead className="w-28 text-sm">Durum</TableHead>
                        <TableHead className="w-32 text-sm">Kategori</TableHead>
                        <TableHead className="w-28 text-sm">Ağırlık</TableHead>
                        <TableHead className="w-24 text-right text-sm">Düzenle</TableHead>
                      </TableRow>
                    </TableHeader>
                  <TableBody>
                    {filterByCategory(view === 'all' ? 'all' : view === 'hard' ? 'Hard' : 'Soft').flatMap(({ group, items: groupItems }) =>
                      groupItems.map(c => (
                        <ConstraintRow
                          key={c.id}
                          constraint={c}
                          groupLabel={group.label}
                          groupKey={group.key}
                          isAdmin={isAdmin}
                        />
                      ))
                    )}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {isAdmin && (
          <TabsContent value="algorithm" className="mt-3">
            <AlgorithmParametersPanel />
          </TabsContent>
        )}

        {isAdmin && (
          <TabsContent value="pairing" className="mt-3">
            {effectiveTermId ? (
              <DistributionConstraintPanel termId={effectiveTermId} />
            ) : (
              <Card>
                <CardContent className="py-8 text-center">
                  <p className="text-sm text-muted-foreground">Önce bir dönem seçin.</p>
                </CardContent>
              </Card>
            )}
          </TabsContent>
        )}
      </Tabs>
    </div>
  );
}
