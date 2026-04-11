import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { TableCell, TableRow } from '@/components/ui/table';
import { AlertTriangle, Check, X } from 'lucide-react';
import { useAvailableClassrooms, useValidateSectionSchedule } from '../../hooks/useFacultyPlanning';
import { useDepartmentInstructors } from '../../hooks/useCourseOpening';
import type { FacultyPlanningSection, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';
import type { ScheduleValidationResult } from '../../types/scheduling';
import { STATUS_CONFIG, buildManualScheduleOptions } from './sectionUtils';

const WEEKDAY_OPTIONS = [
  { value: 1, label: 'Pzt' },
  { value: 2, label: 'Sal' },
  { value: 3, label: 'Çar' },
  { value: 4, label: 'Per' },
  { value: 5, label: 'Cum' },
];

export function SectionRow({ section: s, termId, timeSlots, timeSlotsLoaded, expanded, onToggle, onAssign, onUnassign, isAssigning, isUnassigning }: {
  section: FacultyPlanningSection;
  termId: number | null;
  timeSlots: FacultyPlanningTimeSlot[];
  timeSlotsLoaded: boolean;
  expanded: boolean;
  onToggle: () => void;
  onAssign: (classroomId?: number, slots?: { day: number; startTime: string; endTime: string }[], instructorId?: number) => void;
  onUnassign: () => void;
  isAssigning: boolean;
  isUnassigning: boolean;
}) {
  const [classroomId, setClassroomId] = useState<string>(s.classroomId?.toString() ?? '');
  const [instructorId, setInstructorId] = useState<string>(s.instructorId?.toString() ?? '');
  const [day, setDay] = useState('1');
  const [selectedSlotKey, setSelectedSlotKey] = useState('');
  const [localError, setLocalError] = useState<string>('');
  const [validationResult, setValidationResult] = useState<ScheduleValidationResult | null>(null);
  const validateMutation = useValidateSectionSchedule();
  const requiredSlotCount = s.theoryHours + s.labHours || 1;
  const slotOptions = buildManualScheduleOptions(timeSlots, requiredSlotCount);
  const availableDays = Array.from(new Set(slotOptions.map(option => option.day))).sort((left, right) => left - right);
  const effectiveDay = availableDays.includes(Number(day)) ? Number(day) : (availableDays[0] ?? Number(day));
  const dayOptions = slotOptions.filter(option => option.day === effectiveDay);
  const selectedOption = dayOptions.find(option => option.key === selectedSlotKey) ?? dayOptions[0] ?? null;

  const statusCfg = STATUS_CONFIG[s.status] ?? STATUS_CONFIG.Unscheduled;
  const instructorsQuery = useDepartmentInstructors(s.departmentId);
  const availableClassroomsQuery = useAvailableClassrooms(
    termId,
    selectedOption ? selectedOption.day : undefined,
    selectedOption?.startTime,
    selectedOption?.endTime,
    s.capacity,
  );
  const availableClassrooms = availableClassroomsQuery.data;

  return (
    <>
      <TableRow className="cursor-pointer hover:bg-muted/30" onClick={onToggle}>
        <TableCell>
          <div>
            <span className="font-mono text-xs font-semibold text-primary">{s.courseCode}</span>
            <span className="text-sm ml-2">{s.courseName}</span>
            <span className="text-xs text-muted-foreground ml-1">Şube {s.sectionIndex}</span>
          </div>
        </TableCell>
        <TableCell>{s.departmentCode}</TableCell>
        <TableCell>{s.capacity}</TableCell>
        <TableCell>{s.classroomName ?? '—'}</TableCell>
        <TableCell>{s.scheduleSummary || '—'}</TableCell>
        <TableCell>{s.instructorName ?? '—'}</TableCell>
        <TableCell><Badge variant={statusCfg.variant}>{statusCfg.label}</Badge></TableCell>
      </TableRow>
      {expanded && (
        <TableRow className="bg-muted/20">
          <TableCell colSpan={7} className="p-4">
            <div className="flex flex-wrap items-end gap-4">
              <div className="space-y-1">
                <label className="text-xs font-medium">Derslik</label>
                <select
                  className="h-8 w-52 rounded-md border px-2 text-sm"
                  value={classroomId}
                  onChange={e => setClassroomId(e.target.value)}
                  onClick={e => e.stopPropagation()}
                >
                  <option value="">Derslik seç...</option>
                  {availableClassrooms?.map(cr => (
                    <option key={cr.classroomId} value={cr.classroomId}>
                      {cr.name} ({cr.capacity} kişi, {cr.type})
                    </option>
                  ))}
                </select>
                {classroomId && availableClassrooms && !availableClassrooms.some(cr => String(cr.classroomId) === classroomId) && (
                  <p className="text-xs text-amber-600 mt-1">Seçili derslik bu saat aralığında müsait değil</p>
                )}
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Hoca</label>
                <select
                  className="h-8 min-w-52 rounded-md border px-2 text-sm"
                  value={instructorId}
                  onChange={e => setInstructorId(e.target.value)}
                  onClick={e => e.stopPropagation()}
                  disabled={instructorsQuery.isLoading || instructorsQuery.isError}
                >
                  <option value="">Hoca atanmadı</option>
                  {(instructorsQuery.data ?? []).map(instructor => (
                    <option key={instructor.instructorId} value={instructor.instructorId}>
                      {instructor.fullName}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Gün</label>
                <select
                  className="h-8 rounded-md border px-2 text-sm"
                  value={String(effectiveDay)}
                  onChange={e => setDay(e.target.value)}
                  onClick={e => e.stopPropagation()}
                  disabled={!timeSlotsLoaded || availableDays.length === 0}
                >
                  {WEEKDAY_OPTIONS.map(option => (
                    <option key={option.value} value={option.value} disabled={!availableDays.includes(option.value)}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Saat</label>
                <select
                  className="h-8 rounded-md border px-2 text-sm"
                  value={selectedOption?.key ?? ''}
                  onChange={e => setSelectedSlotKey(e.target.value)}
                  onClick={e => e.stopPropagation()}
                  disabled={!timeSlotsLoaded || dayOptions.length === 0}
                >
                  {dayOptions.length === 0 ? (
                    <option value="">Uygun saat aralığı yok</option>
                  ) : (
                    dayOptions.map(option => (
                      <option key={option.key} value={option.key}>{option.label}</option>
                    ))
                  )}
                </select>
              </div>
              <div className="flex gap-2">
                <Button size="sm" type="button" disabled={isAssigning || validateMutation.isPending} onClick={(e) => {
                  e.stopPropagation();
                  if (!classroomId.trim()) {
                    setLocalError('Atama için derslik seçilmelidir.');
                    return;
                  }
                  if (!timeSlotsLoaded) {
                    setLocalError('Akademik saat seçenekleri henüz yüklenmedi.');
                    return;
                  }
                  if (!selectedOption) {
                    setLocalError(`${requiredSlotCount} slot kapsayan uygun bir saat aralığı bulunamadı.`);
                    return;
                  }
                  setLocalError('');
                  const slots = [{ day: selectedOption.day, startTime: selectedOption.startTime, endTime: selectedOption.endTime }];
                  validateMutation.mutate({
                    sectionId: s.sectionId,
                    classroomId: Number(classroomId),
                    instructorId: instructorId ? Number(instructorId) : undefined,
                    scheduleSlots: slots,
                  }, {
                    onSuccess: (result) => {
                      if (result.isValid || result.conflicts.length === 0) {
                        onAssign(Number(classroomId), slots, instructorId ? Number(instructorId) : undefined);
                      } else {
                        setValidationResult(result);
                      }
                    },
                    onError: () => {
                      // Validation endpoint başarısız olursa doğrudan atama yap
                      onAssign(Number(classroomId), slots, instructorId ? Number(instructorId) : undefined);
                    },
                  });
                }}>
                  {validateMutation.isPending ? 'Kontrol...' : <><Check className="h-4 w-4 mr-1" /> Ata</>}
                </Button>
                {s.status !== 'Unscheduled' && (
                  <Button size="sm" type="button" variant="destructive" disabled={isUnassigning} onClick={(e) => { e.stopPropagation(); onUnassign(); }}>
                    <X className="h-4 w-4 mr-1" /> Kaldır
                  </Button>
                )}
              </div>
            </div>
            <p className="mt-2 text-xs text-muted-foreground">Bu şube için gerekli haftalık yük: {requiredSlotCount} akademik slot.</p>
            {!instructorId && (
              <p className="mt-1 text-xs text-amber-700">
                Hoca atanmadıysa instructor bazlı çakışmalar hesaplanmaz. 5 güne yayılma uyarısı için burada hoca seçmelisin.
              </p>
            )}
            {instructorsQuery.isError && (
              <p className="mt-1 text-xs text-destructive">Bölüm hoca listesi yüklenemedi.</p>
            )}
            {localError && <p className="mt-2 text-xs text-destructive">{localError}</p>}
            {validationResult && validationResult.conflicts.length > 0 && (
              <div className="mt-3 rounded-md border border-amber-200 bg-amber-50 p-3">
                <div className="flex items-center gap-2 mb-2">
                  <AlertTriangle className="h-4 w-4 text-amber-600" />
                  <span className="text-sm font-medium text-amber-700">
                    {validationResult.conflicts.length} çakışma tespit edildi
                  </span>
                </div>
                <ul className="space-y-1 mb-3">
                  {validationResult.conflicts.map((c, i) => (
                    <li key={i} className="text-xs text-amber-700">
                      <span className="font-mono font-bold">{c.courseCode}</span> — {c.message}
                    </li>
                  ))}
                </ul>
                <div className="flex gap-2">
                  <Button size="sm" variant="outline" onClick={(e) => { e.stopPropagation(); setValidationResult(null); }}>
                    İptal
                  </Button>
                  <Button size="sm" variant="destructive" onClick={(e) => {
                    e.stopPropagation();
                    const slots = [{ day: selectedOption!.day, startTime: selectedOption!.startTime, endTime: selectedOption!.endTime }];
                    onAssign(Number(classroomId), slots, instructorId ? Number(instructorId) : undefined);
                    setValidationResult(null);
                  }}>
                    Yine de Ata
                  </Button>
                </div>
              </div>
            )}
          </TableCell>
        </TableRow>
      )}
    </>
  );
}
