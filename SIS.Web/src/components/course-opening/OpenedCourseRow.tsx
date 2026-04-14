import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { AlertCircle, CheckCircle2, ChevronDown, ChevronRight, Edit2, Plus, RotateCcw, Trash2, X, XCircle } from 'lucide-react';
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
    <select className="h-7 w-full rounded border border-input bg-background px-1 text-xs" value={value} onChange={e => onChange(e.target.value)}>
      <option value="">Seçiniz</option>
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

  const editQuotaWarning = (() => {
    const cap = Number(editCap) || 0;
    const intQ = editInternalQuota ? Number(editInternalQuota) : 0;
    const extQ = editExternalQuota ? Number(editExternalQuota) : 0;
    if ((editInternalQuota || editExternalQuota) && (intQ + extQ) > cap) {
      return `Bölüm içi (${intQ}) + bölüm dışı (${extQ}) kontenjan toplamı, toplam kontenjanı (${cap}) aşıyor.`;
    }
    return null;
  })();

  const newQuotaWarning = (() => {
    const cap = Number(newCap) || 0;
    const intQ = newInternalQuota ? Number(newInternalQuota) : 0;
    const extQ = newExternalQuota ? Number(newExternalQuota) : 0;
    if ((newInternalQuota || newExternalQuota) && (intQ + extQ) > cap) {
      return `Bölüm içi (${intQ}) + bölüm dışı (${extQ}) kontenjan toplamı, toplam kontenjanı (${cap}) aşıyor.`;
    }
    return null;
  })();

  const saveEdit = () => {
    if (editingId !== null) {
      onUpdateSection(
        editingId,
        Number(editCap),
        editInsId ? Number(editInsId) : undefined,
        editInternalQuota ? Number(editInternalQuota) : null,
        editExternalQuota ? Number(editExternalQuota) : null,
      );
      setEditingId(null);
    }
  };
  const addSection = () => {
    onAddSection(
      courseId,
      Number(newCap),
      newInsId ? Number(newInsId) : undefined,
      newInternalQuota ? Number(newInternalQuota) : undefined,
      newExternalQuota ? Number(newExternalQuota) : undefined,
    );
    setAdding(false); setNewCap('40'); setNewInternalQuota(''); setNewExternalQuota(''); setNewInsId('');
  };

  return (
    <div className="border-b last:border-b-0">
      <div className="flex items-center gap-1.5 px-3 py-2.5">
        <button className="flex-1 flex items-center gap-3 hover:bg-accent/30 transition-colors text-left rounded -m-1 p-1" onClick={() => setExpanded(!expanded)}>
          {expanded ? <ChevronDown className="h-3.5 w-3.5 text-muted-foreground shrink-0" /> : <ChevronRight className="h-3.5 w-3.5 text-muted-foreground shrink-0" />}
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <span className="font-mono text-xs font-bold text-[#003366]">{courseCode}</span>
              <span className="text-sm truncate">{courseName}</span>
            </div>
            <div className="flex items-center gap-3 mt-0.5 text-[11px] text-muted-foreground">
              <span>{sections.length} şube</span><span>Kont: {totCap}</span><span>Kayıt: {totEnr}</span>
              {forecast && <span>Talep: {forecast.totalPredictedDemand}</span>}
            </div>
          </div>
          {rec > 0 && (
            <Badge variant={sections.length < rec ? 'destructive' : sections.length === rec ? 'success' : 'warning'} className="text-[10px] shrink-0">
              {sections.length}/{rec}
            </Badge>
          )}
        </button>
        <div className="shrink-0 flex items-center gap-1" onClick={e => e.stopPropagation()}>
          <AccessPolicyPopover
            courseId={courseId}
            termId={termId}
            courseDepartmentId={sections[0]?.departmentId ?? null}
            courseDepartmentName={sections[0]?.departmentName ?? null}
          />
          {sections.some(s => s.isActive) && (
            <TooltipProvider delayDuration={200}><Tooltip><TooltipTrigger asChild>
              <Button size="icon" variant="ghost" className="h-7 w-7 text-gray-400 hover:text-red-600 hover:bg-red-50" onClick={() => { setCloseAllError(null); setCloseAllOpen(true); }}>
                <XCircle className="h-3.5 w-3.5" />
              </Button>
            </TooltipTrigger><TooltipContent side="top" className="text-xs">Dersi Kapat (Tüm Şubeler)</TooltipContent></Tooltip></TooltipProvider>
          )}
        </div>
      </div>

      {expanded && (
        <div className="bg-gray-50/50 border-t">
          {sections.map(sec => (
            <div key={sec.sectionId} className={`flex items-center gap-3 px-6 py-2 border-b last:border-b-0 text-sm group ${!sec.isActive ? 'opacity-50' : ''}`}>
              <span className="font-mono text-xs w-8 text-center shrink-0">{sec.sectionNumber}</span>
              {editingId === sec.sectionId ? (<>
                <div className="flex-1 min-w-0 space-y-1">
                  <div className="flex items-center gap-1.5 flex-wrap">
                    <div className="flex flex-col gap-0.5">
                      <span className="text-[10px] text-muted-foreground">Kontenjan</span>
                      <Input className="h-7 w-16 text-xs" value={editCap} onChange={e => setEditCap(e.target.value)} type="number" min={1} max={500} />
                    </div>
                    <div className="flex flex-col gap-0.5">
                      <span className="text-[10px] text-muted-foreground">Bölüm İçi</span>
                      <Input className="h-7 w-16 text-xs" placeholder="—" value={editInternalQuota} onChange={e => setEditInternalQuota(e.target.value)} type="number" min={0} max={500} />
                    </div>
                    <div className="flex flex-col gap-0.5">
                      <span className="text-[10px] text-muted-foreground">Bölüm Dışı</span>
                      <Input className="h-7 w-16 text-xs" placeholder="—" value={editExternalQuota} onChange={e => setEditExternalQuota(e.target.value)} type="number" min={0} max={500} />
                    </div>
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
              </>) : (<>
                <TooltipProvider delayDuration={200}><Tooltip><TooltipTrigger asChild>
                  <span className="text-xs font-mono w-24 shrink-0 cursor-default"><span className="font-semibold">{sec.capacity}</span><span className="text-muted-foreground"> / {sec.enrolledCount} / {sec.registeredCount}</span></span>
                </TooltipTrigger><TooltipContent side="top" className="text-xs"><p>Kontenjan: {sec.capacity}</p>{sec.internalQuota != null && <p>Bölüm İçi: {sec.internalQuota}</p>}{sec.externalQuota != null && <p>Bölüm Dışı: {sec.externalQuota}</p>}<p>Doluluk: {sec.enrolledCount}</p><p>Kayıtlı: {sec.registeredCount}</p></TooltipContent></Tooltip></TooltipProvider>
                <span className="text-xs flex-1 min-w-0 truncate">{sec.instructorName ?? <span className="text-muted-foreground">Atanmadı</span>}</span>
                <div className="flex gap-0.5 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
                  {sec.isActive ? (<>
                    <Button size="icon" variant="ghost" className="h-6 w-6 text-gray-400 hover:text-gray-600" onClick={() => startEdit(sec)}><Edit2 className="h-3 w-3" /></Button>
                    <Button size="icon" variant="ghost" className="h-6 w-6 text-gray-400 hover:text-red-600" onClick={() => setDeleteTarget(sec)}><Trash2 className="h-3 w-3" /></Button>
                  </>) : (
                    <TooltipProvider delayDuration={200}><Tooltip><TooltipTrigger asChild>
                      <Button
                        size="icon" variant="ghost"
                        className="h-6 w-6 text-green-600 hover:text-green-700 hover:bg-green-50"
                        disabled={reactivating === sec.sectionId}
                        onClick={() => {
                          setReactivating(sec.sectionId);
                          onReactivateSection(sec.sectionId).finally(() => setReactivating(null));
                        }}
                      >
                        <RotateCcw className={`h-3 w-3 ${reactivating === sec.sectionId ? 'animate-spin' : ''}`} />
                      </Button>
                    </TooltipTrigger><TooltipContent side="top" className="text-xs">Şubeyi Yeniden Aç</TooltipContent></Tooltip></TooltipProvider>
                  )}
                </div>
              </>)}
            </div>
          ))}
          {adding ? (
            <div className="flex flex-col gap-1.5 px-6 py-2 border-t bg-blue-50/50">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-xs w-8 text-center text-muted-foreground shrink-0">+</span>
                <div className="flex flex-col gap-0.5">
                  <span className="text-[10px] text-muted-foreground">Kontenjan</span>
                  <Input className="h-7 w-16 text-xs" value={newCap} onChange={e => setNewCap(e.target.value)} type="number" min={1} max={500} />
                </div>
                <div className="flex flex-col gap-0.5">
                  <span className="text-[10px] text-muted-foreground">Bölüm İçi</span>
                  <Input className="h-7 w-16 text-xs" placeholder="—" value={newInternalQuota} onChange={e => setNewInternalQuota(e.target.value)} type="number" min={0} max={500} />
                </div>
                <div className="flex flex-col gap-0.5">
                  <span className="text-[10px] text-muted-foreground">Bölüm Dışı</span>
                  <Input className="h-7 w-16 text-xs" placeholder="—" value={newExternalQuota} onChange={e => setNewExternalQuota(e.target.value)} type="number" min={0} max={500} />
                </div>
                <div className="flex-1 min-w-0">
                  <InstructorSelect instructors={instructors} value={newInsId} onChange={setNewInsId} />
                </div>
                <Button size="sm" className="h-7 text-xs shrink-0" onClick={addSection}>Ekle</Button>
                <Button size="sm" variant="ghost" className="h-7 text-xs shrink-0" onClick={() => setAdding(false)}><X className="h-3.5 w-3.5" /></Button>
              </div>
              {newQuotaWarning && (
                <div className="text-[10px] text-amber-600 flex items-center gap-1 ml-10">
                  <AlertCircle className="h-3 w-3 shrink-0" />{newQuotaWarning}
                </div>
              )}
            </div>
          ) : (
            <button className="w-full flex items-center gap-2 px-6 py-1.5 text-xs text-blue-600 hover:bg-blue-50 transition-colors border-t" onClick={() => setAdding(true)}>
              <Plus className="h-3 w-3" /> Şube Ekle
            </button>
          )}
          {closeError && <Alert variant="destructive" className="mx-4 my-2"><AlertCircle className="h-4 w-4" /><AlertDescription>{(closeError as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Şube kapatılamadı.'}</AlertDescription></Alert>}
        </div>
      )}

      <Dialog open={deleteTarget !== null} onOpenChange={() => setDeleteTarget(null)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader><DialogTitle>Şubeyi Kapat</DialogTitle></DialogHeader>
          <div className="space-y-2 text-sm text-muted-foreground">
            <p><strong>{deleteTarget?.courseCode} — Şube {deleteTarget?.sectionNumber}</strong> kapatılacak.</p>
            {deleteTarget && <p>{deleteTarget.draftOrPendingCount > 0 ? `${deleteTarget.draftOrPendingCount} adet Draft/Pending kayıt Dropped durumuna düşecek.` : 'Bu şube için düşürülecek Draft/Pending kayıt bulunmuyor.'}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>Vazgeç</Button>
            <Button variant="destructive" onClick={() => { if (deleteTarget) { onCloseSection(deleteTarget); setDeleteTarget(null); } }} disabled={closePending}>{closePending ? 'Kapatılıyor...' : 'Kapat'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={closeAllOpen} onOpenChange={() => setCloseAllOpen(false)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader><DialogTitle>Dersi Kapat</DialogTitle></DialogHeader>
          <div className="space-y-2 text-sm text-muted-foreground">
            <p><strong>{courseCode} — {courseName}</strong> dersinin tüm <strong>{sections.filter(s => s.isActive).length} aktif şubesi</strong> kapatılacak.</p>
            <p>Draft/Pending durumdaki kayıtlar otomatik olarak Dropped'a düşecek.</p>
            {closeAllError && (
              <Alert variant="destructive" className="mt-2"><AlertCircle className="h-4 w-4" /><AlertDescription>{closeAllError}</AlertDescription></Alert>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloseAllOpen(false)}>Vazgeç</Button>
            <Button
              variant="destructive"
              disabled={closeAllPending}
              onClick={() => {
                setCloseAllPending(true);
                setCloseAllError(null);
                onCloseAllSections(courseId)
                  .then(() => setCloseAllOpen(false))
                  .catch((err: { response?: { data?: { error?: string } } }) => {
                    setCloseAllError(err?.response?.data?.error ?? 'Ders kapatılamadı.');
                  })
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
