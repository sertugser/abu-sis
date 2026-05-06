import { useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Loader2,
  RefreshCw,
  Save,
  Search,
  UserCheck,
  Users,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';

import {
  useApplyBulkAdvisorAssignment,
  usePreviewBulkAdvisorAssignment,
  useUpdateBulkAdvisorSettings,
  type BulkAdvisorAssignmentPreviewResponse,
} from '../hooks/useAdminStudents';

import {
  useAdminDepartmentsByStructure,
  useAdminFaculties,
} from '../hooks/useAdminAcademicStructure';

function getErrorMessage(error: unknown, fallback: string) {
  const err = error as { response?: { data?: { message?: string; error?: string } } };
  return err.response?.data?.message ?? err.response?.data?.error ?? fallback;
}

function getFacultyDisplayName(faculty: { name: string; code: string }) {
  if (faculty.code === 'MUH' || faculty.name === 'Mühendislik Fakültesi') {
    return 'Mühendislik ve Mimarlık Fakültesi';
  }

  return faculty.name;
}

function shouldShowDepartmentOnAdvisorAssignment(department: {
  code: string;
  isServiceUnit: boolean;
}) {
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

export default function BulkAdvisorAssignmentPage() {
  const previewMut = usePreviewBulkAdvisorAssignment();
  const applyMut = useApplyBulkAdvisorAssignment();
  const updateAdvisorSettingsMut = useUpdateBulkAdvisorSettings();

  const facultiesQuery = useAdminFaculties();

  const [facultyIdText, setFacultyIdText] = useState('');
  const [departmentIdText, setDepartmentIdText] = useState('');
  const [includeStudentsWithAdvisor, setIncludeStudentsWithAdvisor] = useState(false);
  const [previewResult, setPreviewResult] = useState<BulkAdvisorAssignmentPreviewResponse | null>(null);
  const [selectedAdvisorIdsByStudent, setSelectedAdvisorIdsByStudent] = useState<Record<number, number>>({});
  const [advisorCapacityTexts, setAdvisorCapacityTexts] = useState<Record<number, string>>({});
  const [excludedAdvisorIds, setExcludedAdvisorIds] = useState<Record<number, boolean>>({});
  const [studentSearch, setStudentSearch] = useState('');
  const [advisorFilterIdText, setAdvisorFilterIdText] = useState('');
  const [advisorFilterType, setAdvisorFilterType] = useState<'assigned' | 'current'>('assigned');
  const [previewPage, setPreviewPage] = useState(1);
  const [previewPageSize, setPreviewPageSize] = useState(50);

  const facultyId = facultyIdText ? Number(facultyIdText) : 0;
  const departmentId = departmentIdText ? Number(departmentIdText) : 0;

  const departmentsQuery = useAdminDepartmentsByStructure(facultyId > 0 ? facultyId : undefined);

  const departments = (departmentsQuery.data ?? []).filter(shouldShowDepartmentOnAdvisorAssignment);

  const selectedDepartment = departments.find((department) => department.id === departmentId);

  const selectedFacultyHasNoDepartment =
    facultyId > 0 && !departmentsQuery.isLoading && departments.length === 0;

  const canPreview =
    facultyId > 0 &&
    departmentId > 0 &&
    !Number.isNaN(facultyId) &&
    !Number.isNaN(departmentId);

  const currentPage = previewResult?.page ?? previewPage;
  const currentPageSize = previewResult?.pageSize ?? previewPageSize;
  const totalPages = previewResult?.totalPages ?? 0;
  const totalAssignmentCount = previewResult?.totalAssignmentCount ?? 0;

  const getAdvisorCapacity = (advisorId: number) => {
    if (excludedAdvisorIds[advisorId]) return 0;

    const rawValue = advisorCapacityTexts[advisorId]?.trim();

    if (!rawValue) return Number.POSITIVE_INFINITY;

    const parsedValue = Number(rawValue);

    if (!Number.isFinite(parsedValue) || parsedValue < 0) return 0;

    return Math.floor(parsedValue);
  };

  const advisorSelectionCounts = previewResult
    ? previewResult.assignments.reduce<Record<number, number>>((acc, item) => {
        const selectedAdvisorId =
          selectedAdvisorIdsByStudent[item.studentId] ?? item.suggestedAdvisorId;

        acc[selectedAdvisorId] = (acc[selectedAdvisorId] ?? 0) + 1;
        return acc;
      }, {})
    : {};

  const advisorLoadRows = previewResult
    ? previewResult.advisors.map((advisor) => {
        const capacity = getAdvisorCapacity(advisor.advisorId);
        const selectedInThisPreview = advisorSelectionCounts[advisor.advisorId] ?? 0;

        return {
          ...advisor,
          capacity,
          selectedInThisPreview,
          isExcluded: excludedAdvisorIds[advisor.advisorId] ?? false,
          hasCapacityViolation: selectedInThisPreview > capacity,
        };
      })
    : [];

  const advisorNameById = previewResult
    ? previewResult.advisors.reduce<Record<number, string>>((acc, advisor) => {
        acc[advisor.advisorId] = advisor.advisorName;
        return acc;
      }, {})
    : {};

  const capacityWarnings = advisorLoadRows
    .filter((advisor) => advisor.hasCapacityViolation)
    .map((advisor) => {
      const capacityText =
        advisor.capacity === Number.POSITIVE_INFINITY ? 'Sınırsız' : advisor.capacity.toString();

      return `${advisor.advisorName}: ${advisor.selectedInThisPreview}/${capacityText}`;
    });

  const hasCapacityViolation = capacityWarnings.length > 0;

  const canApply =
    !!previewResult &&
    previewResult.assignments.length > 0 &&
    !applyMut.isPending &&
    !hasCapacityViolation;

  const normalizedStudentSearch = studentSearch.trim().toLocaleLowerCase('tr-TR');
  const advisorFilterId = advisorFilterIdText ? Number(advisorFilterIdText) : 0;

  const filteredAssignments = previewResult
    ? previewResult.assignments.filter((item) => {
        const selectedAdvisorId =
          selectedAdvisorIdsByStudent[item.studentId] ?? item.suggestedAdvisorId;

        const selectedAdvisorName =
          advisorNameById[selectedAdvisorId] ?? item.suggestedAdvisorName;

        const matchesStudentSearch =
          !normalizedStudentSearch ||
          [
            item.studentName,
            item.studentNumber,
            item.programName,
            item.currentAdvisorName ?? '',
            item.suggestedAdvisorName,
            selectedAdvisorName,
          ].some((value) =>
            value.toLocaleLowerCase('tr-TR').includes(normalizedStudentSearch)
          );

        const matchesAdvisorFilter =
          advisorFilterId <= 0 ||
          (advisorFilterType === 'assigned'
            ? selectedAdvisorId === advisorFilterId
            : item.currentAdvisorId === advisorFilterId);

        return matchesStudentSearch && matchesAdvisorFilter;
      })
    : [];

  const resetPreviewState = () => {
    setPreviewResult(null);
    setSelectedAdvisorIdsByStudent({});
    setAdvisorCapacityTexts({});
    setExcludedAdvisorIds({});
    setStudentSearch('');
    setAdvisorFilterIdText('');
    setAdvisorFilterType('assigned');
    setPreviewPage(1);
  };

  const handlePreview = (
    pageToLoad = 1,
    pageSizeToUse = previewPageSize,
    filters: {
      studentSearch?: string;
      advisorFilterType?: 'assigned' | 'current';
      advisorFilterIdText?: string;
    } = {}
  ) => {
    if (!facultyId) {
      alert('Lütfen fakülte seçin.');
      return;
    }

    if (!departmentId) {
      alert('Lütfen bölüm seçin.');
      return;
    }

    const searchToUse = filters.studentSearch ?? studentSearch;
    const advisorFilterTypeToUse = filters.advisorFilterType ?? advisorFilterType;
    const advisorFilterIdTextToUse = filters.advisorFilterIdText ?? advisorFilterIdText;
    const advisorFilterIdToUse = advisorFilterIdTextToUse ? Number(advisorFilterIdTextToUse) : null;

    previewMut.mutate(
      {
        departmentId,
        programId: null,
        includeStudentsWithAdvisor,
        page: pageToLoad,
        pageSize: pageSizeToUse,
        studentSearch: searchToUse.trim() || null,
        advisorFilterType: advisorFilterIdToUse ? advisorFilterTypeToUse : null,
        advisorFilterId: advisorFilterIdToUse,
      },
      {
        onSuccess: (data) => {
          const initialSelections = data.assignments.reduce<Record<number, number>>((acc, item) => {
            acc[item.studentId] = item.suggestedAdvisorId;
            return acc;
          }, {});

          const initialCapacities = data.advisors.reduce<Record<number, string>>((acc, advisor) => {
            acc[advisor.advisorId] =
              advisor.advisorAssignmentCapacity === null ||
              advisor.advisorAssignmentCapacity === undefined
                ? ''
                : String(advisor.advisorAssignmentCapacity);

            return acc;
          }, {});

          const initialExcludedAdvisors = data.advisors.reduce<Record<number, boolean>>(
            (acc, advisor) => {
              acc[advisor.advisorId] = !advisor.isAvailableForAdvisorAssignment;
              return acc;
            },
            {}
          );

          setPreviewResult(data);
          setPreviewPage(data.page);
          setPreviewPageSize(data.pageSize);
          setSelectedAdvisorIdsByStudent(initialSelections);
          setAdvisorCapacityTexts(initialCapacities);
          setExcludedAdvisorIds(initialExcludedAdvisors);
        },
        onError: (error) => {
          alert(getErrorMessage(error, 'Önizleme alınamadı.'));
        },
      }
    );
  };

  const handleSaveAdvisorSettings = () => {
    if (!previewResult) {
      alert('Kaydedilecek danışman ayarı bulunamadı.');
      return;
    }

    try {
      const advisors = previewResult.advisors.map((advisor) => {
        const rawCapacity = advisorCapacityTexts[advisor.advisorId]?.trim() ?? '';

        let capacity: number | null = null;

        if (rawCapacity !== '') {
          const parsedCapacity = Number(rawCapacity);

          if (!Number.isFinite(parsedCapacity) || parsedCapacity < 0) {
            throw new Error(`${advisor.advisorName} için kontenjan geçerli değil.`);
          }

          capacity = Math.floor(parsedCapacity);
        }

        return {
          advisorId: advisor.advisorId,
          isAvailableForAdvisorAssignment: !(excludedAdvisorIds[advisor.advisorId] ?? false),
          advisorAssignmentCapacity: capacity,
        };
      });

      updateAdvisorSettingsMut.mutate(
        { advisors },
        {
          onSuccess: () => {
            alert('Danışman kontenjan ayarları kaydedildi. Önizleme yenileniyor.');
            handlePreview(currentPage, currentPageSize);
          },
          onError: (error) => {
            alert(getErrorMessage(error, 'Danışman kontenjan ayarları kaydedilemedi.'));
          },
        }
      );
    } catch (error) {
      alert(error instanceof Error ? error.message : 'Danışman kontenjan ayarları geçersiz.');
    }
  };

  const handleRebalanceByCapacities = () => {
    if (!previewResult) return;

    if (previewResult.assignments.length === 0) {
      alert('Yeniden dağıtılacak öğrenci bulunamadı.');
      return;
    }

    const availableAdvisors = previewResult.advisors.filter(
      (advisor) => getAdvisorCapacity(advisor.advisorId) > 0
    );

    if (availableAdvisors.length === 0) {
      alert('Otomatik atama için uygun danışman bulunmuyor. En az bir danışmana kontenjan verin.');
      return;
    }

    let totalFiniteCapacity = 0;
    let hasUnlimitedAdvisor = false;

    for (const advisor of availableAdvisors) {
      const capacity = getAdvisorCapacity(advisor.advisorId);

      if (capacity === Number.POSITIVE_INFINITY) {
        hasUnlimitedAdvisor = true;
      } else {
        totalFiniteCapacity += capacity;
      }
    }

    if (!hasUnlimitedAdvisor && totalFiniteCapacity < previewResult.assignments.length) {
      alert(
        `Bu sayfadaki öğrenciler için toplam danışman kontenjanı yetersiz.\n` +
          `Bu sayfadaki atanacak öğrenci: ${previewResult.assignments.length}\n` +
          `Toplam kontenjan: ${totalFiniteCapacity}`
      );
      return;
    }

    const assignedCounts: Record<number, number> = {};
    const newSelections: Record<number, number> = {};

    for (const advisor of availableAdvisors) {
      assignedCounts[advisor.advisorId] = 0;
    }

    for (const studentAssignment of previewResult.assignments) {
      const candidates = availableAdvisors
        .filter((advisor) => {
          const capacity = getAdvisorCapacity(advisor.advisorId);
          return assignedCounts[advisor.advisorId] < capacity;
        })
        .sort((a, b) => {
          const aProjectedLoad = a.currentStudentCount + assignedCounts[a.advisorId];
          const bProjectedLoad = b.currentStudentCount + assignedCounts[b.advisorId];

          if (aProjectedLoad !== bProjectedLoad) return aProjectedLoad - bProjectedLoad;

          const aAssignedCount = assignedCounts[a.advisorId];
          const bAssignedCount = assignedCounts[b.advisorId];

          if (aAssignedCount !== bAssignedCount) return aAssignedCount - bAssignedCount;

          return a.advisorName.localeCompare(b.advisorName, 'tr-TR');
        });

      const selectedAdvisor = candidates[0];

      if (!selectedAdvisor) {
        alert('Kontenjanlara göre dağıtım tamamlanamadı. Kontenjanları artırın.');
        return;
      }

      newSelections[studentAssignment.studentId] = selectedAdvisor.advisorId;
      assignedCounts[selectedAdvisor.advisorId] += 1;
    }

    setSelectedAdvisorIdsByStudent(newSelections);
    alert('Bu sayfadaki öğrenciler kontenjanlara göre yeniden dağıtıldı.');
  };

  const handleApply = () => {
    if (!previewResult || previewResult.assignments.length === 0) {
      alert('Uygulanacak danışman ataması bulunamadı.');
      return;
    }

    if (hasCapacityViolation) {
      alert(
        'Kontenjan aşımı var. Lütfen kontenjanları artırın veya Kontenjanlara Göre Yeniden Dağıt butonunu kullanın.'
      );
      return;
    }

    const assignmentsToApply = previewResult.assignments.map((item) => ({
      studentId: item.studentId,
      advisorId: selectedAdvisorIdsByStudent[item.studentId] ?? item.suggestedAdvisorId,
    }));

    const confirmed = window.confirm(
      `Bu sayfadaki ${assignmentsToApply.length} öğrenci için danışman ataması uygulanacak. Onaylıyor musunuz?`
    );

    if (!confirmed) return;

    applyMut.mutate(
      {
        assignments: assignmentsToApply,
      },
      {
        onSuccess: (data) => {
          alert(
            `Danışman atama tamamlandı.\n` +
              `Güncellenen: ${data.updatedCount}\n` +
              `Atlanan: ${data.skippedCount}`
          );

          handlePreview(currentPage, currentPageSize);
        },
        onError: (error) => {
          alert(getErrorMessage(error, 'Danışman atamaları uygulanamadı.'));
        },
      }
    );
  };

  const handlePageChange = (newPage: number) => {
    if (!previewResult) return;
    if (newPage < 1 || newPage > previewResult.totalPages) return;

    handlePreview(newPage, currentPageSize);
  };

  const handlePageSizeChange = (newPageSize: number) => {
    setPreviewPageSize(newPageSize);

    if (previewResult) {
      handlePreview(1, newPageSize);
    }
  };

  return (
    <div className="container mx-auto p-6 space-y-6">
      <Card>
        <CardContent className="p-5 space-y-5">
          <div className="flex items-center gap-2">
            <UserCheck className="h-5 w-5 text-primary" />
            <h2 className="font-heading text-lg font-semibold">Atama Kriterleri</h2>
          </div>

          <div className="grid gap-4 md:grid-cols-4">
            <div className="space-y-2">
              <Label>Fakülte</Label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                value={facultyIdText}
                disabled={facultiesQuery.isLoading}
                onChange={(e) => {
                  setFacultyIdText(e.target.value);
                  setDepartmentIdText('');
                  resetPreviewState();
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

              {facultiesQuery.isError ? (
                <p className="text-xs text-red-600">Fakülteler yüklenemedi.</p>
              ) : (
                <p className="text-xs text-muted-foreground">
                  Önce işlem yapılacak fakülteyi seçin.
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label>Bölüm</Label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                value={departmentIdText}
                disabled={!facultyIdText || departmentsQuery.isLoading || selectedFacultyHasNoDepartment}
                onChange={(e) => {
                  setDepartmentIdText(e.target.value);
                  resetPreviewState();
                }}
              >
                <option value="">
                  {!facultyIdText
                    ? 'Önce fakülte seçiniz'
                    : departmentsQuery.isLoading
                      ? 'Bölümler yükleniyor...'
                      : selectedFacultyHasNoDepartment
                        ? 'Bu fakültede bölüm bulunmuyor'
                        : 'Bölüm seçiniz'}
                </option>

                {departments.map((department) => (
                  <option key={department.id} value={department.id}>
                    {getDepartmentDisplayName(department)}
                  </option>
                ))}
              </select>

              {departmentsQuery.isError ? (
                <p className="text-xs text-red-600">Bölümler yüklenemedi.</p>
              ) : selectedFacultyHasNoDepartment ? (
                <p className="text-xs text-amber-700">
                  Seçilen fakültede danışman ataması yapılacak bölüm bulunmuyor.
                </p>
              ) : (
                <p className="text-xs text-muted-foreground">
                  Öğrenciler ve danışmanlar seçilen bölüme göre eşleştirilir.
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label>Mevcut danışmanı olanlar</Label>
              <label className="flex h-10 items-center gap-2 rounded-md border px-3 text-sm">
                <input
                  type="checkbox"
                  checked={includeStudentsWithAdvisor}
                  onChange={(e) => {
                    setIncludeStudentsWithAdvisor(e.target.checked);
                    resetPreviewState();
                  }}
                />
                Yeniden dağıtıma dahil et
              </label>
              <p className="text-xs text-muted-foreground">
                Kapalıysa öğrenciler mevcut danışmanlarıyla devam eder.
              </p>
            </div>

            <div className="space-y-2">
              <Label>Sayfa Başına Öğrenci</Label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                value={previewPageSize}
                onChange={(e) => handlePageSizeChange(Number(e.target.value))}
              >
                <option value={25}>25</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>
              <p className="text-xs text-muted-foreground">
                Büyük listeler sayfalı olarak yüklenir.
              </p>
            </div>
          </div>

          <div className="flex justify-end gap-2">
            <Button
              variant="outline"
              onClick={() => {
                resetPreviewState();
                setFacultyIdText('');
                setDepartmentIdText('');
                setIncludeStudentsWithAdvisor(false);
              }}
            >
              Temizle
            </Button>

            <Button
              onClick={() => handlePreview(1, previewPageSize)}
              disabled={!canPreview || previewMut.isPending}
              className="gap-1.5"
            >
              {previewMut.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="h-4 w-4" />
              )}
              Önizleme Oluştur
            </Button>
          </div>
        </CardContent>
      </Card>

      {previewResult && (
        <>
          <div className="grid gap-4 md:grid-cols-5">
            <Card>
              <CardContent className="p-4">
                <p className="text-sm text-muted-foreground">Bölüm</p>
                <p className="font-semibold">
                  {selectedDepartment
                    ? getDepartmentDisplayName(selectedDepartment)
                    : previewResult.departmentName}
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardContent className="p-4">
                <p className="text-sm text-muted-foreground">Toplam Öğrenci</p>
                <p className="text-2xl font-bold">{previewResult.totalStudentCount}</p>
              </CardContent>
            </Card>

            <Card>
              <CardContent className="p-4">
                <p className="text-sm text-muted-foreground">Atanabilir Öğrenci</p>
                <p className="text-2xl font-bold">{previewResult.totalAssignmentCount}</p>
              </CardContent>
            </Card>

            <Card>
              <CardContent className="p-4">
                <p className="text-sm text-muted-foreground">Bu Sayfadaki Atama</p>
                <p className="text-2xl font-bold">{previewResult.plannedAssignmentCount}</p>
              </CardContent>
            </Card>

            <Card>
              <CardContent className="p-4">
                <p className="text-sm text-muted-foreground">Mevcut Danışmanlı</p>
                <p className="text-2xl font-bold">{previewResult.alreadyAssignedCount}</p>
              </CardContent>
            </Card>
          </div>

          {previewResult.warnings.length > 0 && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
              <div className="flex items-center gap-2 font-medium mb-2">
                <AlertTriangle className="h-4 w-4" />
                Uyarılar
              </div>
              <ul className="list-disc pl-5 space-y-1">
                {previewResult.warnings.map((warning) => (
                  <li key={warning}>{warning}</li>
                ))}
              </ul>
            </div>
          )}

          {capacityWarnings.length > 0 && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800">
              <div className="flex items-center gap-2 font-medium mb-2">
                <AlertTriangle className="h-4 w-4" />
                Kontenjan Aşımı
              </div>
              <p className="mb-2">
                Bazı danışmanlara verilen öğrenci sayısı belirlenen kontenjanı aşıyor.
                Kontenjanı artırın veya yeniden dağıtım yapın.
              </p>
              <ul className="list-disc pl-5 space-y-1">
                {capacityWarnings.map((warning) => (
                  <li key={warning}>{warning}</li>
                ))}
              </ul>
            </div>
          )}

          <Card>
            <CardContent className="p-0">
              <div className="flex items-center justify-between gap-4 p-4 border-b">
                <div>
                  <h2 className="font-heading text-lg font-semibold">Danışman Yük ve Kontenjan Ayarları</h2>
                </div>

                <div className="flex flex-wrap items-center justify-end gap-2">
                  <Badge variant="secondary">{previewResult.advisors.length} danışman</Badge>

                  <Button
                    variant="outline"
                    onClick={handleSaveAdvisorSettings}
                    disabled={updateAdvisorSettingsMut.isPending}
                    className="gap-1.5"
                  >
                    {updateAdvisorSettingsMut.isPending ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Save className="h-4 w-4" />
                    )}
                    Kontenjan Ayarlarını Kaydet
                  </Button>

                  <Button
                    variant="outline"
                    onClick={handleRebalanceByCapacities}
                    className="gap-1.5"
                  >
                    <RefreshCw className="h-4 w-4" />
                    Kontenjanlara Göre Yeniden Dağıt
                  </Button>
                </div>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-muted/40 border-b">
                    <tr>
                      <th className="text-left px-4 py-3 font-medium text-muted-foreground">
                        Danışman
                      </th>
                      <th className="text-center px-4 py-3 font-medium text-muted-foreground">
                        Genel Mevcut Yük
                      </th>
                      <th className="text-center px-4 py-3 font-medium text-muted-foreground">
                        Otomatik Atamaya Dahil
                      </th>
                      <th className="text-center px-4 py-3 font-medium text-muted-foreground">
                        Bu Atamadaki Kontenjan
                      </th>
                      <th className="text-center px-4 py-3 font-medium text-muted-foreground">
                        Bu Sayfada Seçilen
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {advisorLoadRows.map((advisor) => {
                      const capacityText =
                        advisorCapacityTexts[advisor.advisorId] ?? '';

                      return (
                        <tr
                          key={advisor.advisorId}
                          className={`border-b last:border-0 ${
                            advisor.hasCapacityViolation ? 'bg-red-50' : ''
                          }`}
                        >
                          <td className="px-4 py-3 font-medium">{advisor.advisorName}</td>

                          <td className="px-4 py-3 text-center font-mono">
                            {advisor.currentStudentCount}
                          </td>

                          <td className="px-4 py-3 text-center">
                            <input
                              type="checkbox"
                              checked={!advisor.isExcluded}
                              onChange={(e) => {
                                const isIncluded = e.target.checked;

                                setExcludedAdvisorIds((prev) => ({
                                  ...prev,
                                  [advisor.advisorId]: !isIncluded,
                                }));
                              }}
                            />
                          </td>

                          <td className="px-4 py-3 text-center">
                            <input
                              type="number"
                              min={0}
                              className="h-9 w-28 rounded-md border border-input bg-background px-3 text-center text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
                              placeholder="Sınırsız"
                              value={capacityText}
                              disabled={advisor.isExcluded}
                              onChange={(e) => {
                                setAdvisorCapacityTexts((prev) => ({
                                  ...prev,
                                  [advisor.advisorId]: e.target.value,
                                }));
                              }}
                            />
                          </td>

                          <td
                            className={`px-4 py-3 text-center font-mono font-semibold ${
                              advisor.hasCapacityViolation ? 'text-red-700' : ''
                            }`}
                          >
                            {advisor.selectedInThisPreview}
                          </td>
                        </tr>
                      );
                    })}

                    {advisorLoadRows.length === 0 && (
                      <tr>
                        <td colSpan={5} className="px-4 py-10 text-center text-muted-foreground">
                          Uygun danışman bulunamadı.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-0">
              <div className="flex items-center justify-between p-4 border-b">
                <div>
                  <h2 className="font-heading text-lg font-semibold">Atama Önizlemesi</h2>
                  <p className="text-sm text-muted-foreground">
                    Otomatik önerilen danışmanı isterseniz manuel olarak değiştirebilirsiniz.
                  </p>
                </div>
                <Badge variant="secondary">
                  {studentSearch.trim() || advisorFilterIdText
                    ? `${filteredAssignments.length}/${previewResult.assignments.length} bu sayfa`
                    : `${previewResult.assignments.length}/${totalAssignmentCount} öğrenci`}
                </Badge>
              </div>

              <div className="border-b bg-muted/10 p-4 space-y-3">
                <div className="grid gap-3 md:grid-cols-3">
                  <div className="relative">
                    <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                    <input
                      className="h-10 w-full rounded-md border border-input bg-background pl-9 pr-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                      placeholder="Bu sayfada öğrenci adı, soyadı veya numarası ara..."
                      value={studentSearch}
                      onChange={(e) => setStudentSearch(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                          handlePreview(1, currentPageSize, { studentSearch });
                        }
                      }}
                    />
                  </div>

                  <select
                    className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    value={advisorFilterType}
                    onChange={(e) => {
                      const nextFilterType = e.target.value as 'assigned' | 'current';
                      setAdvisorFilterType(nextFilterType);

                      if (previewResult) {
                        handlePreview(1, currentPageSize, { advisorFilterType: nextFilterType });
                      }
                    }}
                  >
                    <option value="assigned">Atanacak danışmana göre filtrele</option>
                    <option value="current">Mevcut danışmana göre filtrele</option>
                  </select>

                  <select
                    className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    value={advisorFilterIdText}
                    onChange={(e) => {
                      const nextAdvisorFilterIdText = e.target.value;
                      setAdvisorFilterIdText(nextAdvisorFilterIdText);

                      if (previewResult) {
                        handlePreview(1, currentPageSize, {
                          advisorFilterIdText: nextAdvisorFilterIdText,
                        });
                      }
                    }}
                  >
                    <option value="">Tüm danışmanlar</option>
                    {previewResult.advisors.map((advisor) => (
                      <option key={advisor.advisorId} value={advisor.advisorId}>
                        {advisor.advisorName}
                      </option>
                    ))}
                  </select>
                </div>

                <p className="text-xs text-muted-foreground">
                  Arama ve danışman filtresi yalnızca mevcut sayfadaki tabloyu filtreler. Büyük öğrenci listeleri backend tarafında sayfalı getirilir.
                </p>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-muted/40 border-b">
                    <tr>
                      <th className="text-left px-4 py-3 font-medium text-muted-foreground">
                        Öğrenci
                      </th>
                      <th className="text-left px-4 py-3 font-medium text-muted-foreground">
                        Program
                      </th>
                      <th className="text-center px-4 py-3 font-medium text-muted-foreground">
                        Dönem
                      </th>
                      <th className="text-left px-4 py-3 font-medium text-muted-foreground">
                        Mevcut Danışman
                      </th>
                      <th className="text-left px-4 py-3 font-medium text-muted-foreground">
                        Atanacak Danışman
                      </th>
                      <th className="text-left px-4 py-3 font-medium text-muted-foreground">
                        Açıklama
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredAssignments.map((item) => {
                      const selectedAdvisorId =
                        selectedAdvisorIdsByStudent[item.studentId] ?? item.suggestedAdvisorId;

                      const isManuallyChanged = selectedAdvisorId !== item.suggestedAdvisorId;

                      return (
                        <tr key={item.studentId} className="border-b last:border-0">
                          <td className="px-4 py-3">
                            <div className="font-medium">{item.studentName}</div>
                            <div className="text-xs text-muted-foreground font-mono">
                              {item.studentNumber}
                            </div>
                          </td>
                          <td className="px-4 py-3">{item.programName}</td>
                          <td className="px-4 py-3 text-center font-mono">
                            {item.completedSemesters}
                          </td>
                          <td className="px-4 py-3">
                            {item.currentAdvisorName ?? (
                              <span className="text-muted-foreground">—</span>
                            )}
                          </td>
                          <td className="px-4 py-3">
                            <select
                              className="h-9 w-full min-w-[220px] rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                              value={selectedAdvisorId}
                              onChange={(e) => {
                                const newAdvisorId = Number(e.target.value);

                                setSelectedAdvisorIdsByStudent((prev) => ({
                                  ...prev,
                                  [item.studentId]: newAdvisorId,
                                }));
                              }}
                            >
                              {previewResult.advisors.map((advisor) => (
                                <option key={advisor.advisorId} value={advisor.advisorId}>
                                  {advisor.advisorName}
                                </option>
                              ))}
                            </select>

                            {isManuallyChanged && (
                              <p className="mt-1 text-xs text-amber-700">
                                Manuel değiştirildi.
                              </p>
                            )}
                          </td>
                          <td className="px-4 py-3 text-muted-foreground">
                            {isManuallyChanged ? 'Admin tarafından manuel danışman seçildi.' : item.reason}
                          </td>
                        </tr>
                      );
                    })}

                    {filteredAssignments.length === 0 && (
                      <tr>
                        <td colSpan={6} className="px-4 py-10 text-center text-muted-foreground">
                          {studentSearch.trim() || advisorFilterIdText
                            ? 'Filtreye uygun öğrenci bulunamadı.'
                            : 'Atanacak öğrenci bulunamadı.'}
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              <div className="flex flex-col gap-3 border-t bg-muted/20 p-4 md:flex-row md:items-center md:justify-between">
                <div className="text-sm text-muted-foreground">
                  Sayfa {totalPages === 0 ? 0 : currentPage} / {totalPages} · Toplam atanabilir öğrenci: {totalAssignmentCount}
                </div>

                <div className="flex flex-wrap items-center justify-end gap-2">
                  <Button
                    variant="outline"
                    onClick={() => handlePageChange(currentPage - 1)}
                    disabled={previewMut.isPending || currentPage <= 1}
                  >
                    Önceki
                  </Button>

                  <Button
                    variant="outline"
                    onClick={() => handlePageChange(currentPage + 1)}
                    disabled={previewMut.isPending || totalPages === 0 || currentPage >= totalPages}
                  >
                    Sonraki
                  </Button>

                  <Button
                    variant="outline"
                    onClick={() => handlePreview(currentPage, currentPageSize)}
                    disabled={!canPreview || previewMut.isPending}
                    className="gap-1.5"
                  >
                    {previewMut.isPending ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <RefreshCw className="h-4 w-4" />
                    )}
                    Yeniden Önizle
                  </Button>

                  <Button
                    onClick={handleApply}
                    disabled={!canApply}
                    className="gap-1.5"
                  >
                    {applyMut.isPending ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <CheckCircle2 className="h-4 w-4" />
                    )}
                    Bu Sayfayı Onayla ve Ata
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        </>
      )}

      {!previewResult && (
        <div className="flex flex-col items-center justify-center py-20 text-center border border-dashed border-border rounded-lg bg-muted/20">
          <Users className="h-12 w-12 text-muted-foreground/40 mb-4" />
          <h2 className="font-heading text-lg font-semibold text-foreground mb-2">
            Önizleme Bekleniyor
          </h2>
          <p className="text-sm text-muted-foreground max-w-md">
            Fakülte ve bölüm seçip önizleme oluşturduğunuzda sistem öğrencileri uygun
            danışmanlara dengeli şekilde dağıtacaktır.
          </p>
        </div>
      )}
    </div>
  );
}
