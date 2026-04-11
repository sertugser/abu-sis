// SIS.Web/src/pages/FacultyPlanningPage.tsx
import { useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle } from 'lucide-react';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties } from '../hooks/useAdminAcademicStructure';
import { StatusBar } from '../components/faculty-planning/StatusBar';
import { ConflictBar } from '../components/faculty-planning/ConflictBar';
import { SectionListTab } from '../components/faculty-planning/SectionListTab';
import { AutoScheduleTab } from '../components/faculty-planning/AutoScheduleTab';
import { CalendarViewTab } from '../components/faculty-planning/CalendarViewTab';

type TabKey = 'list' | 'calendar' | 'auto-schedule';

const TABS: { key: TabKey; label: string }[] = [
  { key: 'list', label: 'Liste Görünümü' },
  { key: 'calendar', label: 'Takvim Görünümü' },
  { key: 'auto-schedule', label: 'Otomatik Programla' },
];

export default function FacultyPlanningPage() {
  const [activeTab, setActiveTab] = useState<TabKey>('list');
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties, isLoading: facultiesLoading } = useAdminFaculties();

  const departments = contextQuery.data?.accessibleDepartments ?? [];

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const facultyId = selectedFacultyId ?? faculties?.[0]?.id ?? null;
  const termId = activeTerm?.id ?? null;

  if (contextQuery.isLoading || facultiesLoading) return <Skeleton className="h-96 rounded-xl" />;

  if (contextQuery.isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Fakülte planlama sayfası yüklenemedi</AlertTitle>
        <AlertDescription>Yetkili kapsam bilgileri alınamadı.</AlertDescription>
      </Alert>
    );
  }

  if (!facultyId) {
    return (
      <Alert>
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Fakülte bulunamadı</AlertTitle>
        <AlertDescription>Sistemde tanımlı fakülte bulunmamaktadır.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-4">
      {/* Inline status bar — always visible */}
      <StatusBar facultyId={facultyId} termId={termId} />

      {/* Faculty selector + tab buttons */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2">
          <label className="text-sm font-medium">Fakülte:</label>
          <select
            className="h-9 rounded-md border border-input bg-background px-3 text-sm"
            value={facultyId}
            onChange={e => setSelectedFacultyId(Number(e.target.value))}
          >
            {(faculties ?? []).map(f => (
              <option key={f.id} value={f.id}>{f.code} — {f.name}</option>
            ))}
          </select>
        </div>
        <div className="flex flex-wrap gap-2 ml-auto">
          {TABS.map(tab => (
            <Button
              key={tab.key}
              variant={activeTab === tab.key ? 'default' : 'outline'}
              size="sm"
              onClick={() => setActiveTab(tab.key)}
            >
              {tab.label}
            </Button>
          ))}
        </div>
      </div>

      {/* Tab content */}
      {activeTab === 'list' && (
        <SectionListTab facultyId={facultyId} termId={termId} departments={departments} />
      )}
      {activeTab === 'calendar' && (
        <CalendarViewTab facultyId={facultyId} termId={termId} />
      )}
      {activeTab === 'auto-schedule' && (
        <AutoScheduleTab facultyId={facultyId} termId={termId} />
      )}

      {/* Inline conflict bar — always visible when conflicts exist */}
      <ConflictBar facultyId={facultyId} termId={termId} />
    </div>
  );
}
