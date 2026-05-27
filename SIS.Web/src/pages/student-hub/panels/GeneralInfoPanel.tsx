import { useEffect, useRef, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  useStudentPersonalInfo,
  useUpdateStudentPersonalInfo,
  useStudentPhoto,
  useUploadStudentPhoto,
  useDeleteStudentPhoto,
  type StudentPersonalInfo,
  type UpdateStudentPersonalInfo,
} from '../../../hooks/useStudentCard';
import { Button } from '@/components/ui/button';
import { toDateInput, toNullable } from '../panelUtils';

const MAX_PHOTO_BYTES = 2_000_000;
const ACCEPTED_PHOTO_TYPES = ['image/png', 'image/jpeg'];

interface PhotoSectionProps {
  studentId: number;
  studentName: string;
  hasPhoto: boolean;
  canEdit: boolean;
}

function PhotoSection({ studentId, studentName, hasPhoto, canEdit }: PhotoSectionProps) {
  const photoUrl = useStudentPhoto(studentId, hasPhoto);
  const upload = useUploadStudentPhoto();
  const remove = useDeleteStudentPhoto();
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const pending = upload.isPending || remove.isPending;

  const handleSelect = async (file: File | undefined) => {
    setError(null);
    setSuccess(null);
    if (!file) return;
    if (!ACCEPTED_PHOTO_TYPES.includes(file.type)) {
      setError('Yalnızca JPEG/PNG.');
      return;
    }
    if (file.size > MAX_PHOTO_BYTES) {
      setError("Dosya 2MB'tan büyük olamaz.");
      return;
    }
    try {
      await upload.mutateAsync({ studentId, file });
      setSuccess('Fotoğraf yüklendi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Fotoğraf yüklenemedi.');
    } finally {
      if (inputRef.current) inputRef.current.value = '';
    }
  };

  const handleRemove = async () => {
    setError(null);
    setSuccess(null);
    try {
      await remove.mutateAsync({ studentId });
      setSuccess('Fotoğraf kaldırıldı.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Fotoğraf kaldırılamadı.');
    }
  };

  return (
    <section className="space-y-3">
      <p className={FIELD_LABEL}>Fotoğraf</p>
      <div className="flex items-center gap-4">
        {photoUrl ? (
          <img src={photoUrl} alt={studentName} className="h-20 w-20 rounded-full object-cover" />
        ) : (
          <div
            aria-hidden="true"
            className="h-20 w-20 rounded-full bg-muted flex items-center justify-center text-xs text-muted-foreground"
          >
            Fotoğraf yok
          </div>
        )}
        {canEdit && (
          <div className="flex flex-col gap-2">
            <input
              ref={inputRef}
              type="file"
              accept="image/png,image/jpeg"
              disabled={pending}
              className="text-sm file:mr-3 file:rounded-md file:border file:border-input file:bg-background file:px-3 file:py-1.5 file:text-sm file:font-medium disabled:cursor-not-allowed disabled:opacity-60"
              onChange={(e) => void handleSelect(e.target.files?.[0])}
            />
            {hasPhoto && (
              <Button
                variant="outline"
                size="sm"
                className="w-fit"
                disabled={pending}
                onClick={() => void handleRemove()}
              >
                {remove.isPending ? 'Kaldırılıyor...' : 'Kaldır'}
              </Button>
            )}
          </div>
        )}
      </div>
      {upload.isPending && <p className="text-sm text-muted-foreground">Yükleniyor...</p>}
      {error && <p className="text-sm text-destructive">{error}</p>}
      {success && <p className="text-sm text-emerald-600">{success}</p>}
    </section>
  );
}

const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';
const INPUT_CLASS =
  'mt-1 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60';

const GENDER_LABELS: Record<string, string> = {
  Unspecified: 'Belirtilmemiş',
  Male: 'Erkek',
  Female: 'Kadın',
};

const MARITAL_LABELS: Record<string, string> = {
  Unspecified: 'Belirtilmemiş',
  Single: 'Bekar',
  Married: 'Evli',
};

interface GeneralFields {
  birthDate: string;
  birthPlace: string;
  gender: string;
  nationality: string;
  bloodType: string;
  maritalStatus: string;
  passportNo: string;
}

const EMPTY: GeneralFields = {
  birthDate: '',
  birthPlace: '',
  gender: 'Unspecified',
  nationality: '',
  bloodType: '',
  maritalStatus: 'Unspecified',
  passportNo: '',
};

function seedFrom(data: StudentPersonalInfo): GeneralFields {
  return {
    birthDate: toDateInput(data.birthDate),
    birthPlace: data.birthPlace ?? '',
    gender: data.gender ?? 'Unspecified',
    nationality: data.nationality ?? '',
    bloodType: data.bloodType ?? '',
    maritalStatus: data.maritalStatus ?? 'Unspecified',
    passportNo: data.passportNo ?? '',
  };
}

export default function GeneralInfoPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission('menu.ogrenci-rehberi.edit-personal');

  const studentId = context?.student.id ?? null;
  const { data: loaded } = useStudentPersonalInfo(studentId);
  const update = useUpdateStudentPersonalInfo();

  const [form, setForm] = useState<GeneralFields>(EMPTY);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Re-seed when navigating between student cards AND when the query data arrives.
  /* eslint-disable react-hooks/set-state-in-effect */
  useEffect(() => {
    setForm(loaded ? seedFrom(loaded) : EMPTY);
    setError(null);
    setSuccess(null);
  }, [studentId, loaded]);
  /* eslint-enable react-hooks/set-state-in-effect */

  if (!context) return null;
  const { student } = context;

  const setField = (key: keyof GeneralFields) => (value: string) => {
    if (success) setSuccess(null);
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSave = async () => {
    setError(null);
    setSuccess(null);
    if (!loaded) return;
    // Edit only this panel's slice but submit the FULL object so other sections aren't wiped.
    const dto: UpdateStudentPersonalInfo = {
      ...loaded,
      birthDate: toNullable(form.birthDate),
      birthPlace: toNullable(form.birthPlace),
      gender: form.gender,
      nationality: toNullable(form.nationality),
      bloodType: toNullable(form.bloodType),
      maritalStatus: form.maritalStatus,
      passportNo: toNullable(form.passportNo),
    };
    try {
      await update.mutateAsync({ studentId: student.id, dto });
      setSuccess('Genel bilgiler güncellendi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Güncellenemedi.');
    }
  };

  return (
    <div className="max-w-2xl space-y-6">
      <PhotoSection
        studentId={student.id}
        studentName={`${student.firstName} ${student.lastName}`}
        hasPhoto={loaded?.photoExtension != null}
        canEdit={canEdit}
      />

      <div className="grid grid-cols-2 gap-x-6 gap-y-4">
        <div>
          <label className={FIELD_LABEL} htmlFor="field-birthDate">
            Doğum Tarihi
          </label>
          <input
            id="field-birthDate"
            type="date"
            className={INPUT_CLASS}
            value={form.birthDate}
            disabled={!canEdit}
            onChange={(e) => setField('birthDate')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-birthPlace">
            Doğum Yeri
          </label>
          <input
            id="field-birthPlace"
            type="text"
            className={INPUT_CLASS}
            value={form.birthPlace}
            disabled={!canEdit}
            onChange={(e) => setField('birthPlace')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-gender">
            Cinsiyet
          </label>
          <select
            id="field-gender"
            className={INPUT_CLASS}
            value={form.gender}
            disabled={!canEdit}
            onChange={(e) => setField('gender')(e.target.value)}
          >
            {Object.entries(GENDER_LABELS).map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-nationality">
            Uyruk
          </label>
          <input
            id="field-nationality"
            type="text"
            className={INPUT_CLASS}
            value={form.nationality}
            disabled={!canEdit}
            onChange={(e) => setField('nationality')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-bloodType">
            Kan Grubu
          </label>
          <input
            id="field-bloodType"
            type="text"
            className={INPUT_CLASS}
            value={form.bloodType}
            disabled={!canEdit}
            onChange={(e) => setField('bloodType')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-maritalStatus">
            Medeni Durum
          </label>
          <select
            id="field-maritalStatus"
            className={INPUT_CLASS}
            value={form.maritalStatus}
            disabled={!canEdit}
            onChange={(e) => setField('maritalStatus')(e.target.value)}
          >
            {Object.entries(MARITAL_LABELS).map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
          </select>
        </div>

        <div className="col-span-2">
          <label className={FIELD_LABEL} htmlFor="field-passportNo">
            Pasaport No
          </label>
          <input
            id="field-passportNo"
            type="text"
            className={INPUT_CLASS}
            value={form.passportNo}
            disabled={!canEdit}
            onChange={(e) => setField('passportNo')(e.target.value)}
          />
        </div>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {success && <p className="text-sm text-emerald-600">{success}</p>}

      {canEdit && (
        <Button onClick={() => void handleSave()} disabled={update.isPending}>
          {update.isPending ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      )}
    </div>
  );
}
