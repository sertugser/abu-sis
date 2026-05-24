import { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  useStudentPersonalInfo,
  useUpdateStudentPersonalInfo,
  type StudentPersonalInfo,
  type UpdateStudentPersonalInfo,
} from '../../../hooks/useStudentCard';
import { Button } from '@/components/ui/button';

const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';
const INPUT_CLASS =
  'mt-1 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60';

interface ContactFields {
  mobilePhone: string;
  phone: string;
  secondaryEmail: string;
  addressProvince: string;
  addressDistrict: string;
  addressLine: string;
  postalCode: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
  emergencyContactRelation: string;
}

const EMPTY: ContactFields = {
  mobilePhone: '',
  phone: '',
  secondaryEmail: '',
  addressProvince: '',
  addressDistrict: '',
  addressLine: '',
  postalCode: '',
  emergencyContactName: '',
  emergencyContactPhone: '',
  emergencyContactRelation: '',
};

function seedFrom(data: StudentPersonalInfo): ContactFields {
  return {
    mobilePhone: data.mobilePhone ?? '',
    phone: data.phone ?? '',
    secondaryEmail: data.secondaryEmail ?? '',
    addressProvince: data.addressProvince ?? '',
    addressDistrict: data.addressDistrict ?? '',
    addressLine: data.addressLine ?? '',
    postalCode: data.postalCode ?? '',
    emergencyContactName: data.emergencyContactName ?? '',
    emergencyContactPhone: data.emergencyContactPhone ?? '',
    emergencyContactRelation: data.emergencyContactRelation ?? '',
  };
}

function toNullable(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

export default function ContactInfoPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission('menu.ogrenci-rehberi.edit-personal');

  const studentId = context?.student.id ?? null;
  const { data: loaded } = useStudentPersonalInfo(studentId);
  const update = useUpdateStudentPersonalInfo();

  const [form, setForm] = useState<ContactFields>(EMPTY);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Re-seed when navigating between student cards (panel stays mounted) AND when the
  // query data arrives. Intentional id/data-keyed reseed, not a render loop.
  /* eslint-disable react-hooks/set-state-in-effect */
  useEffect(() => {
    setForm(loaded ? seedFrom(loaded) : EMPTY);
    setError(null);
    setSuccess(null);
  }, [studentId, loaded]);
  /* eslint-enable react-hooks/set-state-in-effect */

  if (!context) return null;
  const { student } = context;

  const setField = (key: keyof ContactFields) => (value: string) => {
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
      mobilePhone: toNullable(form.mobilePhone),
      phone: toNullable(form.phone),
      secondaryEmail: toNullable(form.secondaryEmail),
      addressProvince: toNullable(form.addressProvince),
      addressDistrict: toNullable(form.addressDistrict),
      addressLine: toNullable(form.addressLine),
      postalCode: toNullable(form.postalCode),
      emergencyContactName: toNullable(form.emergencyContactName),
      emergencyContactPhone: toNullable(form.emergencyContactPhone),
      emergencyContactRelation: toNullable(form.emergencyContactRelation),
    };
    try {
      await update.mutateAsync({ studentId: student.id, dto });
      setSuccess('İletişim bilgileri güncellendi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Güncellenemedi.');
    }
  };

  const textField = (
    key: keyof ContactFields,
    label: string,
    type: 'text' | 'email' | 'tel' = 'text',
  ) => (
    <div>
      <label className={FIELD_LABEL} htmlFor={`field-${key}`}>
        {label}
      </label>
      <input
        id={`field-${key}`}
        type={type}
        className={INPUT_CLASS}
        value={form[key]}
        disabled={!canEdit}
        onChange={(e) => setField(key)(e.target.value)}
      />
    </div>
  );

  return (
    <div className="max-w-2xl space-y-6">
      <div className="grid grid-cols-2 gap-x-6 gap-y-4">
        {textField('mobilePhone', 'Cep Telefonu', 'tel')}
        {textField('phone', 'Telefon', 'tel')}
        <div className="col-span-2">{textField('secondaryEmail', 'İkincil E-posta', 'email')}</div>
      </div>

      <div className="space-y-4">
        <h3 className="text-sm font-semibold">Adres</h3>
        <div className="grid grid-cols-2 gap-x-6 gap-y-4">
          {textField('addressProvince', 'İl')}
          {textField('addressDistrict', 'İlçe')}
          <div className="col-span-2">{textField('addressLine', 'Adres')}</div>
          {textField('postalCode', 'Posta Kodu')}
        </div>
      </div>

      <div className="space-y-4">
        <h3 className="text-sm font-semibold">Acil Durum</h3>
        <div className="grid grid-cols-2 gap-x-6 gap-y-4">
          {textField('emergencyContactName', 'Ad Soyad')}
          {textField('emergencyContactPhone', 'Telefon', 'tel')}
          <div className="col-span-2">{textField('emergencyContactRelation', 'Yakınlık')}</div>
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
