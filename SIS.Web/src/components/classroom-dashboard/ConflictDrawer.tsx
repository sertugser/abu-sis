import { Link } from 'react-router-dom';
import {
  Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger,
} from '@/components/ui/sheet';
import { AlertTriangle } from 'lucide-react';
import type { SchedulingConflict } from '@/types/scheduling';

interface ConflictDrawerProps {
  conflicts: SchedulingConflict[];
}

export default function ConflictDrawer({ conflicts }: ConflictDrawerProps) {
  if (conflicts.length === 0) return null;

  return (
    <Sheet>
      <SheetTrigger asChild>
        <button className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-red-200 bg-red-50 text-red-700 text-sm font-medium hover:bg-red-100 transition-colors">
          <AlertTriangle className="h-4 w-4" />
          {conflicts.length} Çakışma
        </button>
      </SheetTrigger>
      <SheetContent side="right" className="w-[420px] sm:max-w-[420px]">
        <SheetHeader>
          <SheetTitle className="flex items-center gap-2">
            <AlertTriangle className="h-5 w-5 text-red-600" />
            {conflicts.length} Çakışma Tespit Edildi
          </SheetTitle>
        </SheetHeader>
        <div className="mt-4 space-y-2 overflow-y-auto max-h-[calc(100vh-120px)]">
          {conflicts.map((c, i) => (
            <div key={i} className="p-3 rounded-lg border border-red-100 bg-red-50/50 space-y-1">
              <div className="flex items-center gap-2">
                <span className="font-mono text-xs font-bold text-[#003366]">{c.courseCode}</span>
                <span className="text-sm">{c.courseName}</span>
              </div>
              <p className="text-xs text-gray-600">{c.message}</p>
              {c.classroomName && (
                <p className="text-xs text-muted-foreground">Derslik: {c.classroomName}</p>
              )}
              <Link
                to="/ders-programi-olusturma"
                className="text-xs text-blue-600 hover:text-blue-800 hover:underline"
              >
                Programlamaya Git &rarr;
              </Link>
            </div>
          ))}
        </div>
      </SheetContent>
    </Sheet>
  );
}
