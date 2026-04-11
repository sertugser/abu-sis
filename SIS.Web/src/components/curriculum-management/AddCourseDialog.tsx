import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  Dialog, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { ArrowRight, Loader2, Search } from 'lucide-react';
import { useAdminCourses, type CourseListItemDto } from '../../hooks/useAdminCourseCatalog';

interface Props {
  open: boolean;
  onClose: () => void;
  onAdd: (courseId: number, semester: number, isCompulsory: boolean) => void;
  isPending: boolean;
  existingCourseIds: Set<number>;
  catalogRoute?: string;
}

export default function AddCourseDialog({ open, onClose, onAdd, isPending, existingCourseIds, catalogRoute }: Props) {
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<CourseListItemDto | null>(null);
  const [semester, setSemester] = useState(1);
  const [compulsory, setCompulsory] = useState(true);

  const { data: courses, isLoading } = useAdminCourses({ search: search.length >= 2 ? search : undefined });

  const filtered = (courses ?? []).filter(c => !existingCourseIds.has(c.id));

  function handleAdd() {
    if (!selected) return;
    onAdd(selected.id, semester, compulsory);
  }

  function handleClose() {
    setSearch('');
    setSelected(null);
    setSemester(1);
    setCompulsory(true);
    onClose();
  }

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) handleClose(); }}>
      <ResizableDialogContent defaultWidth={520} minWidth={400} minHeight={300} className="flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
          <DialogTitle>Ders Ekle</DialogTitle>
        </DialogHeader>

        {!selected ? (
          <div className="flex flex-col flex-1 min-h-0 px-6 gap-3">
            <div className="relative shrink-0">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Ders kodu veya adı ile arayın..."
                value={search}
                onChange={e => setSearch(e.target.value)}
                className="pl-9"
              />
            </div>
            <div className="flex-1 min-h-0 overflow-y-auto border rounded-md">
              {isLoading ? (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : filtered.length === 0 ? (
                <div className="text-center py-8 space-y-2">
                  <p className="text-sm text-muted-foreground">
                    {search.length < 2 ? 'En az 2 karakter girin' : 'Sonuç bulunamadı'}
                  </p>
                  {search.length >= 2 && catalogRoute && (
                    <p className="text-xs text-muted-foreground">
                      Aradığınız ders bulunamadı mı?{' '}
                      <Link
                        to={catalogRoute}
                        onClick={handleClose}
                        className="text-primary hover:underline inline-flex items-center gap-0.5"
                      >
                        Ders Kataloğu'na Git
                        <ArrowRight className="h-3 w-3" />
                      </Link>
                    </p>
                  )}
                </div>
              ) : (
                filtered.map(c => (
                  <button
                    key={c.id}
                    onClick={() => setSelected(c)}
                    className="w-full text-left px-3 py-2 hover:bg-muted/60 border-b last:border-b-0 transition-colors"
                  >
                    <span className="font-medium text-sm">{c.code}</span>
                    <span className="text-sm text-muted-foreground ml-2">{c.name}</span>
                    <span className="text-xs text-muted-foreground ml-2">({c.ects} AKTS)</span>
                  </button>
                ))
              )}
            </div>
          </div>
        ) : (
          <div className="flex-1 overflow-y-auto px-6 space-y-4">
            <div className="p-3 rounded-md bg-muted/40 border">
              <p className="font-medium text-sm">{selected.code} — {selected.name}</p>
              <p className="text-xs text-muted-foreground mt-1">{selected.ects} AKTS | {selected.theoryHours}T + {selected.labHours}U</p>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Yarıyıl</Label>
                <Input
                  type="number"
                  min={1}
                  max={12}
                  value={semester}
                  onChange={e => setSemester(Number(e.target.value))}
                />
              </div>
              <div className="space-y-2">
                <Label>Zorunlu</Label>
                <div className="flex items-center gap-2 pt-1">
                  <Switch checked={compulsory} onCheckedChange={setCompulsory} />
                  <span className="text-sm text-muted-foreground">{compulsory ? 'Zorunlu' : 'Seçmeli'}</span>
                </div>
              </div>
            </div>
          </div>
        )}

        <DialogFooter className="shrink-0 px-6 py-4 border-t">
          {selected && (
            <Button variant="outline" onClick={() => setSelected(null)}>
              Geri
            </Button>
          )}
          <Button variant="outline" onClick={handleClose}>
            İptal
          </Button>
          {selected && (
            <Button onClick={handleAdd} disabled={isPending}>
              {isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
              Ekle
            </Button>
          )}
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
