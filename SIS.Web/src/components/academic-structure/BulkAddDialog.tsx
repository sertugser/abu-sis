import { useMemo, useState, type ReactNode } from 'react';
import { CheckCircle2, XCircle, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import type { BulkImportResult } from '../../hooks/useAdminAcademicStructure';

export interface ParsedLine<T> {
  lineNo: number;
  label: string;
  item: T | null;
  error: string | null;
}

interface BulkAddDialogProps<T> {
  open: boolean;
  onClose: () => void;
  title: string;
  /** Format açıklaması + örnek (üstte bilgi kutusunda gösterilir). */
  description: ReactNode;
  placeholder: string;
  /** Bir satırın kolonlarından (virgül/tab ile bölünmüş) bir öğe üretir. */
  parseColumns: (columns: string[]) => { label: string; item: T | null; error: string | null };
  submit: (items: T[]) => Promise<BulkImportResult>;
}

function splitLine(line: string): string[] {
  // Excel'den yapıştırınca tab, elle yazınca virgül ayraç olabilir.
  const cols = line.includes('\t') ? line.split('\t') : line.split(',');
  return cols.map(c => c.trim());
}

export function BulkAddDialog<T>({
  open,
  onClose,
  title,
  description,
  placeholder,
  parseColumns,
  submit,
}: BulkAddDialogProps<T>) {
  const [text, setText] = useState('');
  const [result, setResult] = useState<BulkImportResult | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState('');

  const parsed = useMemo<ParsedLine<T>[]>(() => {
    return text
      .split('\n')
      .map((raw, idx) => ({ raw: raw.trim(), lineNo: idx + 1 }))
      .filter(l => l.raw.length > 0)
      .map(l => {
        const { label, item, error } = parseColumns(splitLine(l.raw));
        return { lineNo: l.lineNo, label, item, error };
      });
  }, [text, parseColumns]);

  const validItems = parsed.filter(p => p.item !== null && !p.error);
  const invalidCount = parsed.length - validItems.length;

  function reset() {
    setText('');
    setResult(null);
    setSubmitError('');
    setSubmitting(false);
  }

  function handleClose() {
    reset();
    onClose();
  }

  async function handleSubmit() {
    setSubmitting(true);
    setSubmitError('');
    try {
      const res = await submit(validItems.map(p => p.item as T));
      setResult(res);
      setText('');
    } catch {
      setSubmitError('İçe aktarma başarısız oldu. Tekrar deneyin.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={o => !o && handleClose()}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Upload className="h-5 w-5 text-primary" />
            {title}
          </DialogTitle>
        </DialogHeader>

        {result ? (
          <div className="space-y-4">
            <div className="flex items-center gap-4">
              <span className="inline-flex items-center gap-1.5 text-sm font-medium text-emerald-700">
                <CheckCircle2 className="h-4 w-4" /> {result.createdCount} eklendi
              </span>
              {result.failedCount > 0 && (
                <span className="inline-flex items-center gap-1.5 text-sm font-medium text-destructive">
                  <XCircle className="h-4 w-4" /> {result.failedCount} başarısız
                </span>
              )}
            </div>

            {result.rows.some(r => r.status === 'error') && (
              <div className="rounded-lg border border-slate-200 max-h-64 overflow-y-auto">
                <table className="w-full text-sm">
                  <tbody className="divide-y divide-slate-100">
                    {result.rows
                      .filter(r => r.status === 'error')
                      .map(r => (
                        <tr key={r.index}>
                          <td className="px-3 py-2 font-mono text-xs">{r.label}</td>
                          <td className="px-3 py-2 text-destructive text-xs">{r.message}</td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            )}

            <DialogFooter className="gap-2">
              <Button variant="outline" onClick={reset}>Yeni Aktarım</Button>
              <Button onClick={handleClose}>Kapat</Button>
            </DialogFooter>
          </div>
        ) : (
          <div className="space-y-3">
            <div className="rounded-lg border border-slate-200 bg-slate-50/70 px-3 py-2.5 text-xs text-slate-600">
              {description}
            </div>

            <textarea
              value={text}
              onChange={e => setText(e.target.value)}
              placeholder={placeholder}
              rows={8}
              className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm font-mono text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />

            {parsed.length > 0 && (
              <div className="flex items-center gap-4 text-xs">
                <span className="text-emerald-700">{validItems.length} geçerli satır</span>
                {invalidCount > 0 && <span className="text-amber-700">{invalidCount} hatalı satır (atlanacak)</span>}
              </div>
            )}

            {parsed.length > 0 && (
              <div className="rounded-lg border border-slate-200 max-h-56 overflow-y-auto">
                <table className="w-full text-sm">
                  <tbody className="divide-y divide-slate-100">
                    {parsed.slice(0, 100).map(p => (
                      <tr key={p.lineNo} className={p.error ? 'bg-amber-50/40' : ''}>
                        <td className="px-3 py-1.5 w-8 text-center">
                          {p.error
                            ? <XCircle className="h-3.5 w-3.5 text-amber-500 inline" />
                            : <CheckCircle2 className="h-3.5 w-3.5 text-emerald-500 inline" />}
                        </td>
                        <td className="px-3 py-1.5 font-mono text-xs">{p.label || '(boş)'}</td>
                        <td className="px-3 py-1.5 text-xs text-muted-foreground">{p.error}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {submitError && <p className="text-sm text-destructive">{submitError}</p>}

            <DialogFooter className="gap-2">
              <Button variant="outline" onClick={handleClose}>İptal</Button>
              <Button onClick={handleSubmit} disabled={validItems.length === 0 || submitting}>
                {submitting ? 'Aktarılıyor...' : `İçe Aktar (${validItems.length})`}
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
