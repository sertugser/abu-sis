import { useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Collapsible, CollapsibleContent, CollapsibleTrigger,
} from '@/components/ui/collapsible';
import { Progress } from '@/components/ui/progress';
import { AlertCircle, ChevronDown, ChevronRight, Lightbulb, Plus, Trash2 } from 'lucide-react';
import { useAdminTerms, type AdminTermDto } from '../hooks/useAdminTerms';
import { useAdminFaculties } from '../hooks/useAdminAcademicStructure';
import { useClassrooms } from '../hooks/useClassrooms';
import {
  useClassroomAssignments,
  useCreateClassroomAssignment,
  useDeleteClassroomAssignment,
  useBulkAssignClassrooms,
  useSuggestClassroomAssignments,
} from '../hooks/useClassroomAssignment';
import type {
  ClassroomTermAssignmentDto,
  ClassroomAssignmentSuggestionDto,
} from '../api/classroomAssignment.api';

// ── Helpers ─────────────────────────────────────────────────────────────────

const CLASSROOM_TYPE_LABELS: Record<string, string> = {
  Lecture: 'Derslik',
  Lab: 'Laboratuvar',
  Computer: 'Bilgisayar Lab',
  Amphitheater: 'Amfi',
  Seminar: 'Seminer',
};

function classroomTypeLabel(type: string): string {
  return CLASSROOM_TYPE_LABELS[type] ?? type;
}

// ── Component ───────────────────────────────────────────────────────────────

export default function ClassroomAssignmentPage() {
  const { data: terms = [], isLoading: termsLoading } = useAdminTerms();
  const { data: faculties = [] } = useAdminFaculties();
  const { data: classrooms = [] } = useClassrooms();

  // Term selection: default to active term
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const activeTerm = terms.find(t => t.isActive);
  const termId = selectedTermId ?? activeTerm?.id ?? null;

  const { data: assignments = [], isLoading: assignmentsLoading, isError } = useClassroomAssignments(termId);

  const createMutation = useCreateClassroomAssignment();
  const deleteMutation = useDeleteClassroomAssignment();
  const bulkMutation = useBulkAssignClassrooms();
  const suggestMutation = useSuggestClassroomAssignments();

  // Dialog states
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [suggestDialogOpen, setSuggestDialogOpen] = useState(false);
  const [addForm, setAddForm] = useState({ classroomId: 0, facultyId: 0 });
  const [addError, setAddError] = useState('');

  // Suggestion data
  const [suggestionData, setSuggestionData] = useState<ClassroomAssignmentSuggestionDto | null>(null);

  // Collapsible sections state
  const [openFaculties, setOpenFaculties] = useState<Set<number>>(new Set());

  // ── Derived data ────────────────────────────────────────────────────────

  const assignedClassroomIds = useMemo(
    () => new Set(assignments.map(a => a.classroomId)),
    [assignments],
  );

  const unassignedClassrooms = useMemo(
    () => classrooms.filter(c => !assignedClassroomIds.has(c.id)),
    [classrooms, assignedClassroomIds],
  );

  const groupedByFaculty = useMemo(() => {
    const map = new Map<number, { facultyName: string; items: ClassroomTermAssignmentDto[] }>();
    for (const a of assignments) {
      if (!map.has(a.facultyId)) {
        map.set(a.facultyId, { facultyName: a.facultyName, items: [] });
      }
      map.get(a.facultyId)!.items.push(a);
    }
    return Array.from(map.entries()).sort((a, b) => a[1].facultyName.localeCompare(b[1].facultyName));
  }, [assignments]);

  // ── Handlers ────────────────────────────────────────────────────────────

  const toggleFaculty = (id: number) => {
    setOpenFaculties(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const handleOpenAdd = () => {
    setAddForm({ classroomId: unassignedClassrooms[0]?.id ?? 0, facultyId: faculties[0]?.id ?? 0 });
    setAddError('');
    setAddDialogOpen(true);
  };

  const handleAdd = () => {
    if (!termId || !addForm.classroomId || !addForm.facultyId) return;
    setAddError('');
    createMutation.mutate(
      { termId, classroomId: addForm.classroomId, facultyId: addForm.facultyId },
      {
        onSuccess: () => setAddDialogOpen(false),
        onError: (err: unknown) => {
          const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
          setAddError(msg ?? 'Atama olusturulamadi.');
        },
      },
    );
  };

  const handleDelete = (id: number) => {
    if (confirm('Bu atamayi silmek istediginize emin misiniz?')) {
      deleteMutation.mutate(id);
    }
  };

  const handleSuggest = () => {
    if (!termId) return;
    suggestMutation.mutate(termId, {
      onSuccess: (data) => {
        setSuggestionData(data);
        setSuggestDialogOpen(true);
      },
    });
  };

  const handleApplyAll = () => {
    if (!suggestionData || !termId) return;
    bulkMutation.mutate(
      {
        termId,
        assignments: suggestionData.suggestions.map(s => ({
          classroomId: s.classroomId,
          facultyId: s.facultyId,
        })),
      },
      {
        onSuccess: () => {
          setSuggestDialogOpen(false);
          setSuggestionData(null);
        },
      },
    );
  };

  // ── Render ──────────────────────────────────────────────────────────────

  if (termsLoading) return <Skeleton className="h-96 rounded-xl" />;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold">Derslik - Fakulte Atamasi</h1>
        <div className="flex gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={handleSuggest}
            disabled={!termId || suggestMutation.isPending}
          >
            <Lightbulb className="h-4 w-4 mr-1" />
            {suggestMutation.isPending ? 'Hesaplaniyor...' : 'Akilli Oneri'}
          </Button>
          <Button size="sm" onClick={handleOpenAdd} disabled={!termId || unassignedClassrooms.length === 0}>
            <Plus className="h-4 w-4 mr-1" /> Atama Ekle
          </Button>
        </div>
      </div>

      {/* Term selector */}
      <div className="flex items-center gap-3">
        <label className="text-sm font-medium">Donem:</label>
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm min-w-[300px]"
          value={termId ?? ''}
          onChange={e => setSelectedTermId(Number(e.target.value) || null)}
        >
          {terms.map((t: AdminTermDto) => (
            <option key={t.id} value={t.id}>
              {t.name} {t.isActive ? '(Aktif)' : ''} — {t.statusLabel}
            </option>
          ))}
        </select>
      </div>

      {/* Stats bar */}
      {termId && (
        <div className="flex gap-4">
          <Badge variant="outline" className="text-sm py-1 px-3">
            Toplam Derslik: {classrooms.length}
          </Badge>
          <Badge variant="secondary" className="text-sm py-1 px-3">
            Atanmis: {assignments.length}
          </Badge>
          <Badge variant={unassignedClassrooms.length > 0 ? 'destructive' : 'default'} className="text-sm py-1 px-3">
            Atanmamis: {unassignedClassrooms.length}
          </Badge>
        </div>
      )}

      {/* Error state */}
      {isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Atamalar yuklenemedi.</AlertDescription>
        </Alert>
      )}

      {/* Loading */}
      {assignmentsLoading && <Skeleton className="h-48 rounded-xl" />}

      {/* No term selected */}
      {!termId && (
        <div className="text-center py-12 text-muted-foreground">
          Lutfen bir donem secin.
        </div>
      )}

      {/* Grouped assignments */}
      {termId && !assignmentsLoading && !isError && (
        <>
          {groupedByFaculty.length === 0 ? (
            <Card>
              <CardContent className="py-8">
                <div className="text-center text-muted-foreground">
                  Bu donem icin henuz derslik atamasi yapilmamis. "Akilli Oneri" butonu ile baslayabilirsiniz.
                </div>
              </CardContent>
            </Card>
          ) : (
            <div className="space-y-3">
              {groupedByFaculty.map(([facultyId, group]) => (
                <Collapsible
                  key={facultyId}
                  open={openFaculties.has(facultyId)}
                  onOpenChange={() => toggleFaculty(facultyId)}
                >
                  <Card>
                    <CollapsibleTrigger asChild>
                      <CardHeader className="cursor-pointer hover:bg-muted/50 transition-colors pb-3">
                        <CardTitle className="text-sm flex items-center justify-between">
                          <span className="flex items-center gap-2">
                            {openFaculties.has(facultyId)
                              ? <ChevronDown className="h-4 w-4" />
                              : <ChevronRight className="h-4 w-4" />
                            }
                            {group.facultyName}
                          </span>
                          <Badge variant="outline">{group.items.length} derslik</Badge>
                        </CardTitle>
                      </CardHeader>
                    </CollapsibleTrigger>
                    <CollapsibleContent>
                      <CardContent className="pt-0">
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Derslik Adi</TableHead>
                              <TableHead>Kapasite</TableHead>
                              <TableHead>Tip</TableHead>
                              <TableHead className="w-20">Islem</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {group.items.map(a => {
                              const cr = classrooms.find(c => c.id === a.classroomId);
                              return (
                                <TableRow key={a.id}>
                                  <TableCell className="font-medium">{a.classroomName}</TableCell>
                                  <TableCell className="font-mono">{cr?.capacity ?? '-'}</TableCell>
                                  <TableCell>
                                    <Badge variant="outline">{classroomTypeLabel(cr?.type ?? '')}</Badge>
                                  </TableCell>
                                  <TableCell>
                                    <Button
                                      size="icon"
                                      variant="ghost"
                                      className="h-7 w-7 text-destructive"
                                      onClick={() => handleDelete(a.id)}
                                      disabled={deleteMutation.isPending}
                                    >
                                      <Trash2 className="h-4 w-4" />
                                    </Button>
                                  </TableCell>
                                </TableRow>
                              );
                            })}
                          </TableBody>
                        </Table>
                      </CardContent>
                    </CollapsibleContent>
                  </Card>
                </Collapsible>
              ))}
            </div>
          )}
        </>
      )}

      {/* ── Add Assignment Dialog ────────────────────────────────────────── */}
      <Dialog open={addDialogOpen} onOpenChange={setAddDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Derslik Atamasi Ekle</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-1">
              <label className="text-xs font-medium">Derslik</label>
              <select
                className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                value={addForm.classroomId}
                onChange={e => setAddForm(f => ({ ...f, classroomId: Number(e.target.value) }))}
              >
                {unassignedClassrooms.map(c => (
                  <option key={c.id} value={c.id}>
                    {c.name} (Kap: {c.capacity}, {classroomTypeLabel(c.type)})
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium">Fakulte</label>
              <select
                className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                value={addForm.facultyId}
                onChange={e => setAddForm(f => ({ ...f, facultyId: Number(e.target.value) }))}
              >
                {faculties.map(f => (
                  <option key={f.id} value={f.id}>{f.name}</option>
                ))}
              </select>
            </div>
            {addError && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{addError}</AlertDescription>
              </Alert>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddDialogOpen(false)}>Iptal</Button>
            <Button onClick={handleAdd} disabled={createMutation.isPending}>
              {createMutation.isPending ? 'Kaydediliyor...' : 'Ata'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Suggestion Dialog ────────────────────────────────────────────── */}
      <Dialog open={suggestDialogOpen} onOpenChange={setSuggestDialogOpen}>
        <DialogContent className="max-w-3xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Akilli Derslik Atama Onerisi</DialogTitle>
          </DialogHeader>
          {suggestionData && (
            <div className="space-y-6">
              {/* Faculty load analysis */}
              <div>
                <h3 className="text-sm font-semibold mb-3">Fakulte Yuk Analizi</h3>
                <div className="space-y-3">
                  {suggestionData.facultyLoads.map(fl => (
                    <div key={fl.facultyId} className="space-y-1">
                      <div className="flex justify-between text-sm">
                        <span className="font-medium">{fl.facultyName}</span>
                        <span className="text-muted-foreground">
                          {fl.studentCount} ogrenci &middot; {fl.estimatedSectionCount} section &middot; %{fl.loadPercentage.toFixed(1)}
                        </span>
                      </div>
                      <Progress value={fl.loadPercentage} className="h-2" />
                    </div>
                  ))}
                </div>
              </div>

              {/* Suggestion summary */}
              <div className="flex gap-3 text-sm text-muted-foreground">
                <span>Toplam derslik: {suggestionData.totalClassrooms}</span>
                <span>&middot;</span>
                <span>Zaten atanmis: {suggestionData.alreadyAssigned}</span>
                <span>&middot;</span>
                <span>Onerilen: {suggestionData.suggestions.length}</span>
              </div>

              {/* Suggestions table */}
              {suggestionData.suggestions.length > 0 ? (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Derslik</TableHead>
                      <TableHead>Kapasite</TableHead>
                      <TableHead>Tip</TableHead>
                      <TableHead>Fakulte</TableHead>
                      <TableHead>Neden</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {suggestionData.suggestions.map(s => (
                      <TableRow key={s.classroomId}>
                        <TableCell className="font-medium">{s.classroomName}</TableCell>
                        <TableCell className="font-mono">{s.classroomCapacity}</TableCell>
                        <TableCell>
                          <Badge variant="outline">{classroomTypeLabel(s.classroomType)}</Badge>
                        </TableCell>
                        <TableCell>{s.facultyName}</TableCell>
                        <TableCell className="text-xs text-muted-foreground max-w-[200px]">{s.reason}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              ) : (
                <div className="text-center py-4 text-muted-foreground">
                  Tum derslikler zaten atanmis.
                </div>
              )}

              {/* Bulk apply errors */}
              {bulkMutation.isError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>Toplu atama sirasinda hata olustu.</AlertDescription>
                </Alert>
              )}
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setSuggestDialogOpen(false)}>Kapat</Button>
            {suggestionData && suggestionData.suggestions.length > 0 && (
              <Button onClick={handleApplyAll} disabled={bulkMutation.isPending}>
                {bulkMutation.isPending ? 'Uygulanıyor...' : 'Tumunu Uygula'}
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
