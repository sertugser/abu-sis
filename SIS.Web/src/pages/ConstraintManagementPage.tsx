import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
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
  AlertCircle, ChevronDown, ChevronUp, HelpCircle,
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import {
  useConstraints,
  useToggleConstraint,
  useUpdateConstraintCategory,
} from '../hooks/useConstraints';
import { SCOPE_LABELS } from '../components/constraint/helpers';
import ConstraintDetailPanel from '../components/constraint/ConstraintDetailPanel';
import type { ConstraintDefinitionDto } from '../types/constraint';

// ─── Constraint Row ─────────────────────────────────────────────
function ConstraintRow({
  constraint,
  isAdmin,
}: {
  constraint: ConstraintDefinitionDto;
  isAdmin: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const toggleMutation = useToggleConstraint();
  const categoryMutation = useUpdateConstraintCategory();

  function handleCategoryToggle() {
    const next = constraint.category === 'Hard' ? 'Soft' : 'Hard';
    categoryMutation.mutate({ id: constraint.id, category: next });
  }

  return (
    <Card className="mb-2">
      <CardContent className="py-3 px-4">
        <div className="flex items-center gap-3 flex-wrap">
          {/* Toggle */}
          <Tooltip>
            <TooltipTrigger asChild>
              <div>
                <Switch
                  checked={constraint.isEnabled}
                  onCheckedChange={() => toggleMutation.mutate(constraint.id)}
                  disabled={toggleMutation.isPending}
                />
              </div>
            </TooltipTrigger>
            <TooltipContent>
              <p className="text-xs">
                {constraint.isEnabled ? 'Aktif - tikla devre disi birak' : 'Devre disi - tikla etkinlestir'}
              </p>
            </TooltipContent>
          </Tooltip>

          {/* Code */}
          <Badge variant="outline" className="font-mono text-[11px]">
            {constraint.code}
          </Badge>

          {/* Name */}
          <span className="text-sm font-medium flex-1 min-w-0 truncate">{constraint.name}</span>

          {/* Category badge */}
          <Badge variant={constraint.category === 'Hard' ? 'destructive' : 'default'} className="text-[10px]">
            {constraint.category}
          </Badge>

          {/* Scope */}
          <Badge variant="secondary" className="text-[10px]">
            {SCOPE_LABELS[constraint.scope] ?? constraint.scope}
          </Badge>

          {/* Params summary */}
          {constraint.parameters && (
            <Tooltip>
              <TooltipTrigger asChild>
                <HelpCircle className="h-3.5 w-3.5 text-muted-foreground cursor-help" />
              </TooltipTrigger>
              <TooltipContent className="max-w-xs">
                <pre className="text-[10px] whitespace-pre-wrap">{constraint.parameters}</pre>
              </TooltipContent>
            </Tooltip>
          )}

          {/* Exception count */}
          {constraint.exceptionCount > 0 && (
            <Badge variant="warning" className="text-[10px]">
              {constraint.exceptionCount} istisna
            </Badge>
          )}

          {/* Category change (admin only) */}
          {isAdmin && (
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 text-xs"
                  onClick={handleCategoryToggle}
                  disabled={categoryMutation.isPending}
                >
                  {constraint.category === 'Hard' ? 'Soft Yap' : 'Hard Yap'}
                </Button>
              </TooltipTrigger>
              <TooltipContent>
                <p className="text-xs">Kategoriyi degistir</p>
              </TooltipContent>
            </Tooltip>
          )}

          {/* Expand */}
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => setExpanded(v => !v)}>
            {expanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
          </Button>
        </div>

        {expanded && (
          <div className="mt-3 border-t pt-3">
            <ConstraintDetailPanel constraint={constraint} isAdmin={isAdmin} />
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ─── Main Page ──────────────────────────────────────────────────
export default function ConstraintManagementPage() {
  const { data: constraints, isLoading, isError } = useConstraints();
  const { user } = useAuth();
  const isAdmin = user?.roles.includes('SystemAdministrator') ?? false;

  if (isLoading) return <Skeleton className="h-96 rounded-xl" />;
  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Kisitlamalar yuklenemedi.</AlertDescription>
      </Alert>
    );
  }

  const items = constraints ?? [];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-bold">Program Kisitlamalari</h1>
        <p className="text-sm text-muted-foreground">
          Ders programi olusturma surecinde uygulanan kisitlamalari yonetin, etkinlestirin veya istisnalar tanimlayin.
        </p>
      </div>

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">Tumu ({items.length})</TabsTrigger>
          <TabsTrigger value="hard">
            Hard ({items.filter(c => c.category === 'Hard').length})
          </TabsTrigger>
          <TabsTrigger value="soft">
            Soft ({items.filter(c => c.category === 'Soft').length})
          </TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          {items.map(c => <ConstraintRow key={c.id} constraint={c} isAdmin={isAdmin} />)}
        </TabsContent>
        <TabsContent value="hard">
          {items.filter(c => c.category === 'Hard').map(c => (
            <ConstraintRow key={c.id} constraint={c} isAdmin={isAdmin} />
          ))}
        </TabsContent>
        <TabsContent value="soft">
          {items.filter(c => c.category === 'Soft').map(c => (
            <ConstraintRow key={c.id} constraint={c} isAdmin={isAdmin} />
          ))}
        </TabsContent>
      </Tabs>

      {items.length === 0 && (
        <Card>
          <CardContent className="py-8 text-center">
            <p className="text-sm text-muted-foreground">Henuz kisitlama tanimlanmamis.</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
