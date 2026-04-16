import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Layers, Save, Trash2, Play } from 'lucide-react';
import {
  useConstraintProfiles,
  useApplyProfile,
  useCreateProfileFromCurrent,
  useDeleteProfile,
} from '@/hooks/useConstraintProfiles';

interface ProfileSelectorProps {
  onApplied?: (message: string) => void;
}

export default function ProfileSelector({ onApplied }: ProfileSelectorProps) {
  const { data: profiles } = useConstraintProfiles();
  const applyMutation = useApplyProfile();
  const createMutation = useCreateProfileFromCurrent();
  const deleteMutation = useDeleteProfile();

  const [applyConfirm, setApplyConfirm] = useState<{ id: number; name: string } | null>(null);
  const [saveDialog, setSaveDialog] = useState(false);
  const [newName, setNewName] = useState('');
  const [deleteConfirm, setDeleteConfirm] = useState<{ id: number; name: string } | null>(null);

  function handleApply() {
    if (!applyConfirm) return;
    applyMutation.mutate(applyConfirm.id, {
      onSuccess: (result) => {
        onApplied?.(`"${applyConfirm.name}" profili uygulandı — ${result.changedCount} kısıtlama güncellendi.`);
        setApplyConfirm(null);
      },
    });
  }

  function handleSave() {
    if (!newName.trim()) return;
    createMutation.mutate(
      { name: newName.trim() },
      {
        onSuccess: () => {
          onApplied?.(`"${newName.trim()}" profili kaydedildi.`);
          setSaveDialog(false);
          setNewName('');
        },
      },
    );
  }

  function handleDelete() {
    if (!deleteConfirm) return;
    deleteMutation.mutate(deleteConfirm.id, {
      onSuccess: () => {
        onApplied?.(`"${deleteConfirm.name}" profili silindi.`);
        setDeleteConfirm(null);
      },
    });
  }

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" size="sm">
            <Layers className="h-4 w-4 mr-1" />
            Profiller
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-72">
          {profiles?.map((p) => (
            <DropdownMenuItem
              key={p.id}
              className="flex items-center justify-between"
              onSelect={(e) => e.preventDefault()}
            >
              <div className="flex items-center gap-2 flex-1 min-w-0">
                <span className="truncate text-sm">{p.name}</span>
                {p.isDefault && (
                  <Badge variant="secondary" className="text-[10px] px-1 shrink-0">
                    Varsayilan
                  </Badge>
                )}
              </div>
              <div className="flex items-center gap-1 ml-2 shrink-0">
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6"
                  onClick={() => setApplyConfirm({ id: p.id, name: p.name })}
                >
                  <Play className="h-3 w-3" />
                </Button>
                {!p.isDefault && (
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-6 w-6 text-destructive"
                    onClick={() => setDeleteConfirm({ id: p.id, name: p.name })}
                  >
                    <Trash2 className="h-3 w-3" />
                  </Button>
                )}
              </div>
            </DropdownMenuItem>
          ))}
          <DropdownMenuSeparator />
          <DropdownMenuItem onSelect={() => setSaveDialog(true)}>
            <Save className="h-4 w-4 mr-2" />
            Mevcut Ayarlari Kaydet
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      {/* Apply Confirmation */}
      <AlertDialog open={!!applyConfirm} onOpenChange={() => setApplyConfirm(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Profil Uygula</AlertDialogTitle>
            <AlertDialogDescription>
              "{applyConfirm?.name}" profili uygulanacak. Mevcut ayarlariniz otomatik
              olarak yedeklenecektir. Devam etmek istiyor musunuz?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Iptal</AlertDialogCancel>
            <AlertDialogAction onClick={handleApply} disabled={applyMutation.isPending}>
              {applyMutation.isPending ? 'Uygulaniyor...' : 'Uygula'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Save Dialog */}
      <Dialog open={saveDialog} onOpenChange={setSaveDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Profil Olarak Kaydet</DialogTitle>
          </DialogHeader>
          <Input
            placeholder="Profil adi"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSave()}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setSaveDialog(false)}>
              Iptal
            </Button>
            <Button
              onClick={handleSave}
              disabled={!newName.trim() || createMutation.isPending}
            >
              {createMutation.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation */}
      <AlertDialog open={!!deleteConfirm} onOpenChange={() => setDeleteConfirm(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Profili Sil</AlertDialogTitle>
            <AlertDialogDescription>
              "{deleteConfirm?.name}" profili kalici olarak silinecek. Bu islem geri alinamaz.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Iptal</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Sil
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
