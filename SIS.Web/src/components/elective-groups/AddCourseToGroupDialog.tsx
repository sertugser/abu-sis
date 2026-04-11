import { useState, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Loader2, Search } from 'lucide-react';
import { useAdminCourses, type CourseListItemDto } from '../../hooks/useAdminCourseCatalog';
import { useAddCoursesToGroup } from '../../hooks/useElectiveGroups';

interface Props {
  groupId: number;
  existingCourseIds: number[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export default function AddCourseToGroupDialog({ groupId, existingCourseIds, open, onOpenChange }: Props) {
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Set<number>>(new Set());

  const { data: courses, isLoading } = useAdminCourses({ search: search || undefined });
  const addMutation = useAddCoursesToGroup();

  const filtered = useMemo(() => {
    if (!courses) return [];
    return courses.filter(c => !existingCourseIds.includes(c.id));
  }, [courses, existingCourseIds]);

  function toggleCourse(id: number) {
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function handleAdd() {
    if (selected.size === 0) return;
    addMutation.mutate(
      { groupId, courseIds: Array.from(selected) },
      {
        onSuccess: () => {
          setSelected(new Set());
          setSearch('');
          onOpenChange(false);
        },
      },
    );
  }

  function handleOpenChange(val: boolean) {
    if (!val) {
      setSelected(new Set());
      setSearch('');
    }
    onOpenChange(val);
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-2xl max-h-[80vh] flex flex-col">
        <DialogHeader>
          <DialogTitle>Ders Ekle</DialogTitle>
        </DialogHeader>

        <div className="relative">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Ders kodu veya adı ile arayın..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>

        <div className="flex-1 overflow-auto border rounded-md">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : filtered.length === 0 ? (
            <div className="text-sm text-muted-foreground text-center py-12">
              {search ? 'Sonuc bulunamadi.' : 'Arama yapmak icin yazin.'}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-10" />
                  <TableHead>Kod</TableHead>
                  <TableHead>Ders Adi</TableHead>
                  <TableHead className="w-16 text-center">AKTS</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((c: CourseListItemDto) => (
                  <TableRow
                    key={c.id}
                    className="cursor-pointer"
                    onClick={() => toggleCourse(c.id)}
                  >
                    <TableCell>
                      <input
                        type="checkbox"
                        checked={selected.has(c.id)}
                        onChange={() => toggleCourse(c.id)}
                        className="h-4 w-4 rounded border-gray-300"
                      />
                    </TableCell>
                    <TableCell className="font-mono text-xs">{c.code}</TableCell>
                    <TableCell className="text-sm">{c.name}</TableCell>
                    <TableCell className="text-center text-sm">{c.ects}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
            Iptal
          </Button>
          <Button
            onClick={handleAdd}
            disabled={selected.size === 0 || addMutation.isPending}
          >
            {addMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {selected.size > 0 ? `${selected.size} Ders Ekle` : 'Ekle'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
