import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert';
import { Separator } from '@/components/ui/separator';
import { CalendarPlus, CheckCircle2, AlertTriangle } from 'lucide-react';
import { StepAccordion, ConfirmButton, MutationFeedback, PrerequisiteAlert } from './shared';
import { useOpenNewTerm } from '@/hooks/useTermClosing';

interface NewTermStepProps {
  departmentId: number;
  isCompleted: boolean;
  isActive: boolean;
  step3Completed: boolean;
  completedAt?: string | null;
  completedBy?: string | null;
  completedNotes?: string | null;
}

export function NewTermStep({
  departmentId, isCompleted, isActive, step3Completed,
  completedAt, completedBy, completedNotes,
}: NewTermStepProps) {
  const openTermMutation = useOpenNewTerm(departmentId);
  const isLocked = !step3Completed && !isCompleted;

  const [termName, setTermName] = useState('');
  const [termYear, setTermYear] = useState(new Date().getFullYear());
  const [semester, setSemester] = useState(2);
  const [regStart, setRegStart] = useState('');
  const [regEnd, setRegEnd] = useState('');
  const [termStart, setTermStart] = useState('');
  const [termEnd, setTermEnd] = useState('');

  const handleOpen = async () => {
    try {
      await openTermMutation.mutateAsync({
        name: termName,
        year: termYear,
        semester,
        registrationStart: new Date(regStart).toISOString(),
        registrationEnd: new Date(regEnd).toISOString(),
        termStart: new Date(termStart).toISOString(),
        termEnd: new Date(termEnd).toISOString(),
      });
    } catch { /* feedback below */ }
  };

  const dateErrors: string[] = [];
  if (regStart && regEnd && new Date(regEnd) <= new Date(regStart))
    dateErrors.push('Kayit bitis tarihi baslangictan sonra olmalidir.');
  if (termStart && termEnd && new Date(termEnd) <= new Date(termStart))
    dateErrors.push('Donem bitis tarihi baslangictan sonra olmalidir.');

  const canSubmit = termName && regStart && regEnd && termStart && termEnd && dateErrors.length === 0;

  return (
    <StepAccordion
      stepNum={4}
      title="Yeni Donem"
      description="Yeni akademik donemi tanimlayip kayit takvimini ayarlayin."
      icon={CalendarPlus}
      isCompleted={isCompleted}
      isActive={isActive}
      isLocked={isLocked}
      lockReason="Once Sinif Atlatma tamamlanmalidir"
      completedSummary={completedNotes ?? undefined}
      completedAt={completedAt}
      completedBy={completedBy}
      onToggle={() => {}}
    >
      {isLocked && <PrerequisiteAlert stepName="Sinif Atlatma (Adim 3)" />}

      {!isCompleted && step3Completed && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <FormField label="Donem Adi" required>
              <Input placeholder="2025-2026 Bahar" value={termName} onChange={e => setTermName(e.target.value)} />
            </FormField>
            <FormField label="Yil">
              <Input type="number" value={termYear} onChange={e => setTermYear(Number(e.target.value))} />
            </FormField>
            <FormField label="Donem Tipi">
              <select className="w-full border rounded-md px-3 py-2 text-sm bg-card" value={semester} onChange={e => setSemester(Number(e.target.value))}>
                <option value={1}>Guz</option>
                <option value={2}>Bahar</option>
                <option value={3}>Yaz</option>
              </select>
            </FormField>
          </div>

          <Separator />

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FormField label="Kayit Baslangici" required>
              <Input type="date" value={regStart} onChange={e => setRegStart(e.target.value)} />
            </FormField>
            <FormField label="Kayit Bitisi" required>
              <Input type="date" value={regEnd} onChange={e => setRegEnd(e.target.value)} />
            </FormField>
            <FormField label="Donem Baslangici" required>
              <Input type="date" value={termStart} onChange={e => setTermStart(e.target.value)} />
            </FormField>
            <FormField label="Donem Bitisi" required>
              <Input type="date" value={termEnd} onChange={e => setTermEnd(e.target.value)} />
            </FormField>
          </div>

          {dateErrors.map((err, i) => (
            <Alert key={i} variant="destructive" className="py-2">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>{err}</AlertDescription>
            </Alert>
          ))}

          <ConfirmButton
            label="Donemi Olustur"
            icon={CalendarPlus}
            isLoading={openTermMutation.isPending}
            disabled={!canSubmit}
            title="Yeni donemi acmak istiyor musunuz?"
            description={`"${termName || '(isimsiz)'}" donemi olusturulacak ve kayit tarihleri ayarlanacaktir.`}
            onConfirm={handleOpen}
          />
        </div>
      )}

      {isCompleted && (
        <Alert variant="success">
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Yeni Donem Olusturuldu</AlertTitle>
          <AlertDescription>
            {openTermMutation.data
              ? `"${openTermMutation.data.message}" (ID: ${openTermMutation.data.newTermId})`
              : completedNotes ?? 'Yeni donem basariyla olusturuldu.'}
          </AlertDescription>
        </Alert>
      )}

      <MutationFeedback mutation={openTermMutation} />
    </StepAccordion>
  );
}

function FormField({ label, required, children }: { label: string; required?: boolean; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs font-medium">
        {label}
        {required && <span className="text-destructive ml-0.5">*</span>}
      </Label>
      {children}
    </div>
  );
}
