interface FacultyOption {
  id: number;
  name: string;
}

interface UserScopeAssignmentFieldProps {
  isVisible: boolean;
  faculties: FacultyOption[];
  facultyId: number | null;
  onFacultyIdChange: (facultyId: number | null) => void;
  isLoading?: boolean;
}

export function UserScopeAssignmentField({
  isVisible,
  faculties,
  facultyId,
  onFacultyIdChange,
  isLoading = false,
}: UserScopeAssignmentFieldProps) {
  if (!isVisible) {
    return null;
  }

  return (
    <div className="mt-4 space-y-2 border-t border-border pt-4">
      <div className="space-y-1">
        <label className="text-sm font-medium text-foreground">Kapsam (Fakülte)</label>
        <p className="text-xs text-muted-foreground">
          Dekan rolü bu fakülteyle sınırlandırılır. Boş bırakılırsa kullanıcı bu panelde boş liste görür.
        </p>
      </div>

      <select
        value={facultyId ?? ''}
        onChange={(event) => onFacultyIdChange(event.target.value === '' ? null : Number(event.target.value))}
        disabled={isLoading}
        className="w-full rounded-lg border border-border bg-white px-3 py-2 text-sm text-foreground"
      >
        <option value="">Kapsam yok</option>
        {faculties.map((faculty) => (
          <option key={faculty.id} value={faculty.id}>
            {faculty.name}
          </option>
        ))}
      </select>

      {isLoading && (
        <p className="text-xs text-muted-foreground">Mevcut kapsam yükleniyor...</p>
      )}
    </div>
  );
}
