import { Search, SlidersHorizontal, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  useAdminDepartments,
  useAdminPrograms,
  useAdminAdvisors,
  useAdminCurricula,
} from '../../../hooks/useAdminLookup';
import { STUDENT_STATUS_LABELS } from '../../../lib/constants';
import type { StudentFilterState } from './studentFilter';

const SELECT_CLASS = 'border border-input rounded-md px-3 py-2 text-sm bg-background text-foreground';
const LABEL_CLASS = 'text-[11px] font-medium uppercase tracking-wide text-muted-foreground';

interface StudentFilterBarProps {
  filters: StudentFilterState;
  onChange: (patch: Partial<StudentFilterState>) => void;
  onClear: () => void;
  advancedOpen: boolean;
  onToggleAdvanced: () => void;
}

export default function StudentFilterBar({
  filters,
  onChange,
  onClear,
  advancedOpen,
  onToggleAdvanced,
}: StudentFilterBarProps) {
  const { data: departments = [] } = useAdminDepartments();
  const { data: programs = [] } = useAdminPrograms(filters.departmentId);
  const { data: advisors = [] } = useAdminAdvisors();
  const { data: curricula = [] } = useAdminCurricula(filters.programId);

  return (
    <div className="px-4 py-3 border-b border-border space-y-3">
      {/* Basic row */}
      <div className="flex flex-wrap gap-3 items-center">
        <div className="relative flex-1 min-w-[220px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            aria-label="Öğrenci ara"
            placeholder="Ad, soyad veya öğrenci numarası..."
            value={filters.search}
            onChange={e => onChange({ search: e.target.value })}
            className="pl-9"
          />
        </div>

        <select
          aria-label="Bölüm"
          value={filters.departmentId ?? ''}
          onChange={e =>
            onChange({
              departmentId: e.target.value ? Number(e.target.value) : undefined,
              programId: undefined,
              curriculumId: undefined,
            })
          }
          className={SELECT_CLASS}
        >
          <option value="">Tüm Bölümler</option>
          {departments.map(d => (
            <option key={d.id} value={d.id}>{d.name}</option>
          ))}
        </select>

        <select
          aria-label="Program"
          value={filters.programId ?? ''}
          onChange={e =>
            onChange({
              programId: e.target.value ? Number(e.target.value) : undefined,
              curriculumId: undefined,
            })
          }
          className={SELECT_CLASS}
        >
          <option value="">Tüm Programlar</option>
          {programs.map(p => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>

        <select
          aria-label="Durum"
          value={filters.status}
          onChange={e => onChange({ status: e.target.value })}
          className={SELECT_CLASS}
        >
          <option value="">Tüm Durumlar</option>
          {Object.entries(STUDENT_STATUS_LABELS).map(([k, v]) => (
            <option key={k} value={k}>{v}</option>
          ))}
        </select>

        <select
          aria-label="Danışman durumu"
          value={filters.advisorFilter}
          onChange={e => onChange({ advisorFilter: e.target.value })}
          className={SELECT_CLASS}
        >
          <option value="">Danışman: hepsi</option>
          <option value="true">Danışmanı var</option>
          <option value="false">Danışmanı yok</option>
        </select>

        <Button variant="outline" size="sm" onClick={onToggleAdvanced}>
          <SlidersHorizontal className="h-4 w-4 mr-2" />
          Gelişmiş filtreler
        </Button>
      </div>

      {/* Advanced grid */}
      {advancedOpen && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3 pt-3 border-t border-border">
          <label className="flex flex-col gap-1">
            <span className={LABEL_CLASS}>GNO Min</span>
            <input
              type="number"
              step={0.01}
              min={0}
              max={4}
              value={filters.minCgpa}
              onChange={e => onChange({ minCgpa: e.target.value })}
              className={SELECT_CLASS}
            />
          </label>

          <label className="flex flex-col gap-1">
            <span className={LABEL_CLASS}>GNO Max</span>
            <input
              type="number"
              step={0.01}
              min={0}
              max={4}
              value={filters.maxCgpa}
              onChange={e => onChange({ maxCgpa: e.target.value })}
              className={SELECT_CLASS}
            />
          </label>

          <label className="flex flex-col gap-1">
            <span className={LABEL_CLASS}>Sınıf</span>
            <select
              value={filters.classYear}
              onChange={e => onChange({ classYear: e.target.value })}
              className={SELECT_CLASS}
            >
              <option value="">Tümü</option>
              <option value="1">1</option>
              <option value="2">2</option>
              <option value="3">3</option>
              <option value="4">4</option>
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className={LABEL_CLASS}>Müfredat</span>
            <select
              value={filters.curriculumId ?? ''}
              onChange={e => onChange({ curriculumId: e.target.value ? Number(e.target.value) : undefined })}
              disabled={filters.programId == null}
              className={`${SELECT_CLASS} disabled:opacity-50`}
            >
              <option value="">Tümü</option>
              {curricula.map(c => (
                <option key={c.id} value={c.id}>{c.name} ({c.version})</option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className={LABEL_CLASS}>Öğrenci No (Başlangıç)</span>
            <input
              type="text"
              value={filters.studentNoFrom}
              onChange={e => onChange({ studentNoFrom: e.target.value })}
              className={SELECT_CLASS}
            />
          </label>

          <label className="flex flex-col gap-1">
            <span className={LABEL_CLASS}>Öğrenci No (Bitiş)</span>
            <input
              type="text"
              value={filters.studentNoTo}
              onChange={e => onChange({ studentNoTo: e.target.value })}
              className={SELECT_CLASS}
            />
          </label>

          <label className="flex flex-col gap-1">
            <span className={LABEL_CLASS}>Kayıt Tarihi (Başlangıç)</span>
            <input
              type="date"
              value={filters.enrollmentFrom}
              onChange={e => onChange({ enrollmentFrom: e.target.value })}
              className={SELECT_CLASS}
            />
          </label>

          <label className="flex flex-col gap-1">
            <span className={LABEL_CLASS}>Kayıt Tarihi (Bitiş)</span>
            <input
              type="date"
              value={filters.enrollmentTo}
              onChange={e => onChange({ enrollmentTo: e.target.value })}
              className={SELECT_CLASS}
            />
          </label>

          <label className="flex flex-col gap-1">
            <span className={LABEL_CLASS}>Danışman</span>
            <select
              value={filters.advisorId ?? ''}
              onChange={e => onChange({ advisorId: e.target.value ? Number(e.target.value) : undefined })}
              className={SELECT_CLASS}
            >
              <option value="">Tümü</option>
              {advisors.map(a => (
                <option key={a.id} value={a.id}>{a.fullName}</option>
              ))}
            </select>
          </label>

          <label className="flex items-center gap-2 self-end pb-2">
            <input
              type="checkbox"
              checked={filters.hasFailingCourse}
              onChange={e => onChange({ hasFailingCourse: e.target.checked })}
              className="h-4 w-4 rounded border-input"
            />
            <span className="text-sm text-foreground">FF/NA dersi olan</span>
          </label>

          <div className="flex items-end pb-1">
            <Button variant="ghost" size="sm" onClick={onClear}>
              <X className="h-4 w-4 mr-1" />
              Temizle
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
