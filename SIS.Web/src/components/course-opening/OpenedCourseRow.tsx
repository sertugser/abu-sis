import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { AlertCircle, CheckCircle2, ChevronDown, Edit2, Layers, Plus, RotateCcw, Trash2, X, XCircle } from 'lucide-react';
import type { OpenedCourseSectionDto, InstructorOptionDto, InstructorWorkloadDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';
import { WorkloadBar } from './WorkloadBar';
import { AccessPolicyPopover } from './AccessPolicyPopover';

interface Props {
  courseCode: string; courseName: string; courseId: number;
  termId: number;
  sections: OpenedCourseSectionDto[];
  forecast: CourseDemandForecastDto | undefined;
  instructors: InstructorOptionDto[];
  onUpdateSection: (sectionId: number, capacity: number, instructorId: number | undefined, internalQuota?: number | null, externalQuota?: number | null) => void;
  onCloseSection: (section: OpenedCourseSectionDto) => void;
  onCloseAllSections: (courseId: number) => Promise<unknown>;
  onReactivateSection: (sectionId: number) => Promise<unknown>;
  onAddSection: (courseId: number, capacity: number, instructorId: number | undefined, internalQuota?: number, externalQuota?: number) => void;
  updatePending: boolean; closePending: boolean; closeError: Error | null;
  workloadData: InstructorWorkloadDto | undefined;
  onInstructorSelect: (id: number | null) => void;
}

function InstructorSelect({ instructors, value, onChange }: { instructors: InstructorOptionDto[]; value: string; onChange: (v: string) => void }) {
  return (
    <select className="h-7 w-full rounded-lg border border-gray-200 bg-white px-2 text-xs" value={value} onChange={e => onChange(e.target.value)}>
      <option value="">Hoca Seçiniz</option>
      {instructors.map(i => <option key={i.instructorId} value={i.instructorId}>{i.fullName}</option>)}
    </select>
  );
}

export function OpenedCourseRow({ courseCode, courseName, courseId, termId, sections, forecast, instructors, onUpdateSection, onCloseSection, onCloseAllSections, onReactivateSection, onAddSection, updatePending, closePending, closeError, workloadData, onInstructorSelect }: Props) {
  const [expanded, setExpanded] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editCap, setEditCap] = useState('');
  const [editInternalQuota, setEditInternalQuota] = useState('');
  const [editExternalQuota, setEditExternalQuota] = useState('');
  const [editInsId, setEditInsId] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<OpenedCourseSectionDto | null>(null);
  const [closeAllOpen, setCloseAllOpen] = useState(false);
  const [closeAllPending, setCloseAllPending] = useState(false);
  const [closeAllError, setCloseAllError] = useState<string | null>(null);
  const [reactivating, setReactivating] = useState<number | null>(null);
  const [adding, setAdding] = useState(false);
  const [newCap, setNewCap] = useState('40');
  const [newInternalQuota, setNewInternalQuota] = useState('');
  const [newExternalQuota, setNewExternalQuota] = useState('');
  const [newInsId, setNewInsId] = useState('');

  const totCap = sections.reduce((s, sec) => s + sec.capacity, 0);
  const totEnr = sections.reduce((s, sec) => s + sec.enrolledCount, 0);
  const rec = forecast?.recommendedSectionCount ?? 0;

  const startEdit = (sec: OpenedCourseSectionDto) => {
    setEditingId(sec.sectionId); setEditCap(String(sec.capacity));
    setEditInternalQuota(sec.internalQuota != null ? String(sec.internalQuota) : '');
    setEditExternalQuota(sec.externalQuota != null ? String(sec.externalQuota) : '');
    setEditInsId(sec.instructorId?.toString() ?? ''); onInstructorSelect(sec.instructorId ?? null);
  };

  const getQuotaWarning = (cap: string, intQ: string, extQ: string) => {
    const c = Number(cap) || 0;
    const i = intQ ? Number(intQ) : 0;
    const e = extQ ? Number(extQ) : 0;
    if ((intQ || extQ) && (i + e) > c) return `İç (${i}) + Dış (${e}) > Kontenjan (${c})`;
    return null;
  };

  const editQuotaWarning = getQuotaWarning(editCap, editInternalQuota, editExternalQuota);
  const newQuotaWarning = getQuotaWarning(newCap, newInternalQuota, newExternalQuota);

  const saveEdit = () => {
    if (editingId !== null) {
      onUpdateSection(editingId, Number(editCap), editInsId ? Number(editInsId) : undefined,
        editInternalQuota ? Number(editInternalQuota) : null, editExternalQuota ? Number(editExternalQuota) : null);
      setEditingId(null);
    }
  };

  const addSection = () => {
    onAddSection(courseId, Number(newCap), newInsId ? Number(newInsId) : undefined,
      newInternalQuota ? Number(newInternalQuota) : undefined, newExternalQuota ? Number(newExternalQuota) : undefined);
    setAdding(false); setNewCap('40'); setNewInternalQuota(''); setNewExternalQuota(''); setNewInsId('');
  };

  return (
    <div className="border-b last:border-b-0">
      {/* ── Summary Row ── */}
      <div
        className={cn(
          'flex items-center gap-2 px-3 py-2.5 cursor-pointer transition-colors group',
          expanded ? 'bg-blue-50/40' : 'hover:bg-gray-50/80',
        )}
        onClick={() => setExpanded(!expanded)}
      >
        <ChevronDown className={cn(
          'h-3.5 w-3.5 text-gray-400 transition-transform shrink-0 group-hover:text-gray-600',
          !expanded && '-rotate-90',
        )} />

        {/* Course info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-mono text-xs font-bold text-blue-700 shrink-0">{courseCode}</span>
            <span className="text-sm text-gray-800 truncate">{courseName}</span>
          </div>
          {/* Metrics as inline pills */}
          <div className="flex items-center gap-1.5 mt-1">
            <span className="inline-flex items-center gap-0.5 text-[10px] text-gray-500">
              <Layers className="h-2.5 w-2.5" />{sections.length} şube
            </span>
            <span className="text-gray-300">·</span>
            <span className="text-[10px] text-gray-500 tabular-nums">Kont: <strong className="text-gray-700">{totCap}</strong></span>
            <span className="text-gray-300">·</span>
            <span className="text-[10px] text-gray-500 tabular-nums">Kayıt: <strong className={totEnr > 0 ? 'text-gray-700' : 'text-gray-400'}>{totEnr}</strong></span>
            {forecast && (
              <>
                <span className="text-gray-300">·</span>
                <span className={cn('text-[10px] tabular-nums font-medium', forecast.totalPredictedDemand > totCap ? 'text-red-600' : 'text-gray-500')}>
                  Talep: {forecast.totalPredictedDemand}
                </span>
              </>
            )}
          </div>
        </div>

        {/* Section/Rec badge + Actions */}
        <div className="flex items-center gap-1.5 shrink-0" onClick={e => e.stopPropagation()}>
          {rec > 0 && (
            <Badge
              variant={sections.length < rec ? 'destructive' : sections.length === rec ? 'success' : 'warning'}
              className="text-[10px] tabular-nums"
            >
              {sections.length}/{rec}
            </Badge>
          )}
          <AccessPolicyPopover courseId={courseId} termId={termId} courseDepartmentId={sections[0]?.departmentId ?? null} courseDepartmentName={sections[0]?.departmentName ?? null} />
          {sections.some(s => s.isActive) && (
            <TooltipProvider delayDuration={200}><Tooltip><TooltipTrigger asChild>
              <Button size="icon" variant="ghost" className="h-6 w-6 text-gray-400 hover:text-red-600 hover:bg-red-50" onClick={() => { setCloseAllError(null); setCloseAllOpen(true); }}>
                <XCircle className="h-3.5 w-3.5" />
              </Button>
            </TooltipTrigger><TooltipContent side="top" className="text-xs">Dersi Kapat</TooltipContent></Tooltip></TooltipProvider>
          )}
        </div>
      </div>

      {/* ── Expanded: Section List ── */}
      {expanded && (
        <div className="bg-gray-50/50 border-t">
          {/* Section header */}
          <div className="flex items-center gap-3 px-6 py-1.5 text-[10px] font-medium text-gray-400 border-b">
            <span className="w-8 text-center">#</span>
            <span className="w-20">Kontenjan</span>
            <span className="flex-1">Öğretim Üyesi</span>
            <span className="w-16" />
          </div>

          {sections.map(sec => (
            <div key={sec.sectionId} className={cn(
              'flex items-center gap-3 px-6 py-2 border-b last:border-b-0 group',
              !sec.isActive && 'opacity-40',
            )}>
              <span className="font-mono text-xs w-8 text-center text-gray-500 shrink-0">{sec.sectionNumber}</span>

              {editingId === sec.sectionId ? (
                /* ── Edit mode ── */
                <>
                  <div className="flex-1 min-w-0 space-y-1.5">
                    <div className="flex items-center gap-1.5 flex-wrap">
                      <FieldInput label="Kont." value={editCap} onChange={setEditCap} />
                      <FieldInput label="İç" value={editInternalQuota} onChange={setEditInternalQuota} placeholder="—" />
                      <FieldInput label="Dış" value={editExternalQuota} onChange={setEditExternalQuota} placeholder="—" />
                    </div>
                    {editQuotaWarning && (
                      <div className="text-[10px] text-amber-600 flex items-center gap-1">
                        <AlertCircle className="h-3 w-3 shrink-0" />{editQuotaWarning}
                      </div>
                    )}
                    <InstructorSelect instructors={instructors} value={editInsId} onChange={v => { setEditInsId(v); onInstructorSelect(v ? Number(v) : null); }} />
                    {workloadData && <WorkloadBar workload={workloadData} />}
                  </div>
                  <div className="flex gap-0.5 shrink-0">
                    <Button size="icon" variant="ghost" className="h-6 w-6" onClick={saveEdit} disabled={updatePending}><CheckCircle2 className="h-3 w-3 text-green-600" /></Button>
                    <Button size="icon" variant="ghost" className="h-6 w-6" onClick={() => setEditingId(null)}><X className="h-3 w-3" /></Button>
                  </div>
                </>
              ) : (
                /* ── View mode ── */
                <>
                  <TooltipProvider delayDuration={200}><Tooltip><TooltipTrigger asChild>
                    <span className="text-xs font-mono w-20 shrink-0 cursor-default tabular-nums">
                      <strong>{sec.capacity}</strong>
                      <span className="text-gray-400"> / {sec.enrolledCount}</span>
                    </span>
                  </TooltipTrigger><TooltipContent side="top" className="text-xs space-y-0.5">
                    <p>Kontenjan: {sec.capacity}</p>
                    {sec.internalQuota != null && <p>Bölüm İçi: {sec.internalQuota}</p>}
                    {sec.externalQuota != null && <p>Bölüm Dışı: {sec.externalQuota}</p>}
                    <p>Doluluk: {sec.enrolledCount}</p>
                    <p>Kayıtlı: {sec.registeredCount}</p>
                  </TooltipContent></Tooltip></TooltipProvider>

                  <span className="text-xs flex-1 min-w-0 truncate text-gray-700">
                    {sec.instructorName ?? <span className="text-gray-400 italic">Atanmadı</span>}
                  </span>

                  <div className="flex gap-0.5 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
                    {sec.isActive ? (
                      <>
                        <Button size="icon" variant="ghost" className="h-6 w-6 text-gray-400 hover:text-gray-700" onClick={() => startEdit(sec)}><Edit2 className="h-3 w-3" /></Button>
                        <Button size="icon" variant="ghost" className="h-6 w-6 text-gray-400 hover:text-red-600" onClick={() => setDeleteTarget(sec)}><Trash2 className="h-3 w-3" /></Button>
                      </>
                    ) : (
                      <TooltipProvider delayDuration={200}><Tooltip><TooltipTrigger asChild>
                        <Button
                          size="icon" variant="ghost"
                          className="h-6 w-6 text-green-600 hover:text-green-700 hover:bg-green-50"
                          disabled={reactivating === sec.sectionId}
                          onClick={() => { setReactivating(sec.sectionId); onReactivateSection(sec.sectionId).finally(() => setReactivating(null)); }}
                        >
                          <RotateCcw className={cn('h-3 w-3', reactivating === sec.sectionId && 'animate-spin')} />
                        </Button>
                      </TooltipTrigger><TooltipContent side="top" className="text-xs">Yeniden Aç</TooltipContent></Tooltip></TooltipProvider>
                    )}
                  </div>
                </>
              )}
            </div>
          ))}

          {/* Add section */}
          {adding ? (
            <div className="flex flex-col gap-1.5 px-6 py-2.5 border-t bg-blue-50/40">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-xs w-8 text-center text-blue-500 font-bold shrink-0">+</span>
                <FieldInput label="Kont." value={newCap} onChange={setNewCap} />
                <FieldInput label="İç" value={newInternalQuota} onChange={setNewInternalQuota} placeholder="—" />
                <FieldInput label="Dış" value={newExternalQuota} onChange={setNewExternalQuota} placeholder="—" />
                <div className="flex-1 min-w-[140px]">
                  <InstructorSelect instructors={instructors} value={newInsId} onChange={setNewInsId} />
                </div>
                <Button size="sm" className="h-7 text-xs rounded-lg" onClick={addSection}>Ekle</Button>
                <Button size="sm" variant="ghost" className="h-7 text-xs" onClick={() => setAdding(false)}><X className="h-3.5 w-3.5" /></Button>
              </div>
              {newQuotaWarning && (
                <div className="text-[10px] text-amber-600 flex items-center gap-1 ml-10">
                  <AlertCircle className="h-3 w-3 shrink-0" />{newQuotaWarning}
                </div>
              )}
            </div>
          ) : (
            <button className="w-full flex items-center gap-2 px-6 py-2 text-xs text-blue-600 hover:bg-blue-50/60 transition-colors border-t font-medium" onClick={() => setAdding(true)}>
              <Plus className="h-3 w-3" /> Şube Ekle
            </button>
          )}

          {closeError && (
            <Alert variant="destructive" className="mx-4 my-2 rounded-lg">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{(closeError as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Şube kapatılamadı.'}</AlertDescription>
            </Alert>
          )}
        </div>
      )}

      {/* ── Close Section Dialog ── */}
      <Dialog open={deleteTarget !== null} onOpenChange={() => setDeleteTarget(null)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader><DialogTitle>Şubeyi Kapat</DialogTitle></DialogHeader>
          <div className="space-y-2 text-sm text-gray-600">
            <p><strong>{deleteTarget?.courseCode} — Şube {deleteTarget?.sectionNumber}</strong> kapatılacak.</p>
            {deleteTarget && <p>{deleteTarget.draftOrPendingCount > 0 ? `${deleteTarget.draftOrPendingCount} adet Draft/Pending kayıt Dropped durumuna düşecek.` : 'Düşürülecek kayıt bulunmuyor.'}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>Vazgeç</Button>
            <Button variant="destructive" onClick={() => { if (deleteTarget) { onCloseSection(deleteTarget); setDeleteTarget(null); } }} disabled={closePending}>{closePending ? 'Kapatılıyor...' : 'Kapat'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Close All Dialog ── */}
      <Dialog open={closeAllOpen} onOpenChange={() => setCloseAllOpen(false)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader><DialogTitle>Dersi Kapat</DialogTitle></DialogHeader>
          <div className="space-y-2 text-sm text-gray-600">
            <p><strong>{courseCode} — {courseName}</strong> dersinin tüm <strong>{sections.filter(s => s.isActive).length} aktif şubesi</strong> kapatılacak.</p>
            <p>Draft/Pending durumdaki kayıtlar otomatik olarak Dropped'a düşecek.</p>
            {closeAllError && (
              <Alert variant="destructive" className="mt-2 rounded-lg"><AlertCircle className="h-4 w-4" /><AlertDescription>{closeAllError}</AlertDescription></Alert>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloseAllOpen(false)}>Vazgeç</Button>
            <Button
              variant="destructive"
              disabled={closeAllPending}
              onClick={() => {
                setCloseAllPending(true); setCloseAllError(null);
                onCloseAllSections(courseId)
                  .then(() => setCloseAllOpen(false))
                  .catch((err: { response?: { data?: { error?: string } } }) => setCloseAllError(err?.response?.data?.error ?? 'Ders kapatılamadı.'))
                  .finally(() => setCloseAllPending(false));
              }}
            >
              {closeAllPending ? 'Kapatılıyor...' : 'Tüm Şubeleri Kapat'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

/* ── Compact Field Input ── */

function FieldInput({ label, value, onChange, placeholder }: { label: string; value: string; onChange: (v: string) => void; placeholder?: string }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-[9px] text-gray-400 font-medium">{label}</span>
      <Input className="h-7 w-14 text-xs rounded-md" value={value} onChange={e => onChange(e.target.value)} type="number" min={0} max={500} placeholder={placeholder} />
    </div>
  );
}
