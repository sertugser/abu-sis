import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { TableCell, TableRow } from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { AlertTriangle, Check, Loader2, Lock, X } from 'lucide-react';
import { useAvailableClassrooms, useValidateSectionSchedule } from '../../hooks/useFacultyPlanning';
import { useDepartmentInstructors } from '../../hooks/useCourseOpening';
import type { FacultyPlanningSection, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';
import type { ScheduleValidationResult } from '../../types/scheduling';
import { STATUS_CONFIG, buildManualScheduleOptions } from './sectionUtils';

const DAY_LABELS: Record<number, string> = { 1: 'Pzt', 2: 'Sal', 3: 'Çar', 4: 'Per', 5: 'Cum' };

interface Props {
  section: FacultyPlanningSection;
  termId: number | null;
  timeSlots: FacultyPlanningTimeSlot[];
  timeSlotsLoaded: boolean;
  onAssign: (classroomId?: number, slots?: { day: number; startTime: string; endTime: string }[], instructorId?: number) => void;
  onUnassign: () => void;
  isAssigning: boolean;
  isUnassigning: boolean;
  readOnly: boolean;
  even: boolean;
}

export function InlineSectionRow({
  section: s, termId, timeSlots, timeSlotsLoaded,
  onAssign, onUnassign, isAssigning, isUnassigning, readOnly, even,
}: Props) {
  const isUnscheduled = s.status === 'Unscheduled';
  const editable = !readOnly && isUnscheduled;

  const [classroomId, setClassroomId] = useState<string>(s.classroomId?.toString() ?? '');
  const [instructorId, setInstructorId] = useState<string>(s.instructorId?.toString() ?? '');
  const [day, setDay] = useState('1');
  const [selectedSlotKey, setSelectedSlotKey] = useState('');
  const [localError, setLocalError] = useState('');
  const [validationResult, setValidationResult] = useState<ScheduleValidationResult | null>(null);

  const validateMutation = useValidateSectionSchedule();
  const requiredSlotCount = s.theoryHours + s.labHours || 1;
  const slotOptions = buildManualScheduleOptions(timeSlots, requiredSlotCount);
  const availableDays = Array.from(new Set(slotOptions.map(o => o.day))).sort((a, b) => a - b);
  const effectiveDay = availableDays.includes(Number(day)) ? Number(day) : (availableDays[0] ?? Number(day));
  const dayOptions = slotOptions.filter(o => o.day === effectiveDay);
  const selectedOption = dayOptions.find(o => o.key === selectedSlotKey) ?? dayOptions[0] ?? null;

  const statusCfg = STATUS_CONFIG[s.status] ?? STATUS_CONFIG.Unscheduled;
  const instructorsQuery = useDepartmentInstructors(s.departmentId);
  const expectedType = s.labHours > 0 && s.theoryHours === 0 ? 'Lab' : 'Lecture';
  const classroomsQuery = useAvailableClassrooms(
    termId, selectedOption?.day, selectedOption?.startTime, selectedOption?.endTime, s.capacity, expectedType,
  );

  const handleAssignClick = () => {
    if (!classroomId.trim()) { setLocalError('Derslik seçin'); return; }
    if (!selectedOption) { setLocalError('Saat seçin'); return; }
    setLocalError('');
    const slots = [{ day: selectedOption.day, startTime: selectedOption.startTime, endTime: selectedOption.endTime }];
    validateMutation.mutate({
      sectionId: s.sectionId, classroomId: Number(classroomId),
      instructorId: instructorId ? Number(instructorId) : undefined, scheduleSlots: slots,
    }, {
      onSuccess: (result) => {
        if (result.isValid || result.conflicts.length === 0) {
          onAssign(Number(classroomId), slots, instructorId ? Number(instructorId) : undefined);
        } else {
          setValidationResult(result);
        }
      },
      onError: () => setLocalError('Doğrulama başarısız'),
    });
  };

  // Status-based left border accent
  const borderAccent = s.status === 'Scheduled' ? 'border-l-2 border-l-emerald-400'
    : s.status === 'Conflicted' ? 'border-l-2 border-l-red-400'
    : s.status === 'Partial' ? 'border-l-2 border-l-amber-400'
    : 'border-l-2 border-l-transparent';

  const rowBg = even ? 'bg-slate-50/60' : 'bg-white';

  return (
    <>
      <TableRow className={`${rowBg} ${borderAccent} hover:bg-blue-50/50 transition-colors`}>
        {/* Ders — kompakt 2 satır */}
        <TableCell className="py-2.5 pl-4 pr-2">
          <div className="flex items-center gap-2 min-w-0">
            <div className="min-w-0 flex-1">
              <div className="flex items-baseline gap-1.5 min-w-0">
                <span className="font-mono text-xs font-bold text-[#003366] shrink-0">{s.courseCode}</span>
                <span className="text-sm text-slate-800 truncate">{s.courseName}</span>
              </div>
              <div className="flex items-center gap-1.5 text-[11px] text-slate-400 mt-0.5">
                <span className="bg-slate-100 text-slate-600 px-1.5 py-0.5 rounded font-medium">{s.departmentCode}</span>
                <span>Ş.{s.sectionIndex}</span>
                <span>·</span>
                <span>{s.theoryHours}T+{s.labHours}L</span>
                <span>·</span>
                <span>{s.capacity} kişi</span>
                {s.isPinned && (
                  <TooltipProvider delayDuration={100}>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <Lock className="h-3 w-3 text-amber-500" />
                      </TooltipTrigger>
                      <TooltipContent className="text-xs">Sabitlenmiş</TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                )}
              </div>
            </div>
          </div>
        </TableCell>

        {/* Derslik */}
        <TableCell className="py-2 px-2">
          {editable ? (
            <select
              className="h-7 w-full max-w-[160px] rounded border border-input bg-background px-1.5 text-[11px] focus:ring-1 focus:ring-primary"
              value={classroomId}
              onChange={e => setClassroomId(e.target.value)}
            >
              <option value="">Derslik seç...</option>
              {(classroomsQuery.data ?? []).map(cr => (
                <option key={cr.classroomId} value={cr.classroomId}>
                  {cr.name} ({cr.capacity})
                </option>
              ))}
            </select>
          ) : (
            <span className="text-[13px] font-medium">{s.classroomName ?? <span className="text-muted-foreground">—</span>}</span>
          )}
        </TableCell>

        {/* Gün + Saat */}
        <TableCell className="py-2 px-2">
          {editable ? (
            <div className="flex gap-1">
              <select
                className="h-7 w-[52px] rounded border border-input bg-background px-1 text-[11px] focus:ring-1 focus:ring-primary"
                value={String(effectiveDay)}
                onChange={e => setDay(e.target.value)}
                disabled={!timeSlotsLoaded}
              >
                {Object.entries(DAY_LABELS).map(([v, l]) => (
                  <option key={v} value={v} disabled={!availableDays.includes(Number(v))}>{l}</option>
                ))}
              </select>
              <select
                className="h-7 flex-1 min-w-[100px] rounded border border-input bg-background px-1 text-[11px] focus:ring-1 focus:ring-primary"
                value={selectedOption?.key ?? ''}
                onChange={e => setSelectedSlotKey(e.target.value)}
                disabled={!timeSlotsLoaded || dayOptions.length === 0}
              >
                {dayOptions.length === 0
                  ? <option value="">—</option>
                  : dayOptions.map(o => <option key={o.key} value={o.key}>{o.label}</option>)}
              </select>
            </div>
          ) : (
            <span className="text-[12px] font-mono">{s.scheduleSummary || <span className="text-muted-foreground">—</span>}</span>
          )}
        </TableCell>

        {/* Hoca */}
        <TableCell className="py-2 px-2">
          {editable ? (
            <select
              className="h-7 w-full max-w-[160px] rounded border border-input bg-background px-1.5 text-[11px] focus:ring-1 focus:ring-primary"
              value={instructorId}
              onChange={e => setInstructorId(e.target.value)}
              disabled={instructorsQuery.isLoading}
            >
              <option value="">Hoca seç...</option>
              {(instructorsQuery.data ?? []).map(i => (
                <option key={i.instructorId} value={i.instructorId}>{i.fullName}</option>
              ))}
            </select>
          ) : (
            <span className="text-[13px]">{s.instructorName ?? <span className="text-muted-foreground">—</span>}</span>
          )}
        </TableCell>

        {/* Durum */}
        <TableCell className="py-2 px-2 w-[110px]">
          <Badge variant={statusCfg.variant} className="text-[10px] font-normal">{statusCfg.label}</Badge>
        </TableCell>

        {/* Aksiyon */}
        <TableCell className="py-2 px-1 w-10 text-center">
          {!readOnly && (
            isUnscheduled ? (
              <Button
                size="icon" variant="ghost"
                className="h-7 w-7 rounded-full hover:bg-emerald-100"
                disabled={isAssigning || validateMutation.isPending}
                onClick={handleAssignClick}
                title="Ata"
              >
                {validateMutation.isPending
                  ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  : <Check className="h-3.5 w-3.5 text-emerald-600" />}
              </Button>
            ) : !s.isPinned ? (
              <Button
                size="icon" variant="ghost"
                className="h-7 w-7 rounded-full hover:bg-red-100"
                disabled={isUnassigning}
                onClick={onUnassign}
                title="Kaldır"
              >
                {isUnassigning
                  ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  : <X className="h-3.5 w-3.5 text-red-500" />}
              </Button>
            ) : null
          )}
        </TableCell>
      </TableRow>

      {/* Çakışma banner */}
      {validationResult && validationResult.conflicts.length > 0 && (
        <TableRow className="bg-amber-50/80">
          <TableCell colSpan={6} className="py-2 px-4">
            <div className="flex items-center gap-2 text-xs">
              <AlertTriangle className="h-3.5 w-3.5 text-amber-600 shrink-0" />
              <span className="text-amber-800 flex-1">
                {validationResult.conflicts.map(c => `${c.courseCode}: ${c.message}`).join(' · ')}
              </span>
              <Button size="sm" variant="ghost" className="h-6 text-xs px-2" onClick={() => setValidationResult(null)}>İptal</Button>
              <Button size="sm" variant="destructive" className="h-6 text-xs px-2" onClick={() => {
                const slots = [{ day: selectedOption!.day, startTime: selectedOption!.startTime, endTime: selectedOption!.endTime }];
                onAssign(Number(classroomId), slots, instructorId ? Number(instructorId) : undefined);
                setValidationResult(null);
              }}>Yine de Ata</Button>
            </div>
          </TableCell>
        </TableRow>
      )}

      {/* Hata banner */}
      {localError && (
        <TableRow>
          <TableCell colSpan={6} className="py-1 px-4 bg-red-50 text-xs text-red-700">{localError}</TableCell>
        </TableRow>
      )}
    </>
  );
}
