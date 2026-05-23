import { useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Loader2,
  Plus,
  RefreshCw,
  Save,
  Trash2,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';

import {
  getExamCurricula,
  getExamCurriculumCourses,
  getExamCurriculumTemplate,
  getExamCurriculumTerms,
  saveExamCurriculumTemplate,
  type ExamCurriculumTemplate,
} from '../api/examCurriculum.api';

import {
  useAdminDepartmentsByStructure,
} from '../hooks/useAdminAcademicStructure';
import { useFacultyLookup } from '../hooks/useAdminLookup';

type CalculationMethod = 'Absolute' | 'Relative';

interface ComponentRow {
  id?: number | null;
  clientKey: string;
  name: string;
  examType: string;
  weightText: string;
  order: number;
  isRequired: boolean;
  minimumScoreText: string;
  requiresAttendanceEligibility: boolean;
  prerequisiteValue: string;
}

const examTypeOptions = [
  { value: 'Midterm', label: 'Vize' },
  { value: 'Final', label: 'Final' },
  { value: 'Makeup', label: 'Bütünleme' },
  { value: 'Assignment', label: 'Ödev' },
  { value: 'Project', label: 'Proje' },
  { value: 'Lab', label: 'Laboratuvar' },
  { value: 'Quiz', label: 'Kısa Sınav' },
  { value: 'Other', label: 'Diğer' },
];

function getErrorMessage(error: unknown, fallback: string) {
  const err = error as {
    message?: string;
    response?: {
      status?: number;
      data?: { message?: string; error?: string; title?: string; detail?: string } | string;
    };
  };

  const data = err.response?.data;
  const message =
    typeof data === 'string'
      ? data
      : data?.message ?? data?.error ?? data?.detail ?? data?.title ?? err.message;

  if (message) return message;
  if (err.response?.status) return `${fallback} HTTP ${err.response.status}`;

  return fallback;
}

function createClientKey() {
  return `row-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function createDefaultRows(): ComponentRow[] {
  return [
    {
      clientKey: createClientKey(),
      name: 'Vize',
      examType: 'Midterm',
      weightText: '40',
      order: 1,
      isRequired: true,
      minimumScoreText: '',
      requiresAttendanceEligibility: false,
      prerequisiteValue: '',
    },
    {
      clientKey: createClientKey(),
      name: 'Final',
      examType: 'Final',
      weightText: '60',
      order: 2,
      isRequired: true,
      minimumScoreText: '',
      requiresAttendanceEligibility: false,
      prerequisiteValue: '',
    },
  ];
}

function getExamTypeLabel(value: string) {
  return examTypeOptions.find((item) => item.value === value)?.label ?? value;
}

function getFacultyDisplayName(faculty: { name: string; code: string }) {
  if (faculty.code === 'MUH' || faculty.name === 'Mühendislik Fakültesi') {
    return 'Mühendislik ve Mimarlık Fakültesi';
  }

  return faculty.name;
}

function shouldShowDepartment(department: { code: string; isServiceUnit: boolean }) {
  if (department.isServiceUnit) return false;

  const hiddenCodes = ['AIT', 'TBK', 'TRD', 'YDL'];
  return !hiddenCodes.includes(department.code);
}

function getDepartmentDisplayName(department: { name: string; code: string }) {
  const displayNamesByCode: Record<string, string> = {
    BM: 'Bilgisayar Mühendisliği (İngilizce)',
    BMTR: 'Bilgisayar Mühendisliği (Türkçe)',
    BSM: 'Bilişim Sistemleri Mühendisliği (İngilizce)',
    EEM: 'Elektrik Elektronik Mühendisliği (İngilizce)',
    EM: 'Endüstri Mühendisliği (İngilizce)',
    YM: 'Yazılım Mühendisliği (İngilizce)',
  };

  return displayNamesByCode[department.code] ?? department.name;
}

function mapTemplateToRows(template: ExamCurriculumTemplate): ComponentRow[] {
  if (template.components.length === 0) return createDefaultRows();

  return template.components.map((component, index) => ({
    id: component.id,
    clientKey: createClientKey(),
    name: component.name,
    examType: component.examType,
    weightText: String(component.weight),
    order: component.order || index + 1,
    isRequired: component.isRequired,
    minimumScoreText:
      component.minimumScore === null || component.minimumScore === undefined
        ? ''
        : String(component.minimumScore),
    requiresAttendanceEligibility: component.requiresAttendanceEligibility,
    prerequisiteValue: component.prerequisiteGradeComponentId
      ? `id:${component.prerequisiteGradeComponentId}`
      : '',
  }));
}

export default function ExamCurriculumPage() {
  const facultiesQuery = useFacultyLookup();

  const [facultyIdText, setFacultyIdText] = useState('');
  const [departmentIdText, setDepartmentIdText] = useState('');
  const [academicTermIdText, setAcademicTermIdText] = useState('');
  const [curriculumIdText, setCurriculumIdText] = useState('');
  const [courseIdText, setCourseIdText] = useState('');
  const [calculationMethod, setCalculationMethod] = useState<CalculationMethod>('Absolute');
  const [rows, setRows] = useState<ComponentRow[]>(createDefaultRows());
  const [loadedTemplate, setLoadedTemplate] = useState<ExamCurriculumTemplate | null>(null);

  const facultyId = facultyIdText ? Number(facultyIdText) : 0;
  const departmentId = departmentIdText ? Number(departmentIdText) : 0;
  const academicTermId = academicTermIdText ? Number(academicTermIdText) : 0;
  const curriculumId = curriculumIdText ? Number(curriculumIdText) : 0;
  const courseId = courseIdText ? Number(courseIdText) : 0;

  const departmentsQuery = useAdminDepartmentsByStructure(facultyId > 0 ? facultyId : undefined);

  const departments = (departmentsQuery.data ?? []).filter(shouldShowDepartment);

  const termsQuery = useQuery({
    queryKey: ['exam-curriculum', 'terms'],
    queryFn: getExamCurriculumTerms,
  });

  const curriculaQuery = useQuery({
    queryKey: ['exam-curriculum', 'curricula', departmentId],
    queryFn: () => getExamCurricula(departmentId),
    enabled: departmentId > 0,
  });

  const coursesQuery = useQuery({
    queryKey: ['exam-curriculum', 'curriculum-courses', curriculumId],
    queryFn: () => getExamCurriculumCourses(curriculumId),
    enabled: curriculumId > 0,
  });

  const loadTemplateMut = useMutation({
    mutationFn: () =>
      getExamCurriculumTemplate({
        curriculumId,
        courseId,
        academicTermId,
      }),
    onSuccess: (data) => {
      setLoadedTemplate(data);
      setCalculationMethod(
        data.calculationMethod === 'Relative' ? 'Relative' : 'Absolute'
      );
      setRows(mapTemplateToRows(data));
    },
    onError: (error) => {
      alert(getErrorMessage(error, 'Sınav müfredatı yüklenemedi.'));
    },
  });

  const saveTemplateMut = useMutation({
    mutationFn: saveExamCurriculumTemplate,
    onSuccess: (data) => {
      setLoadedTemplate(data);
      setCalculationMethod(
        data.calculationMethod === 'Relative' ? 'Relative' : 'Absolute'
      );
      setRows(mapTemplateToRows(data));
      alert('Sınav müfredatı başarıyla kaydedildi.');
    },
    onError: (error) => {
      alert(getErrorMessage(error, 'Sınav müfredatı kaydedilemedi.'));
    },
  });

  const totalWeight = useMemo(
    () =>
      rows.reduce((sum, row) => {
        const value = Number(row.weightText);
        return sum + (Number.isFinite(value) ? value : 0);
      }, 0),
    [rows]
  );

  const validationMessages = useMemo(() => {
    const messages: string[] = [];

    if (!academicTermId) messages.push('Akademik dönem seçilmelidir.');
    if (!curriculumId) messages.push('Müfredat seçilmelidir.');
    if (!courseId) messages.push('Ders seçilmelidir.');

    if (rows.length === 0) {
      messages.push('En az bir sınav türü eklemelisiniz.');
    }

    const normalizedNames = new Set<string>();

    for (const row of rows) {
      const name = row.name.trim();
      const normalizedName = name.toLocaleLowerCase('tr-TR');

      if (!name) {
        messages.push('Sınav türü adı boş olamaz.');
      } else if (normalizedNames.has(normalizedName)) {
        messages.push(`Aynı isimde birden fazla sınav türü olamaz: ${name}`);
      } else {
        normalizedNames.add(normalizedName);
      }

      const weight = Number(row.weightText);

      if (!Number.isFinite(weight) || weight <= 0 || weight > 100) {
        messages.push(`${name || 'Sınav türü'} ağırlığı 0 ile 100 arasında olmalıdır.`);
      }

      if (row.minimumScoreText.trim()) {
        const minimumScore = Number(row.minimumScoreText);

        if (!Number.isFinite(minimumScore) || minimumScore < 0 || minimumScore > 100) {
          messages.push(`${name || 'Sınav türü'} baraj puanı 0 ile 100 arasında olmalıdır.`);
        }
      }
    }

    if (totalWeight !== 100) {
      messages.push(`Sınav ağırlıkları toplamı 100 olmalıdır. Şu anki toplam: ${totalWeight}`);
    }

    return Array.from(new Set(messages));
  }, [academicTermId, curriculumId, courseId, rows, totalWeight]);

  const canLoadTemplate = academicTermId > 0 && curriculumId > 0 && courseId > 0;
  const canSave = validationMessages.length === 0 && !saveTemplateMut.isPending;

  const selectedCurriculum = (curriculaQuery.data ?? []).find((item) => item.id === curriculumId);
  const selectedCourse = (coursesQuery.data ?? []).find((item) => item.courseId === courseId);

  const updateRow = (clientKey: string, patch: Partial<ComponentRow>) => {
    setRows((prev) =>
      prev.map((row) => (row.clientKey === clientKey ? { ...row, ...patch } : row))
    );
  };

  const addRow = () => {
    setRows((prev) => [
      ...prev,
      {
        clientKey: createClientKey(),
        name: '',
        examType: 'Other',
        weightText: '',
        order: prev.length + 1,
        isRequired: true,
        minimumScoreText: '',
        requiresAttendanceEligibility: false,
        prerequisiteValue: '',
      },
    ]);
  };

  const removeRow = (clientKey: string) => {
    setRows((prev) =>
      prev
        .filter((row) => row.clientKey !== clientKey)
        .map((row, index) => ({
          ...row,
          order: index + 1,
          prerequisiteValue:
            row.prerequisiteValue === `client:${clientKey}` ? '' : row.prerequisiteValue,
        }))
    );
  };

  const resetSelectionsAfterDepartment = () => {
    setCurriculumIdText('');
    setCourseIdText('');
    setLoadedTemplate(null);
    setRows(createDefaultRows());
    setCalculationMethod('Absolute');
  };

  const resetSelectionsAfterCurriculum = () => {
    setCourseIdText('');
    setLoadedTemplate(null);
    setRows(createDefaultRows());
    setCalculationMethod('Absolute');
  };

  const handleLoadTemplate = () => {
    if (!canLoadTemplate) {
      alert('Lütfen akademik dönem, müfredat ve ders seçin.');
      return;
    }

    loadTemplateMut.mutate();
  };

  const handleSave = () => {
    if (!canSave) {
      alert(validationMessages[0] ?? 'Sınav müfredatı kaydedilemez.');
      return;
    }

    saveTemplateMut.mutate({
      academicTermId,
      curriculumId,
      courseId,
      calculationMethod,
      components: rows.map((row, index) => {
        const prerequisiteGradeComponentId =
          row.prerequisiteValue.startsWith('id:')
            ? Number(row.prerequisiteValue.replace('id:', ''))
            : null;

        const prerequisiteClientKey =
          row.prerequisiteValue.startsWith('client:')
            ? row.prerequisiteValue.replace('client:', '')
            : null;

        return {
          id: row.id ?? null,
          clientKey: row.clientKey,
          name: row.name.trim(),
          examType: row.examType,
          weight: Number(row.weightText),
          order: index + 1,
          isRequired: row.isRequired,
          minimumScore: row.minimumScoreText.trim()
            ? Number(row.minimumScoreText)
            : null,
          requiresAttendanceEligibility: row.requiresAttendanceEligibility,
          prerequisiteGradeComponentId,
          prerequisiteClientKey,
        };
      }),
    });
  };

  return (
    <div className="container mx-auto p-6 space-y-6">
      <Card>
        <CardContent className="p-5 space-y-5">
          

          <div className="grid gap-4 md:grid-cols-5">
            <div className="space-y-2">
              <Label>Fakülte</Label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={facultyIdText}
                disabled={facultiesQuery.isLoading}
                onChange={(e) => {
                  setFacultyIdText(e.target.value);
                  setDepartmentIdText('');
                  resetSelectionsAfterDepartment();
                }}
              >
                <option value="">
                  {facultiesQuery.isLoading ? 'Fakülteler yükleniyor...' : 'Fakülte seçiniz'}
                </option>

                {(facultiesQuery.data ?? []).map((faculty) => (
                  <option key={faculty.id} value={faculty.id}>
                    {getFacultyDisplayName(faculty)}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <Label>Bölüm</Label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={departmentIdText}
                disabled={!facultyIdText || departmentsQuery.isLoading}
                onChange={(e) => {
                  setDepartmentIdText(e.target.value);
                  resetSelectionsAfterDepartment();
                }}
              >
                <option value="">
                  {!facultyIdText
                    ? 'Önce fakülte seçiniz'
                    : departmentsQuery.isLoading
                      ? 'Bölümler yükleniyor...'
                      : 'Bölüm seçiniz'}
                </option>

                {departments.map((department) => (
                  <option key={department.id} value={department.id}>
                    {getDepartmentDisplayName(department)}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <Label>Akademik Dönem</Label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={academicTermIdText}
                disabled={termsQuery.isLoading}
                onChange={(e) => {
                  setAcademicTermIdText(e.target.value);
                  setLoadedTemplate(null);
                  setRows(createDefaultRows());
                }}
              >
                <option value="">
                  {termsQuery.isLoading ? 'Dönemler yükleniyor...' : 'Dönem seçiniz'}
                </option>

                {(termsQuery.data ?? []).map((term) => (
                  <option key={term.id} value={term.id}>
                    {term.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <Label>Müfredat</Label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={curriculumIdText}
                disabled={!departmentId || curriculaQuery.isLoading}
                onChange={(e) => {
                  setCurriculumIdText(e.target.value);
                  resetSelectionsAfterCurriculum();
                }}
              >
                <option value="">
                  {!departmentId
                    ? 'Önce bölüm seçiniz'
                    : curriculaQuery.isLoading
                      ? 'Müfredatlar yükleniyor...'
                      : 'Müfredat seçiniz'}
                </option>

                {(curriculaQuery.data ?? []).map((curriculum) => (
                  <option key={curriculum.id} value={curriculum.id}>
                    {curriculum.name} {curriculum.isActive ? '(Aktif)' : ''}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <Label>Ders</Label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={courseIdText}
                disabled={!curriculumId || coursesQuery.isLoading}
                onChange={(e) => {
                  setCourseIdText(e.target.value);
                  setLoadedTemplate(null);
                  setRows(createDefaultRows());
                }}
              >
                <option value="">
                  {!curriculumId
                    ? 'Önce müfredat seçiniz'
                    : coursesQuery.isLoading
                      ? 'Dersler yükleniyor...'
                      : 'Ders seçiniz'}
                </option>

                {(coursesQuery.data ?? []).map((course) => (
                  <option key={course.courseId} value={course.courseId}>
                    {course.courseCode} - {course.courseName}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="text-sm text-muted-foreground">
              {selectedCurriculum && selectedCourse ? (
                <>
                  Seçili: <strong>{selectedCurriculum.name}</strong> /{' '}
                  <strong>{selectedCourse.courseCode} - {selectedCourse.courseName}</strong>
                </>
              ) : (
                'Sınav şablonunu düzenlemek için dönem, müfredat ve ders seçin.'
              )}
            </div>

            <Button
              variant="outline"
              onClick={handleLoadTemplate}
              disabled={!canLoadTemplate || loadTemplateMut.isPending}
              className="gap-1.5"
            >
              {loadTemplateMut.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="h-4 w-4" />
              )}
              Şablonu Yükle
            </Button>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardContent className="p-4">
            <p className="text-sm text-muted-foreground">Toplam Ağırlık</p>
            <p className={`text-2xl font-bold ${totalWeight === 100 ? 'text-green-700' : 'text-red-700'}`}>
              %{totalWeight}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <p className="text-sm text-muted-foreground">Durum</p>
            <div className="mt-1">
              {totalWeight === 100 ? (
                <Badge className="gap-1">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  Kaydedilebilir
                </Badge>
              ) : (
                <Badge variant="destructive" className="gap-1">
                  <AlertTriangle className="h-3.5 w-3.5" />
                  Toplam 100 değil
                </Badge>
              )}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <p className="text-sm text-muted-foreground">Hesaplama Yöntemi</p>
            <select
              className="mt-2 h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
              value={calculationMethod}
              onChange={(e) => setCalculationMethod(e.target.value as CalculationMethod)}
            >
              <option value="Absolute">Mutlak Sistem</option>
              <option value="Relative">Bağıl Değerlendirme / Çan Eğrisi</option>
            </select>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <p className="text-sm text-muted-foreground">Kayıtlı Şablon</p>
            <p className="text-lg font-semibold">
              {loadedTemplate ? `${loadedTemplate.components.length} sınav türü` : 'Henüz yüklenmedi'}
            </p>
          </CardContent>
        </Card>
      </div>

      {validationMessages.length > 0 && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          <div className="mb-2 flex items-center gap-2 font-medium">
            <AlertTriangle className="h-4 w-4" />
            Kontrol Edilmesi Gerekenler
          </div>
          <ul className="list-disc space-y-1 pl-5">
            {validationMessages.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        </div>
      )}

      <Card>
        <CardContent className="p-0">
          <div className="flex items-center justify-between gap-3 border-b p-4">
            <div>
              <h2 className="font-heading text-lg font-semibold">Sınav Türleri ve Başarı Kuralları</h2>
            </div>

            <Button variant="outline" onClick={addRow} className="gap-1.5">
              <Plus className="h-4 w-4" />
              Yeni Sınav Türü Ekle
            </Button>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b bg-muted/40">
                <tr>
                  <th className="min-w-[180px] px-4 py-3 text-left font-medium text-muted-foreground">
                    Sınav Adı
                  </th>
                  <th className="min-w-[150px] px-4 py-3 text-left font-medium text-muted-foreground">
                    Tip
                  </th>
                  <th className="min-w-[110px] px-4 py-3 text-center font-medium text-muted-foreground">
                    Ağırlık %
                  </th>
                  <th className="min-w-[120px] px-4 py-3 text-center font-medium text-muted-foreground">
                    Baraj Puanı
                  </th>
                  <th className="min-w-[130px] px-4 py-3 text-center font-medium text-muted-foreground">
                    Zorunlu
                  </th>
                  <th className="min-w-[170px] px-4 py-3 text-center font-medium text-muted-foreground">
                    Devamsızlık Şartı
                  </th>
                  <th className="min-w-[240px] px-4 py-3 text-left font-medium text-muted-foreground">
                    Bağımlı Kural
                  </th>
                  <th className="w-[80px] px-4 py-3 text-center font-medium text-muted-foreground">
                    Sil
                  </th>
                </tr>
              </thead>

              <tbody>
                {rows.map((row, index) => (
                  <tr key={row.clientKey} className="border-b last:border-0">
                    <td className="px-4 py-3">
                      <input
                        className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                        value={row.name}
                        placeholder="Örn: Vize, Final, Ödev 1"
                        onChange={(e) => updateRow(row.clientKey, { name: e.target.value })}
                      />
                    </td>

                    <td className="px-4 py-3">
                      <select
                        className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                        value={row.examType}
                        onChange={(e) => {
                          const examType = e.target.value;
                          const currentLabel = getExamTypeLabel(row.examType);
                          const nextLabel = getExamTypeLabel(examType);

                          updateRow(row.clientKey, {
                            examType,
                            name: row.name.trim() === currentLabel ? nextLabel : row.name,
                          });
                        }}
                      >
                        {examTypeOptions.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    </td>

                    <td className="px-4 py-3 text-center">
                      <input
                        type="number"
                        min={0}
                        max={100}
                        step="0.01"
                        className="h-9 w-24 rounded-md border border-input bg-background px-3 text-center text-sm"
                        value={row.weightText}
                        onChange={(e) => updateRow(row.clientKey, { weightText: e.target.value })}
                      />
                    </td>

                    <td className="px-4 py-3 text-center">
                      <input
                        type="number"
                        min={0}
                        max={100}
                        step="0.01"
                        className="h-9 w-24 rounded-md border border-input bg-background px-3 text-center text-sm"
                        value={row.minimumScoreText}
                        placeholder="Yok"
                        onChange={(e) =>
                          updateRow(row.clientKey, { minimumScoreText: e.target.value })
                        }
                      />
                    </td>

                    <td className="px-4 py-3 text-center">
                      <label className="inline-flex items-center gap-2">
                        <input
                          type="checkbox"
                          checked={row.isRequired}
                          onChange={(e) =>
                            updateRow(row.clientKey, { isRequired: e.target.checked })
                          }
                        />
                        <span>Zorunlu</span>
                      </label>
                    </td>

                    <td className="px-4 py-3 text-center">
                      <label className="inline-flex items-center gap-2">
                        <input
                          type="checkbox"
                          checked={row.requiresAttendanceEligibility}
                          onChange={(e) =>
                            updateRow(row.clientKey, {
                              requiresAttendanceEligibility: e.target.checked,
                            })
                          }
                        />
                        <span>Aranır</span>
                      </label>
                    </td>

                    <td className="px-4 py-3">
                      <select
                        className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                        value={row.prerequisiteValue}
                        onChange={(e) =>
                          updateRow(row.clientKey, { prerequisiteValue: e.target.value })
                        }
                      >
                        <option value="">Bağımlı kural yok</option>

                        {rows
                          .filter((candidate) => candidate.clientKey !== row.clientKey)
                          .map((candidate) => (
                            <option
                              key={candidate.clientKey}
                              value={
                                candidate.id
                                  ? `id:${candidate.id}`
                                  : `client:${candidate.clientKey}`
                              }
                            >
                              {row.name || `Satır ${index + 1}`} için önce{' '}
                              {candidate.name || 'seçili sınav'} girilmiş olmalı
                            </option>
                          ))}
                      </select>
                    </td>

                    <td className="px-4 py-3 text-center">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => removeRow(row.clientKey)}
                        disabled={rows.length <= 1}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </td>
                  </tr>
                ))}

                {rows.length === 0 && (
                  <tr>
                    <td colSpan={8} className="px-4 py-10 text-center text-muted-foreground">
                      Henüz sınav türü eklenmedi.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="flex items-center justify-end gap-2 border-t bg-muted/20 p-4">
            <Button
              variant="outline"
              onClick={() => setRows(createDefaultRows())}
              disabled={saveTemplateMut.isPending}
            >
              Varsayılan Vize/Final
            </Button>

            <Button
              onClick={handleSave}
              disabled={!canSave}
              className="gap-1.5"
            >
              {saveTemplateMut.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Save className="h-4 w-4" />
              )}
              Kaydet
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
