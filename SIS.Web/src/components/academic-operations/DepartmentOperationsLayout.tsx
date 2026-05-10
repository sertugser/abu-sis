import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { AlertCircle, Building2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent } from '@/components/ui/card';
import type { AcademicOperationsContext } from '@/types/academicOperations';

interface DepartmentOperationsLayoutProps {
  icon: LucideIcon;
  title: string;
  description: string;
  context: AcademicOperationsContext | null;
  departmentId: number | null;
  onDepartmentChange: (departmentId: number) => void;
  termName?: string;
  isContextLoading?: boolean;
  contextError?: string | null;
  showDepartmentBadge?: boolean;
  showDepartmentSelect?: boolean;
  compactHeader?: boolean;
  hideHeader?: boolean;
  actions?: ReactNode;
  children: ReactNode;
}

export default function DepartmentOperationsLayout({
  icon: Icon,
  title,
  description,
  context,
  departmentId,
  onDepartmentChange,
  termName,
  isContextLoading = false,
  contextError,
  showDepartmentBadge = true,
  showDepartmentSelect = true,
  compactHeader = false,
  hideHeader = false,
  actions,
  children,
}: DepartmentOperationsLayoutProps) {
  const activeDepartment = context?.accessibleDepartments.find(department => department.departmentId === departmentId);
  const shouldShowDepartmentSelect = showDepartmentSelect && (context?.accessibleDepartments.length ?? 0) > 1;
  const showMeta = (showDepartmentBadge && activeDepartment) || termName;
  const hasHeaderControls = shouldShowDepartmentSelect || actions;
  const headerControls = (
    <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
      {shouldShowDepartmentSelect && (
        <select
          value={departmentId ?? ''}
          onChange={event => onDepartmentChange(Number(event.target.value))}
          className="h-9 min-w-[240px] rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring"
        >
          {context?.accessibleDepartments.map(department => (
            <option key={department.departmentId} value={department.departmentId}>
              {department.departmentCode} - {department.departmentName}
            </option>
          ))}
        </select>
      )}
      {actions}
    </div>
  );

  return (
    <div className={compactHeader ? 'space-y-3' : 'space-y-5'}>
      {hideHeader ? (
        hasHeaderControls ? (
          <div className="flex justify-end">
            {headerControls}
          </div>
        ) : null
      ) : (
      <div className={`flex flex-col lg:flex-row lg:items-start lg:justify-between ${compactHeader ? 'gap-3' : 'gap-4'}`}>
        <div className={compactHeader ? 'flex min-w-0 items-start gap-2.5' : 'flex min-w-0 items-start gap-3'}>
          <div className={`rounded-lg border bg-card text-primary ${compactHeader ? 'p-2' : 'p-2.5'}`}>
            <Icon className={compactHeader ? 'h-4 w-4' : 'h-5 w-5'} />
          </div>
          <div className="min-w-0">
            <h1 className={`font-heading font-semibold tracking-tight ${compactHeader ? 'text-xl' : 'text-2xl'}`}>{title}</h1>
            <p className={`mt-1 max-w-3xl text-muted-foreground ${compactHeader ? 'text-xs' : 'text-sm'}`}>{description}</p>
            {showMeta && (
            <div className={compactHeader ? 'mt-1.5 flex flex-wrap items-center gap-2 text-xs text-muted-foreground' : 'mt-2 flex flex-wrap items-center gap-2 text-xs text-muted-foreground'}>
              {showDepartmentBadge && activeDepartment && (
                <span className="inline-flex items-center gap-1 rounded-md border bg-muted/30 px-2 py-1">
                  <Building2 className="h-3.5 w-3.5" />
                  {activeDepartment.departmentCode} - {activeDepartment.departmentName}
                </span>
              )}
              {termName && <span className="rounded-md border bg-muted/30 px-2 py-1">{termName}</span>}
            </div>
            )}
          </div>
        </div>

        {headerControls}
      </div>
      )}

      {isContextLoading ? (
        <Card>
          <CardContent className="py-10 text-sm text-muted-foreground">Kapsam bilgileri yükleniyor...</CardContent>
        </Card>
      ) : contextError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Bölüm kapsamı alınamadı</AlertTitle>
          <AlertDescription>{contextError}</AlertDescription>
        </Alert>
      ) : departmentId === null ? (
        <Alert variant="warning">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Bölüm bulunamadı</AlertTitle>
          <AlertDescription>Bu ekran için atanmış veya erişilebilir bir bölüm yok.</AlertDescription>
        </Alert>
      ) : (
        children
      )}
    </div>
  );
}
