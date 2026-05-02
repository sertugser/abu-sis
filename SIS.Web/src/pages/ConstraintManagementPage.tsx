import { useState, useMemo, useEffect } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  Tooltip, TooltipContent, TooltipTrigger,
} from '@/components/ui/tooltip';
import {
  AlertCircle, Building2, BookOpen, CheckCircle2, ChevronDown, ChevronUp,
  Clock, FlaskConical, GraduationCap,
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import {
  useConstraints,
  useToggleConstraint,
  useUpdateConstraintCategory,
  useScoreBreakdown,
  useUpdateConstraintWeight,
} from '../hooks/useConstraints';
import { useAdminTerms } from '../hooks/useAdminTerms';
import {
  CATEGORY_LABELS, CONSTRAINT_GROUPS,
} from '../components/constraint/helpers';
import ConstraintDetailPanel from '../components/constraint/ConstraintDetailPanel';
import ProfileSelector from '../components/constraint/ProfileSelector';
import ScoreBreakdownDashboard from '../components/constraint/ScoreBreakdownDashboard';
import DistributionConstraintPanel from '../components/constraint/DistributionConstraintPanel';
import AlgorithmParametersPanel from '../components/constraint/AlgorithmParametersPanel';
import ApprovalPanel from '../components/constraint/ApprovalPanel';
import SimulationDrawer from '../components/constraint/SimulationDrawer';
import { useTrendData, useHeatmap, useBottlenecks, useTakeSnapshot } from '../hooks/useConstraintAnalytics';
import type { ConstraintDefinitionDto } from '../types/constraint';

const GROUP_ICONS = { Building2, GraduationCap, BookOpen, Clock } as const;


// ─── Constraint Row ─────────────────────────────────────────────
function ConstraintRow({
  constraint,
  isAdmin,
}: {
  constraint: ConstraintDefinitionDto;
  isAdmin: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const [showToggleConfirm, setShowToggleConfirm] = useState(false);
  const [showCategoryConfirm, setShowCategoryConfirm] = useState(false);
  const toggleMutation = useToggleConstraint();
  const categoryMutation = useUpdateConstraintCategory();
  const weightMutation = useUpdateConstraintWeight();

  const nextCategory = constraint.category === 'Hard' ? 'Soft' : 'Hard';
  const nextCategoryLabel = CATEGORY_LABELS[nextCategory];

  function handleToggleClick() {
    if (constraint.isEnabled && constraint.category === 'Hard' && isAdmin) {
      setShowToggleConfirm(true);
    } else {
      toggleMutation.mutate(constraint.id);
    }
  }

  function handleCategoryClick() {
    if (isAdmin) {
      setShowCategoryConfirm(true);
    }
  }

  const categoryBadgeClass = constraint.category === 'Hard'
    ? 'bg-red-600 text-white border-red-600 dark:bg-red-700 dark:border-red-700'
    : 'bg-amber-400 text-white border-amber-400 dark:bg-amber-500 dark:border-amber-500';

  return (
    <>
      <div className={`rounded-lg border px-4 py-3 transition-colors ${
        constraint.isEnabled
          ? 'bg-card border-border'
          : 'bg-muted/40 border-border/50 opacity-70'
      }`}>
        <div className="flex items-center gap-3">
          {/* Toggle */}
          {isAdmin ? (
            <Tooltip>
              <TooltipTrigger asChild>
                <div>
                  <Switch
                    checked={constraint.isEnabled}
                    onCheckedChange={handleToggleClick}
                    disabled={toggleMutation.isPending}
                  />
                </div>
              </TooltipTrigger>
              <TooltipContent>
                <p className="text-xs">
                  {constraint.isEnabled ? 'Devre dışı bırak' : 'Etkinleştir'}
                </p>
              </TooltipContent>
            </Tooltip>
          ) : (
            <div className={`h-2.5 w-2.5 rounded-full shrink-0 ${constraint.isEnabled ? 'bg-green-500' : 'bg-gray-300'}`} />
          )}

          {/* Category badge */}
          <Badge variant="outline" className={`text-[10px] shrink-0 ${categoryBadgeClass}`}>
            {CATEGORY_LABELS[constraint.category]}
          </Badge>

          {/* Weight slider (Soft only) */}
          {constraint.category === 'Soft' ? (
            <div className="flex items-center gap-2 min-w-[140px] shrink-0">
              <span className="text-xs text-muted-foreground tabular-nums w-4">{constraint.weight}</span>
              <input
                type="range"
                min={1}
                max={10}
                value={constraint.weight}
                onChange={(e) => weightMutation.mutate({ id: constraint.id, weight: Number(e.target.value) })}
                className="w-20 accent-amber-500"
                disabled={!isAdmin}
              />
            </div>
          ) : (
            <Tooltip>
              <TooltipTrigger asChild>
                <span className="text-xs text-muted-foreground shrink-0 cursor-help">—</span>
              </TooltipTrigger>
              <TooltipContent>
                <p className="text-xs">Zorunlu kısıtlamalarda ağırlık uygulanmaz</p>
              </TooltipContent>
            </Tooltip>
          )}

          {/* Name + description */}
          <div className="flex-1 min-w-0">
            <span className="text-sm font-medium block truncate">{constraint.name}</span>
            {constraint.description && (
              <span className="text-xs text-muted-foreground block truncate mt-0.5">
                {constraint.description}
              </span>
            )}
          </div>

          {/* Exception count */}
          {constraint.exceptionCount > 0 && (
            <Badge variant="secondary" className="text-[10px] shrink-0">
              {constraint.exceptionCount} istisna
            </Badge>
          )}

          {/* Category change (admin only) */}
          {isAdmin && (
            <Button
              variant="outline"
              size="sm"
              className="h-7 text-xs shrink-0"
              onClick={handleCategoryClick}
              disabled={categoryMutation.isPending}
            >
              {nextCategoryLabel} Yap
            </Button>
          )}

          {/* Expand */}
          <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" onClick={() => setExpanded(v => !v)}>
            {expanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
          </Button>
        </div>

        {expanded && (
          <div className="mt-3 border-t pt-3">
            <ConstraintDetailPanel constraint={constraint} isAdmin={isAdmin} />
          </div>
        )}
      </div>

      {/* Toggle confirmation */}
      <AlertDialog open={showToggleConfirm} onOpenChange={setShowToggleConfirm}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Zorunlu kısıtlamayı devre dışı bırak?</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>"{constraint.name}"</strong> zorunlu bir kısıtlamadır.
              Devre dışı bırakıldığında ders programı oluşturulurken bu kural kontrol edilmeyecek
              ve istenmeyen çakışmalara yol açabilir.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => toggleMutation.mutate(constraint.id)}
            >
              Devre Dışı Bırak
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Category change confirmation */}
      <AlertDialog open={showCategoryConfirm} onOpenChange={setShowCategoryConfirm}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              Kısıtlamayı {nextCategoryLabel.toLowerCase()} yap?
            </AlertDialogTitle>
            <AlertDialogDescription>
              <strong>"{constraint.name}"</strong> kısıtlamasını{' '}
              <strong>{CATEGORY_LABELS[constraint.category].toLowerCase()}</strong> &rarr;{' '}
              <strong>{nextCategoryLabel.toLowerCase()}</strong> olarak değiştirmek istediğinizden emin misiniz?
              {nextCategory === 'Soft' && (
                <span className="block mt-2 text-amber-600 dark:text-amber-400">
                  Tercihli olarak değiştirildiğinde bu kural ihlal edilebilir hale gelir.
                  Program kalitesi düşebilir ancak engelleme yapılmaz.
                </span>
              )}
              {nextCategory === 'Hard' && (
                <span className="block mt-2 text-red-600 dark:text-red-400">
                  Zorunlu olarak değiştirildiğinde bu kural kesinlikle ihlal edilemez.
                  İhlal durumunda program oluşturulamaz.
                </span>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => categoryMutation.mutate({ id: constraint.id, category: nextCategory })}
            >
              {nextCategoryLabel} Yap
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

// ─── Group Section ──────────────────────────────────────────────
function GroupSection({
  group,
  constraints,
  isAdmin,
}: {
  group: typeof CONSTRAINT_GROUPS[number];
  constraints: ConstraintDefinitionDto[];
  isAdmin: boolean;
}) {
  const [open, setOpen] = useState(true);
  const Icon = GROUP_ICONS[group.icon];
  const hardCount = constraints.filter(c => c.category === 'Hard').length;
  const softCount = constraints.filter(c => c.category === 'Soft').length;

  if (constraints.length === 0) return null;

  return (
    <div className="space-y-2">
      <button
        type="button"
        className="flex items-center gap-2 w-full text-left py-1.5 group"
        onClick={() => setOpen(v => !v)}
      >
        <Icon className="h-4 w-4 text-muted-foreground" />
        <span className="text-sm font-semibold flex-1">{group.label}</span>
        <span className="text-xs text-muted-foreground">
          {hardCount > 0 && <span className="text-red-600 dark:text-red-400">{hardCount} zorunlu</span>}
          {hardCount > 0 && softCount > 0 && ' / '}
          {softCount > 0 && <span className="text-amber-600 dark:text-amber-400">{softCount} tercihli</span>}
        </span>
        {open ? <ChevronUp className="h-4 w-4 text-muted-foreground" /> : <ChevronDown className="h-4 w-4 text-muted-foreground" />}
      </button>
      {open && (
        <div className="space-y-1.5 pl-6">
          {constraints.map(c => (
            <ConstraintRow key={c.id} constraint={c} isAdmin={isAdmin} />
          ))}
        </div>
      )}
    </div>
  );
}


// ─── Main Page ──────────────────────────────────────────────────
export default function ConstraintManagementPage() {
  const { data: constraints, isLoading, isError } = useConstraints();
  const { data: terms } = useAdminTerms();
  const { user } = useAuth();
  const isAdmin = user?.roles.includes('SystemAdministrator') ?? false;

  const [profileToast, setProfileToast] = useState<string | null>(null);
  const [simulationOpen, setSimulationOpen] = useState(false);

  useEffect(() => {
    if (!profileToast) return;
    const t = setTimeout(() => setProfileToast(null), 5000);
    return () => clearTimeout(t);
  }, [profileToast]);

  // Default to the active term, fallback to latest
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const effectiveTermId = useMemo(() => {
    if (selectedTermId !== null) return selectedTermId;
    if (!terms || terms.length === 0) return null;
    const active = terms.find(t => t.isActive);
    return active ? active.id : terms[0].id;
  }, [selectedTermId, terms]);

  const { data: breakdown, isLoading: breakdownLoading } = useScoreBreakdown(effectiveTermId);
  const { data: trendData } = useTrendData(effectiveTermId);
  const { data: heatmapData } = useHeatmap(effectiveTermId);
  const { data: bottleneckData } = useBottlenecks(effectiveTermId);
  const takeSnapshotMut = useTakeSnapshot(effectiveTermId);

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
      {/* Profile toast */}
      {profileToast && (
        <Alert variant="default" className="py-2">
          <CheckCircle2 className="h-4 w-4" />
          <AlertDescription className="text-xs">{profileToast}</AlertDescription>
        </Alert>
      )}

      {/* Term selector + Score breakdown */}
      {isAdmin && terms && terms.length > 0 && (
        <div className="space-y-3">
          <div className="flex items-center gap-2 rounded-xl border border-gray-200 bg-white px-3 py-2 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 whitespace-nowrap">Dönem</span>
            <select
              className="h-7 rounded-md border border-gray-200 bg-gray-50 px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none min-w-[200px]"
              value={effectiveTermId ?? ''}
              onChange={(e) => setSelectedTermId(Number(e.target.value))}
            >
              {terms.map(t => (
                <option key={t.id} value={t.id}>
                  {t.name} {t.isActive ? '(Aktif)' : ''}
                </option>
              ))}
            </select>

            <div className="w-px self-stretch bg-gray-200 mx-1" />

            <ProfileSelector onApplied={setProfileToast} />

            <div className="w-px self-stretch bg-gray-200 mx-1" />

            <button
              type="button"
              className="inline-flex items-center gap-1.5 rounded-md border border-gray-200 bg-gray-50 px-2.5 h-7 text-xs font-medium text-gray-600 hover:bg-gray-100 transition-colors"
              onClick={() => setSimulationOpen(true)}
            >
              <FlaskConical className="h-3.5 w-3.5" />
              Simülasyon
            </button>
          </div>
          <ScoreBreakdownDashboard
            breakdown={breakdown}
            isLoading={breakdownLoading}
            trendData={trendData}
            heatmapData={heatmapData}
            bottleneckData={bottleneckData}
            onTakeSnapshot={() => takeSnapshotMut.mutate()}
            snapshotPending={takeSnapshotMut.isPending}
          />
        </div>
      )}

      {/* Pending approval requests */}
      <ApprovalPanel />

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">Tümü ({items.length})</TabsTrigger>
          <TabsTrigger value="hard">
            Zorunlu ({hardCount})
          </TabsTrigger>
          <TabsTrigger value="soft">
            Tercihli ({softCount})
          </TabsTrigger>
          {isAdmin && (
            <TabsTrigger value="algorithm">
              Algoritma Parametreleri
            </TabsTrigger>
          )}
        </TabsList>

        {(['all', 'hard', 'soft'] as const).map(tab => (
          <TabsContent key={tab} value={tab} className="space-y-3 mt-3">
            {filterByCategory(tab === 'all' ? 'all' : tab === 'hard' ? 'Hard' : 'Soft').map(({ group, items: groupItems }) => (
              <GroupSection
                key={group.key}
                group={group}
                constraints={groupItems}
                isAdmin={isAdmin}
              />
            ))}
          </TabsContent>
        ))}

        {isAdmin && (
          <TabsContent value="algorithm" className="mt-3">
            <AlgorithmParametersPanel />
          </TabsContent>
        )}
      </Tabs>

      {items.length === 0 && (
        <Card>
          <CardContent className="py-8 text-center">
            <p className="text-sm text-muted-foreground">Henüz kısıtlama tanımlanmamış.</p>
          </CardContent>
        </Card>
      )}

      {/* Distribution Constraints */}
      {isAdmin && effectiveTermId && (
        <div className="space-y-3 border-t pt-6">
          <DistributionConstraintPanel termId={effectiveTermId} />
        </div>
      )}


      {/* Simulation Drawer */}
      <SimulationDrawer
        open={simulationOpen}
        onOpenChange={setSimulationOpen}
        termId={effectiveTermId}
        constraints={items}
      />
    </div>
  );
}
