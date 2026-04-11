import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { AlertCircle, Loader2, Plus } from 'lucide-react';
import {
  useElectiveGroupList,
  useCreateElectiveGroup,
} from '../hooks/useElectiveGroups';
import { useAdminFaculties } from '../hooks/useAdminAcademicStructure';
import {
  ELECTIVE_SUBTYPE_LABELS,
  ELECTIVE_SUBTYPE_SHORT,
} from '../types/electiveGroup';
import type {
  ElectiveSubtype,
  ElectiveGroupListDto,
  CreateElectiveGroupRequest,
} from '../types/electiveGroup';
import ElectiveGroupDetailSheet from '../components/elective-groups/ElectiveGroupDetailSheet';

const SUBTYPES: ElectiveSubtype[] = ['UniversityElective', 'NonDepartmentElective', 'TechnicalElective'];

// ─── Create Dialog ─────────────────────────────────────────────────

interface CreateDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function CreateElectiveGroupDialog({ open, onOpenChange }: CreateDialogProps) {
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [subtype, setSubtype] = useState<ElectiveSubtype>('UniversityElective');
  const [facultyId, setFacultyId] = useState<string>('');
  const [description, setDescription] = useState('');

  const { data: faculties } = useAdminFaculties();
  const createMutation = useCreateElectiveGroup();

  function reset() {
    setName('');
    setCode('');
    setSubtype('UniversityElective');
    setFacultyId('');
    setDescription('');
  }

  function handleCreate() {
    const req: CreateElectiveGroupRequest = {
      name: name.trim(),
      code: code.trim(),
      electiveSubtype: subtype,
      ...(subtype === 'TechnicalElective' && facultyId ? { facultyId: Number(facultyId) } : {}),
      ...(description.trim() ? { description: description.trim() } : {}),
    };
    createMutation.mutate(req, {
      onSuccess: () => {
        reset();
        onOpenChange(false);
      },
    });
  }

  function handleOpenChange(val: boolean) {
    if (!val) reset();
    onOpenChange(val);
  }

  const canSubmit = name.trim().length > 0 && code.trim().length > 0;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Yeni Secmeli Ders Grubu</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div>
            <Label htmlFor="ceg-name">Grup Adi</Label>
            <Input id="ceg-name" value={name} onChange={e => setName(e.target.value)} placeholder="ornegin: Teknik Secmeli Grup A" />
          </div>
          <div>
            <Label htmlFor="ceg-code">Kod</Label>
            <Input id="ceg-code" value={code} onChange={e => setCode(e.target.value.toUpperCase())} placeholder="ornegin: TE-A" />
          </div>
          <div>
            <Label>Tur</Label>
            <Select value={subtype} onValueChange={v => setSubtype(v as ElectiveSubtype)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {SUBTYPES.map(s => (
                  <SelectItem key={s} value={s}>{ELECTIVE_SUBTYPE_LABELS[s]}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {subtype === 'TechnicalElective' && (
            <div>
              <Label>Fakulte</Label>
              <Select value={facultyId} onValueChange={setFacultyId}>
                <SelectTrigger>
                  <SelectValue placeholder="Fakulte secin..." />
                </SelectTrigger>
                <SelectContent>
                  {faculties?.map(f => (
                    <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <div>
            <Label htmlFor="ceg-desc">Aciklama</Label>
            <Textarea id="ceg-desc" value={description} onChange={e => setDescription(e.target.value)} rows={3} />
          </div>
        </div>

        {createMutation.isError && (
          <p className="text-xs text-destructive mt-1">Grup olusturulamadi.</p>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>Iptal</Button>
          <Button onClick={handleCreate} disabled={!canSubmit || createMutation.isPending}>
            {createMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Olustur
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Subtype Badge ─────────────────────────────────────────────────

const SUBTYPE_COLORS: Record<ElectiveSubtype, string> = {
  UniversityElective: 'bg-blue-100 text-blue-800 border-blue-200',
  NonDepartmentElective: 'bg-purple-100 text-purple-800 border-purple-200',
  TechnicalElective: 'bg-amber-100 text-amber-800 border-amber-200',
};

function SubtypeBadge({ subtype }: { subtype: ElectiveSubtype }) {
  return (
    <Badge variant="outline" className={`text-[10px] ${SUBTYPE_COLORS[subtype]}`}>
      {ELECTIVE_SUBTYPE_SHORT[subtype]}
    </Badge>
  );
}

// ─── Main Page ─────────────────────────────────────────────────────

export default function ElectiveGroupManagementPage() {
  const [subtypeFilter, setSubtypeFilter] = useState<string>('all');
  const [facultyFilter, setFacultyFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [createOpen, setCreateOpen] = useState(false);
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null);

  const { data: faculties } = useAdminFaculties();

  const querySubtype = subtypeFilter === 'all' ? undefined : (subtypeFilter as ElectiveSubtype);
  const queryFacultyId = subtypeFilter === 'TechnicalElective' && facultyFilter !== 'all'
    ? Number(facultyFilter)
    : undefined;
  const queryIsActive = statusFilter === 'all' ? undefined : statusFilter === 'active';

  const { data: groups, isLoading, error } = useElectiveGroupList(querySubtype, queryFacultyId, queryIsActive);

  return (
    <div className="p-6 space-y-4">
      {/* ── Top bar ── */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-tight">Secmeli Ders Gruplari</h1>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4 mr-2" />
          Yeni Grup
        </Button>
      </div>

      {/* ── Filters ── */}
      <div className="flex flex-wrap items-center gap-3">
        <Select value={subtypeFilter} onValueChange={v => { setSubtypeFilter(v); if (v !== 'TechnicalElective') setFacultyFilter('all'); }}>
          <SelectTrigger className="w-[220px]">
            <SelectValue placeholder="Tur" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tumu</SelectItem>
            {SUBTYPES.map(s => (
              <SelectItem key={s} value={s}>{ELECTIVE_SUBTYPE_LABELS[s]}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={facultyFilter}
          onValueChange={setFacultyFilter}
          disabled={subtypeFilter !== 'TechnicalElective'}
        >
          <SelectTrigger className="w-[200px]">
            <SelectValue placeholder="Fakulte" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tumu</SelectItem>
            {faculties?.map(f => (
              <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger className="w-[130px]">
            <SelectValue placeholder="Durum" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tumu</SelectItem>
            <SelectItem value="active">Aktif</SelectItem>
            <SelectItem value="inactive">Pasif</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* ── Error ── */}
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Gruplar yuklenirken hata olustu.</AlertDescription>
        </Alert>
      )}

      {/* ── Loading skeleton ── */}
      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-10 w-full" />
          ))}
        </div>
      )}

      {/* ── Table ── */}
      {!isLoading && groups && (
        <div className="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Kod</TableHead>
                <TableHead>Grup Adi</TableHead>
                <TableHead className="w-24">Tur</TableHead>
                <TableHead>Fakulte</TableHead>
                <TableHead className="w-20 text-center">Ders Sayisi</TableHead>
                <TableHead className="w-20">Durum</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {groups.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-muted-foreground py-8">
                    Kayit bulunamadi.
                  </TableCell>
                </TableRow>
              ) : (
                groups.map((g: ElectiveGroupListDto) => (
                  <TableRow
                    key={g.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => setSelectedGroupId(g.id)}
                  >
                    <TableCell className="font-mono text-xs">{g.code}</TableCell>
                    <TableCell className="text-sm font-medium">{g.name}</TableCell>
                    <TableCell><SubtypeBadge subtype={g.electiveSubtype} /></TableCell>
                    <TableCell className="text-sm text-muted-foreground">{g.facultyName ?? '-'}</TableCell>
                    <TableCell className="text-center text-sm">{g.courseCount}</TableCell>
                    <TableCell>
                      <Badge variant={g.isActive ? 'default' : 'secondary'} className={g.isActive ? 'bg-emerald-100 text-emerald-800 border-emerald-200' : 'bg-red-100 text-red-800 border-red-200'}>
                        {g.isActive ? 'Aktif' : 'Pasif'}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      )}

      {/* ── Create Dialog ── */}
      <CreateElectiveGroupDialog open={createOpen} onOpenChange={setCreateOpen} />

      {/* ── Detail Sheet ── */}
      <ElectiveGroupDetailSheet
        key={selectedGroupId}
        groupId={selectedGroupId}
        onClose={() => setSelectedGroupId(null)}
      />
    </div>
  );
}
